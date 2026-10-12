using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class GameSessionTests
    {
        private GameConfig _config;
        private FakeClock _clock;
        private EventBus _bus;
        private RecordingSaveRequests _save;
        private List<object> _published;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _clock = new FakeClock();
            _bus = new EventBus();
            _save = new RecordingSaveRequests();
            _published = new List<object>();
            _bus.Subscribe<object>(e => _published.Add(e));
        }

        private GameSession NewSession(GameWorld world = null) =>
            new GameSession(world ?? GameWorld.CreateNew(_config, 1), _clock, _bus, _save, _clock.UtcNowMs);

        [Test]
        public void Tick_AdvancesByRealTime_AndPublishesEvents()
        {
            var session = NewSession();
            session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);
            _published.Clear();

            _clock.Advance(20_000);
            session.Tick();

            Assert.That(session.World.GameTimeMs, Is.EqualTo(20_000L));
            Assert.That(session.World.Food, Is.EqualTo(13));
            Assert.That(_published.OfType<ActivityCycleCompleted>().Count(), Is.EqualTo(1));
            Assert.That(_published.OfType<OfflineProgressApplied>(), Is.Empty);
        }

        [Test]
        public void Tick_ClockMovedBackwards_DoesNothing()
        {
            var session = NewSession();
            _clock.Advance(-50_000);
            session.Tick();
            _clock.Advance(10_000);
            session.Tick();

            Assert.That(session.World.GameTimeMs, Is.EqualTo(10_000L));
        }

        [Test]
        public void LongAbsence_IsCappedAndSummarised()
        {
            var session = NewSession();
            session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);

            _clock.Advance(3 * 60 * 60_000L);
            session.Tick();

            Assert.That(session.World.GameTimeMs, Is.EqualTo(60 * 60_000L));
            var summary = _published.OfType<OfflineProgressApplied>().Single().Summary;
            Assert.That(summary.WasCapped, Is.True);
            Assert.That(summary.FoodGained, Is.GreaterThan(0));
            Assert.That(summary.Stopped.Single().Reason, Is.EqualTo(IdleReason.Tired));
        }

        [Test]
        public void DebugTimeScale_MultipliesProgress()
        {
            var session = NewSession();
            session.DebugTimeScale = 10;

            _clock.Advance(1_000);
            session.Tick();

            Assert.That(session.World.GameTimeMs, Is.EqualTo(10_000L));
        }

        [Test]
        public void Command_CatchesUpTimeBeforeValidating()
        {
            var session = NewSession();
            session.AssignActivity(TestIds.Ryzhy, TestIds.TrainStr);

            _clock.Advance(30_000);
            var result = session.StopActivity(TestIds.Ryzhy);

            Assert.That(result.IsOk, Is.True);
            var ryzhy = session.World.Cats[0];
            Assert.That(ryzhy.GetXp(AttributeType.Strength), Is.EqualTo(15), "the completed cycle is kept, only the next one is cancelled");
            Assert.That(ryzhy.Activity.IdleReason, Is.EqualTo(IdleReason.Stopped));
        }

        [Test]
        public void FailedCommand_ChangesNothing_AndPublishesNothing()
        {
            var tired = TestCats.Make(TestIds.Ryzhy, energy: 5);
            var session = NewSession(TestWorlds.Make(_config, cats: tired));
            _published.Clear();

            var result = session.AssignActivity(TestIds.Ryzhy, TestIds.TrainStr);

            Assert.That(result.Error, Is.EqualTo(CommandError.Tired));
            Assert.That(tired.Activity.IsWorking, Is.False);
            Assert.That(_published, Is.Empty);
            Assert.That(_save.Deferred + _save.Immediate, Is.Zero);
        }

        [Test]
        public void Commands_MapErrors()
        {
            var session = NewSession(TestWorlds.Make(_config, food: 0, cats: TestCats.Make(TestIds.Ryzhy, satiety: 50)));

            Assert.That(session.AssignActivity(new CatId("cat_x"), TestIds.Sleep).Error, Is.EqualTo(CommandError.UnknownCat));
            Assert.That(session.AssignActivity(TestIds.Ryzhy, TestIds.Sleep).Error, Is.EqualTo(CommandError.EnergyFull));
            Assert.That(session.StopActivity(TestIds.Ryzhy).Error, Is.EqualTo(CommandError.NotWorking));
            Assert.That(session.FeedCat(TestIds.Ryzhy).Error, Is.EqualTo(CommandError.NoFood));
            Assert.That(session.EquipSkill(TestIds.Ryzhy, 0, TestIds.Bite).Error, Is.EqualTo(CommandError.SkillLocked));
            Assert.That(session.BuyUpgrade(TestIds.Can).Error, Is.EqualTo(CommandError.NotEnoughJunk));
        }

        [Test]
        public void SuccessfulCommands_RequestSaves()
        {
            var session = NewSession(TestWorlds.Make(_config, food: 5, junk: 20, cats: TestCats.Make(TestIds.Ryzhy, satiety: 50)));

            Assert.That(session.FeedCat(TestIds.Ryzhy).IsOk, Is.True);
            Assert.That(session.BuyUpgrade(TestIds.Can).IsOk, Is.True);

            Assert.That((_save.Deferred, _save.Immediate), Is.EqualTo((1, 1)));
            Assert.That(_published.OfType<UpgradePurchased>().Single().NewLevel, Is.EqualTo(1));
        }
    }
}
