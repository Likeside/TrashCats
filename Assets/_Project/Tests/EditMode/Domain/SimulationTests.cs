using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class SimulationTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp() => _config = TestConfigs.Default();

        [Test]
        public void CreateNew_StartsWithConfigCatsAndResources()
        {
            var world = GameWorld.CreateNew(_config, 42);

            Assert.That(world.Cats.Select(c => c.Id), Is.EqualTo(new[] { TestIds.Ryzhy }));
            Assert.That((world.Food, world.Junk, world.GameTimeMs, world.SaveSeed), Is.EqualTo((10, 0, 0L, 42)));
            Assert.That(WorldInvariants.Check(world), Is.Empty);
        }

        [Test]
        public void AdvanceTo_CompletesCyclesInChronologicalOrderAcrossCats()
        {
            var a = TestCats.Make(TestIds.Ryzhy);
            var b = TestCats.Make(TestIds.Shnyr);
            var c = TestCats.Make(TestIds.Ponchik, energy: 50);
            var world = TestWorlds.Make(_config, cats: new[] { a, b, c });
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, a, TestIds.TrainStr, events);  // 30s
            ActivitySystem.Start(world, b, TestIds.Scavenge, events);  // 20s
            ActivitySystem.Start(world, c, TestIds.Sleep, events);     // 15s
            events.Clear();

            Simulation.AdvanceTo(world, 45_000, events);

            var completions = events.OfType<ActivityCycleCompleted>().Select(e => e.CatId).ToArray();
            // 15s Ponchik, 20s Shnyr, 30s Ryzhy and Ponchik (tie -> gang order), 40s Shnyr. Ponchik is then full.
            Assert.That(completions, Is.EqualTo(new[] { TestIds.Ponchik, TestIds.Shnyr, TestIds.Ryzhy, TestIds.Ponchik, TestIds.Shnyr }));
            Assert.That(c.Activity.IdleReason, Is.EqualTo(IdleReason.SleepDone));
            Assert.That(world.GameTimeMs, Is.EqualTo(45_000L));
        }

        [Test]
        public void SimultaneousCompletions_FollowGangOrder()
        {
            var a = TestCats.Make(TestIds.Ryzhy);
            var b = TestCats.Make(TestIds.Shnyr);
            var world = TestWorlds.Make(_config, cats: new[] { a, b });
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, b, TestIds.Scavenge, events);
            ActivitySystem.Start(world, a, TestIds.Scavenge, events);
            events.Clear();

            Simulation.AdvanceTo(world, 20_000, events);

            Assert.That(events.OfType<ActivityCycleCompleted>().Select(e => e.CatId), Is.EqualTo(new[] { TestIds.Ryzhy, TestIds.Shnyr }));
        }

        [Test]
        public void CycleEndingExactlyAtTarget_IsCompleted()
        {
            var cat = TestCats.Make(TestIds.Ryzhy);
            var world = TestWorlds.Make(_config, cats: cat);
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, cat, TestIds.Scavenge, events);

            Simulation.AdvanceTo(world, 19_999, events);
            Assert.That(world.Food, Is.EqualTo(0));
            Simulation.AdvanceTo(world, 20_000, events);
            Assert.That(world.Food, Is.EqualTo(3));
        }

        [Test]
        public void MovingTimeBackwards_Throws()
        {
            var world = TestWorlds.Make(_config, timeMs: 1000, cats: TestCats.Make(TestIds.Ryzhy));

            Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.AdvanceTo(world, 999, new List<DomainEvent>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.AdvanceBy(world, -1, new List<DomainEvent>()));
        }

        [Test]
        public void StationFreedByOneCat_CanBeTakenByAnotherDuringSimulation()
        {
            var a = TestCats.Make(TestIds.Ryzhy, energy: 20);
            var b = TestCats.Make(TestIds.Shnyr);
            var world = TestWorlds.Make(_config, cats: new[] { a, b });
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, a, TestIds.TrainStr, events);
            Assert.That(ActivitySystem.CanStart(world, b, TestIds.TrainStr), Is.EqualTo(ActivityError.StationBusy));

            Simulation.AdvanceTo(world, 30_000, events);

            Assert.That(a.Activity.IdleReason, Is.EqualTo(IdleReason.Tired));
            ActivitySystem.Start(world, b, TestIds.TrainStr, events);
            Assert.That(WorldInvariants.Check(world), Is.Empty);
        }

        [Test]
        public void Invariants_DetectBrokenState()
        {
            var a = TestCats.Make(TestIds.Ryzhy, activity: ActivityState.Working(TestIds.TrainStr, 0, 30_000, false));
            var b = TestCats.Make(TestIds.Shnyr, activity: ActivityState.Working(TestIds.TrainStr, 0, 30_000, false));
            var world = TestWorlds.Make(_config, timeMs: 1000, cats: new[] { a, b });

            var errors = WorldInvariants.Check(world);

            Assert.That(errors.Any(e => e.Contains("capacity 1")), Is.True, string.Join("\n", errors));
        }
    }
}
