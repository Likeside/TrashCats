using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class TutorialServiceTests
    {
        private GameConfig _config;
        private FakeClock _clock;
        private EventBus _bus;
        private GameSession _session;
        private TutorialService _tutorial;
        private List<string> _hints;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _clock = new FakeClock();
            _bus = new EventBus();
            Start(GameWorld.CreateNew(_config, 1));
        }

        [TearDown]
        public void TearDown() => _tutorial.Dispose();

        private void Start(GameWorld world)
        {
            _tutorial?.Dispose();
            _session = new GameSession(world, _clock, _bus, new RecordingSaveRequests(), _clock.UtcNowMs);
            _tutorial = new TutorialService(_session, _bus);
            _hints = new List<string>();
            _tutorial.HintRequested += _hints.Add;
            _tutorial.Evaluate();
        }

        private string Step => _tutorial.Current?.Id;

        private void Pass(long ms)
        {
            _clock.Advance(ms);
            _session.Tick();
        }

        [Test]
        public void GuidedChain_ScavengeFeedTrain_InOrder()
        {
            Assert.That(Step, Is.EqualTo(TutorialService.Scavenge));
            Assert.That(_tutorial.Current.Cat, Is.EqualTo(TestIds.Ryzhy));

            _session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);
            Assert.That(Step, Is.Null, "waits for the first haul before asking to feed");

            Pass(20_000);
            Assert.That(Step, Is.EqualTo(TutorialService.Feed));

            Assert.That(_session.FeedCat(TestIds.Ryzhy).IsOk, Is.True);
            Assert.That(Step, Is.EqualTo(TutorialService.Train));

            _session.AssignActivity(TestIds.Ryzhy, TestIds.TrainStr);
            Assert.That(Step, Is.Null, "nothing to do until a skill unlocks, the cat tires or grows stronger");
            Assert.That(_session.World.Tutorial.IsDone(TutorialService.Train), Is.True);
        }

        [Test]
        public void TiredCat_AsksToSleep_ThenToFight()
        {
            CompleteFirstSteps();
            Start(WorldWith(TestCats.Make(TestIds.Ryzhy, energy: 10), done: new[] { TutorialService.Scavenge, TutorialService.Feed, TutorialService.Train }));
            Assert.That(Step, Is.EqualTo(TutorialService.Sleep));

            _session.AssignActivity(TestIds.Ryzhy, TestIds.Sleep);
            Assert.That(_session.World.Tutorial.IsDone(TutorialService.Sleep), Is.True);

            Pass(30 * 60_000L); // rested
            Assert.That(Step, Is.EqualTo(TutorialService.Fight));
            Assert.That(_tutorial.Current.Territory, Is.EqualTo(TestIds.Backyard));
        }

        [Test]
        public void UnlockedSkill_AsksToEquip_UntilEquipped()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 4);
            Start(WorldWith(cat, done: new[] { TutorialService.Scavenge, TutorialService.Feed, TutorialService.Train }));

            Assert.That(Step, Is.EqualTo(TutorialService.Equip));
            var skill = _tutorial.Current.Skill.Value;
            Assert.That(_session.EquipSkill(TestIds.Ryzhy, 0, skill).IsOk, Is.True);
            Assert.That(Step, Is.Not.EqualTo(TutorialService.Equip));
        }

        [Test]
        public void Skip_EndsChain_AndIsSaved()
        {
            _tutorial.Skip();

            Assert.That(Step, Is.Null);
            var data = SaveMapper.ToSave(_session.World, 0, new SettingsSave());
            var loaded = SaveMapper.FromSave(data, _config).World;
            Assert.That(loaded.Tutorial.Skipped, Is.True);
        }

        [Test]
        public void CompletedSteps_SurviveSaveAndLoad()
        {
            _session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);
            var loaded = SaveMapper.FromSave(SaveMapper.ToSave(_session.World, 0, new SettingsSave()), _config).World;

            Start(loaded);
            Assert.That(loaded.Tutorial.IsDone(TutorialService.Scavenge), Is.True);
            Assert.That(Step, Is.Not.EqualTo(TutorialService.Scavenge));
        }

        [Test]
        public void JunkHint_FiresOnce()
        {
            _session.AssignActivity(TestIds.Ryzhy, TestIds.Rummage);
            Pass(25_000);
            Pass(25_000);

            Assert.That(_hints.Count(h => h == TutorialService.HintJunk), Is.EqualTo(1));
        }

        [Test]
        public void RelayHint_OnlyWithTwoOrMoreCats_Once()
        {
            _tutorial.OnPrepOpened(1);
            Assert.That(_hints, Is.Empty);
            _tutorial.OnPrepOpened(2);
            _tutorial.OnPrepOpened(3);
            Assert.That(_hints, Is.EqualTo(new[] { TutorialService.HintRelay }));
        }

        [Test]
        public void SaveFromBeforeTheTutorial_WithProgress_SkipsIt()
        {
            Start(TestWorlds.Make(_config, 10, 0, 0, null, TestCats.Make(TestIds.Ryzhy), TestCats.Make(TestIds.Shnyr)));

            Assert.That(_session.World.Tutorial.Skipped, Is.True);
            Assert.That(Step, Is.Null);
        }

        private void CompleteFirstSteps()
        {
            _session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);
            Pass(20_000);
            _session.FeedCat(TestIds.Ryzhy);
            _session.AssignActivity(TestIds.Ryzhy, TestIds.TrainStr);
        }

        private GameWorld WorldWith(Cat cat, string[] done) =>
            new GameWorld(_config, 0, 1, new[] { cat }, new Wallet(20, 0), new UpgradeLevels(), tutorial: new TutorialState(done));
    }
}
