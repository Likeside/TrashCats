using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class SaveTests
    {
        private GameConfig _config;
        private FakeClock _clock;
        private InMemorySaveStorage _storage;
        private SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _clock = new FakeClock();
            _storage = new InMemorySaveStorage();
            _service = new SaveService(_storage, _config, _clock, indented: true);
        }

        private GameSession Session(GameWorld world, long lastTick) => new GameSession(world, _clock, new EventBus(), NoSaveRequests.Instance, lastTick);

        private GameWorld BusyWorld()
        {
            var cats = new[] { TestIds.Ryzhy, TestIds.Shnyr, TestIds.Ponchik }.Select(id => CatFactory.Create(_config.GetCat(id), _config)).ToList();
            var world = new GameWorld(_config, 0, 99, cats, new Wallet(7, 33), new UpgradeLevels(new[] { TestWorlds.Level(TestIds.Can, 2), TestWorlds.Level(TestIds.Boxes, 1) }));
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, cats[0], TestIds.TrainStr, events);
            ActivitySystem.Start(world, cats[1], TestIds.Rummage, events);
            Simulation.AdvanceTo(world, 47_500, events);
            CatRules.Equip(cats[2], 2, null, _config, events);
            return world;
        }

        [Test]
        public void RoundTrip_RestoresIdenticalWorld()
        {
            var world = BusyWorld();
            var session = Session(world, _clock.UtcNowMs);

            _service.Save(session, new SettingsSave { BattleSpeed = 2 });
            var loaded = _service.LoadOrCreate();

            Assert.That(loaded.Source, Is.EqualTo(SaveSource.Main));
            Assert.That(loaded.Problems, Is.Empty);
            Assert.That(loaded.Game.Warnings, Is.Empty);
            Assert.That(WorldFingerprint.Of(loaded.Game.World), Is.EqualTo(WorldFingerprint.Of(world)));
            Assert.That(loaded.Game.Settings.BattleSpeed, Is.EqualTo(2));
            Assert.That(loaded.Game.LastTickUtcMs, Is.EqualTo(_clock.UtcNowMs));
        }

        [Test]
        public void SaveMidCycle_LoadAndContinue_EqualsNoRestart()
        {
            var reference = BusyWorld();
            var restarted = BusyWorld();
            _service.Save(Session(restarted, _clock.UtcNowMs), null);

            var loaded = _service.LoadOrCreate().Game.World;
            Simulation.AdvanceBy(reference, 10 * 60_000, new List<DomainEvent>());
            Simulation.AdvanceBy(loaded, 10 * 60_000, new List<DomainEvent>());

            Assert.That(WorldFingerprint.Of(loaded), Is.EqualTo(WorldFingerprint.Of(reference)));
        }

        [Test]
        public void TimeElapsedBeforeSave_IsNotLost()
        {
            var world = GameWorld.CreateNew(_config, 1);
            var session = Session(world, _clock.UtcNowMs);
            session.AssignActivity(TestIds.Ryzhy, TestIds.Scavenge);

            _clock.Advance(20_000);
            _service.Save(session, null);

            Assert.That(_service.LoadOrCreate().Game.World.Food, Is.EqualTo(13));
        }

        [Test]
        public void CorruptMain_FallsBackToBackup_BothCorrupt_StartsNewGame()
        {
            _service.Save(Session(BusyWorld(), _clock.UtcNowMs), null);
            _service.Save(Session(BusyWorld(), _clock.UtcNowMs), null);
            _storage.Main = "{ this is not json";

            var fromBackup = _service.LoadOrCreate();
            Assert.That(fromBackup.Source, Is.EqualTo(SaveSource.Backup));
            Assert.That(fromBackup.Problems.Single(), Does.StartWith("main:"));

            _storage.Backup = "[]";
            var fresh = _service.LoadOrCreate();
            Assert.That(fresh.Source, Is.EqualTo(SaveSource.NewGame));
            Assert.That(fresh.Game.World.Cats.Select(c => c.Id), Is.EqualTo(new[] { TestIds.Ryzhy }));
        }

        [Test]
        public void NoSave_StartsNewGame()
        {
            var outcome = _service.LoadOrCreate();

            Assert.That(outcome.Source, Is.EqualTo(SaveSource.NewGame));
            Assert.That(outcome.Problems, Is.Empty);
            Assert.That(outcome.Game.LastTickUtcMs, Is.EqualTo(_clock.UtcNowMs));
        }

        [Test]
        public void FutureVersion_IsSetAsideAndNotLoaded()
        {
            _storage.Main = "{\"Version\": 99, \"Cats\": []}";

            var outcome = _service.LoadOrCreate();

            Assert.That(outcome.Source, Is.EqualTo(SaveSource.NewGame));
            Assert.That(_storage.Aside.Keys, Is.EquivalentTo(new[] { "save.future.v99.json" }));
        }

        [Test]
        public void UnknownIdsAndOutOfRangeValues_AreRepairedWithWarnings()
        {
            var data = SaveMapper.ToSave(BusyWorld(), 0, null);
            data.Cats.Add(new CatSave { Id = "cat_removed_in_new_build" });
            data.Cats[0].Energy = 250;
            data.Cats[0].Skills = new[] { "skill_unknown", null, null };
            data.Cats[1].Activity.ActivityId = "act_removed";
            data.Upgrades["upg_removed"] = 1;
            data.Upgrades["upg_can"] = 9;

            var game = SaveMapper.FromSave(data, _config);

            Assert.That(game.World.Cats.Count, Is.EqualTo(3));
            Assert.That(game.World.Cats[0].Energy, Is.EqualTo(100));
            Assert.That(game.World.Cats[1].Activity.Status, Is.EqualTo(ActivityStatus.Idle));
            Assert.That(game.World.GetUpgradeLevel(TestIds.Can), Is.EqualTo(2));
            Assert.That(game.Warnings.Count, Is.EqualTo(6), string.Join("\n", game.Warnings));
            Assert.That(WorldInvariants.Check(game.World), Is.Empty);
        }

        [Test]
        public void TwoCatsOnOneStation_SecondIsSetIdle()
        {
            var data = SaveMapper.ToSave(BusyWorld(), 0, null);
            data.Cats[1].Activity = new ActivitySave
            {
                State = "Working", ActivityId = "act_train_str",
                CycleStartMs = data.GameTimeMs - 1000, CycleDurationMs = 30_000,
            };

            var game = SaveMapper.FromSave(data, _config);

            Assert.That(game.World.Cats[0].Activity.IsWorking, Is.True);
            Assert.That(game.World.Cats[1].Activity.IsWorking, Is.False);
            Assert.That(game.Warnings.Any(w => w.Contains("already taken")), Is.True);
        }

        [Test]
        public void SaveWithoutKnownCats_IsCorrupted()
        {
            var data = new SaveData { Cats = new List<CatSave> { new CatSave { Id = "cat_nobody" } } };

            Assert.Throws<SaveCorruptedException>(() => SaveMapper.FromSave(data, _config));
        }

        [Test]
        public void Scheduler_SavesImmediatelyOrThrottled()
        {
            var session = Session(GameWorld.CreateNew(_config, 1), _clock.UtcNowMs);
            var scheduler = new SaveScheduler(_service, _clock, throttleMs: 5_000);
            scheduler.Attach(session, () => new SettingsSave());

            scheduler.MarkDirty(SaveUrgency.Deferred);
            scheduler.Update();
            Assert.That(_storage.Writes, Is.EqualTo(0));

            _clock.Advance(5_000);
            scheduler.Update();
            Assert.That(_storage.Writes, Is.EqualTo(1));
            scheduler.Update();
            Assert.That(_storage.Writes, Is.EqualTo(1), "nothing new to save");

            scheduler.MarkDirty(SaveUrgency.Immediate);
            Assert.That(_storage.Writes, Is.EqualTo(2));
            Assert.That(scheduler.IsDirty, Is.False);
        }
    }
}
