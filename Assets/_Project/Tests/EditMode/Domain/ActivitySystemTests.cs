using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class ActivitySystemTests
    {
        private GameConfig _config;
        private List<DomainEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _events = new List<DomainEvent>();
        }

        // --- CanStart -------------------------------------------------------------------------

        [Test]
        public void CanStart_ReportsEveryError()
        {
            var tired = TestCats.Make(TestIds.Ryzhy, energy: 19);
            var full = TestCats.Make(TestIds.Shnyr, energy: 100);
            var fighter = TestCats.Make(TestIds.Ponchik, activity: ActivityState.InBattle);
            var world = TestWorlds.Make(_config, cats: new[] { tired, full, fighter });

            Assert.That(ActivitySystem.CanStart(world, full, new ActivityId("act_unknown")), Is.EqualTo(ActivityError.UnknownActivity));
            Assert.That(ActivitySystem.CanStart(world, fighter, TestIds.Scavenge), Is.EqualTo(ActivityError.InBattle));
            Assert.That(ActivitySystem.CanStart(world, tired, TestIds.Scavenge), Is.EqualTo(ActivityError.Tired));
            Assert.That(ActivitySystem.CanStart(world, tired, TestIds.TrainStr), Is.EqualTo(ActivityError.Tired));
            Assert.That(ActivitySystem.CanStart(world, full, TestIds.Sleep), Is.EqualTo(ActivityError.EnergyFull));
        }

        [Test]
        public void CanStart_SleepIsAllowedWhenTired_AndTrainingAtMaxIsNot()
        {
            var tired = TestCats.Make(TestIds.Ryzhy, energy: 5);
            var maxed = TestCats.Make(TestIds.Shnyr, str: 10);
            var world = TestWorlds.Make(_config, cats: new[] { tired, maxed });

            Assert.That(ActivitySystem.CanStart(world, tired, TestIds.Sleep), Is.EqualTo(ActivityError.None));
            Assert.That(ActivitySystem.CanStart(world, maxed, TestIds.TrainStr), Is.EqualTo(ActivityError.AttributeMaxed));
            Assert.That(ActivitySystem.CanStart(world, maxed, TestIds.TrainAgi), Is.EqualTo(ActivityError.None));
        }

        [Test]
        public void EveryStation_HoldsOneCat()
        {
            var a = TestCats.Make(TestIds.Ryzhy, energy: 50);
            var b = TestCats.Make(TestIds.Shnyr, energy: 50);
            var world = TestWorlds.Make(_config, cats: new[] { a, b });

            ActivitySystem.Start(world, a, TestIds.TrainStr, _events);
            ActivitySystem.Start(world, a, TestIds.TrainStr, _events); // restarting own station is fine

            Assert.That(ActivitySystem.CanStart(world, b, TestIds.TrainStr), Is.EqualTo(ActivityError.StationBusy));
            Assert.That(ActivitySystem.CanStart(world, b, TestIds.TrainAgi), Is.EqualTo(ActivityError.None));

            // the street, the dumpster and the bed too (two cats did sleep on one bed)
            foreach (var activity in new[] { TestIds.Scavenge, TestIds.Rummage, TestIds.Sleep })
            {
                ActivitySystem.Start(world, a, activity, _events);
                Assert.That(ActivitySystem.CanStart(world, b, activity), Is.EqualTo(ActivityError.StationBusy), activity.Value);
            }
            Assert.That(ActivitySystem.CanStart(world, b, TestIds.TrainStr), Is.EqualTo(ActivityError.None));
        }

        [Test]
        public void Start_WithError_Throws()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 0);
            var world = TestWorlds.Make(_config, cats: cat);

            Assert.Throws<InvalidOperationException>(() => ActivitySystem.Start(world, cat, TestIds.TrainStr, _events));
            Assert.That(cat.Activity.Status, Is.EqualTo(ActivityStatus.Idle));
        }

        // --- Start & duration -----------------------------------------------------------------

        [Test]
        public void Start_FixesDurationAtCycleStart_HungerDoublesAffectedActivities()
        {
            var fed = TestCats.Make(TestIds.Ryzhy, satiety: 50);
            var hungry = TestCats.Make(TestIds.Shnyr, satiety: 19, energy: 50);
            var world = TestWorlds.Make(_config, timeMs: 1000, cats: new[] { fed, hungry });

            ActivitySystem.Start(world, fed, TestIds.Scavenge, _events);
            ActivitySystem.Start(world, hungry, TestIds.TrainAgi, _events);

            Assert.That((fed.Activity.CycleStartMs, fed.Activity.CycleDurationMs), Is.EqualTo((1000L, 20_000L)));
            Assert.That(hungry.Activity.CycleDurationMs, Is.EqualTo(60_000L));
            Assert.That(hungry.Activity.HungryOverride, Is.True);
            Assert.That(fed.Activity.HungryOverride, Is.False);
            Assert.That(_events.OfType<ActivityStarted>().All(e => !e.IsRepeat), Is.True);
        }

        [Test]
        public void Sleep_IsNotSlowedByHunger()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 10, satiety: 0);
            var world = TestWorlds.Make(_config, cats: cat);

            ActivitySystem.Start(world, cat, TestIds.Sleep, _events);

            Assert.That(cat.Activity.CycleDurationMs, Is.EqualTo(15_000L));
            Assert.That(cat.Activity.HungryOverride, Is.False);
        }

        [Test]
        public void FeedingMidCycle_DoesNotShortenCurrentCycle()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, satiety: 10);
            var world = TestWorlds.Make(_config, food: 10, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.Scavenge, _events);

            Simulation.AdvanceTo(world, 10_000, _events);
            Feeding.Feed(cat, world.Wallet, _config, _events);

            Assert.That(cat.Activity.CycleDurationMs, Is.EqualTo(40_000L));
            Simulation.AdvanceTo(world, 39_999, _events);
            Assert.That(_events.OfType<ActivityCycleCompleted>(), Is.Empty);
        }

        // --- Completion ----------------------------------------------------------------------

        [Test]
        public void Scavenge_PaysCostsAddsFood_AndRepeatsAtCompletionTime()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 100, satiety: 60);
            var world = TestWorlds.Make(_config, food: 2, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.Scavenge, _events);
            _events.Clear();

            Simulation.AdvanceTo(world, 25_000, _events);

            Assert.That((cat.Energy, cat.Satiety, world.Wallet.Food), Is.EqualTo((90, 56, 5)));
            Assert.That(_events.Single<ActivityCycleCompleted>().Output, Is.EqualTo(3));
            var repeat = _events.Single<ActivityStarted>();
            Assert.That((repeat.CycleStartMs, repeat.IsRepeat), Is.EqualTo((20_000L, true)));
            Assert.That(cat.Activity.CycleStartMs, Is.EqualTo(20_000L));
        }

        [Test]
        public void Rummage_AddsJunkWithDumpsterBonus()
        {
            var cat = TestCats.Make(TestIds.Ryzhy);
            var world = TestWorlds.Make(_config, upgrades: new[] { TestWorlds.Level(TestIds.Dumpster, 2) }, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.Rummage, _events);

            Simulation.AdvanceTo(world, 25_000, _events);

            Assert.That(world.Wallet.Junk, Is.EqualTo(5));
        }

        [TestCase(AttributeType.Strength, 0, 15)]   // aptitude +50%
        [TestCase(AttributeType.Strength, 2, 19)]   // aptitude +50%, bench (Wheel) lvl 2 +40%
        [TestCase(AttributeType.Technique, 0, 10)]
        public void Training_GrantsXpWithAptitudeAndStationBonus(AttributeType attribute, int stationLevel, int expectedXp)
        {
            var cat = TestCats.Make(TestIds.Ryzhy, aptitude: AttributeType.Strength);
            var station = attribute == AttributeType.Strength ? TestIds.Wheel : TestIds.Can;
            var activity = attribute == AttributeType.Strength ? TestIds.TrainStr : TestIds.TrainAgi;
            var world = TestWorlds.Make(_config, upgrades: new[] { TestWorlds.Level(station, stationLevel) }, cats: cat);
            ActivitySystem.Start(world, cat, activity, _events);

            Simulation.AdvanceTo(world, 30_000, _events);

            Assert.That(_events.Single<ActivityCycleCompleted>().Output, Is.EqualTo(expectedXp));
            Assert.That((cat.Energy, cat.Satiety), Is.EqualTo((85, 86)));
        }

        [Test]
        public void UpgradeBoughtMidCycle_AppliesToCurrentCycle()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, aptitude: AttributeType.Technique);
            var world = TestWorlds.Make(_config, junk: 20, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.TrainStr, _events);
            Simulation.AdvanceTo(world, 10_000, _events);

            Hideout.Buy(world.Upgrades, world.Wallet, _config, TestIds.Wheel, _events);
            Simulation.AdvanceTo(world, 30_000, _events);

            Assert.That(_events.Single<ActivityCycleCompleted>().Output, Is.EqualTo(12));
        }

        [Test]
        public void Sleep_RestoresEnergyWithBoxes_AndStopsWhenFull()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 40);
            var world = TestWorlds.Make(_config, upgrades: new[] { TestWorlds.Level(TestIds.Boxes, 1) }, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.Sleep, _events);

            Simulation.AdvanceTo(world, 15_000, _events);
            Assert.That(cat.Energy, Is.EqualTo(75));
            Assert.That(cat.Activity.IsWorking, Is.True);

            Simulation.AdvanceTo(world, 30_000, _events);
            Assert.That(cat.Energy, Is.EqualTo(100));
            Assert.That(cat.Activity.IdleReason, Is.EqualTo(IdleReason.SleepDone));
            Assert.That(_events.Single<ActivityStopped>().Reason, Is.EqualTo(IdleReason.SleepDone));
        }

        [Test]
        public void Training_StopsWhenTired()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 35);
            var world = TestWorlds.Make(_config, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.TrainStr, _events);

            Simulation.AdvanceTo(world, 90_000, _events);

            Assert.That(cat.Energy, Is.EqualTo(5));
            Assert.That(cat.Activity.IdleReason, Is.EqualTo(IdleReason.Tired));
            Assert.That(_events.OfType<ActivityCycleCompleted>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Training_StopsWhenHungry_UnlessStartedHungryByPlayer()
        {
            var fed = TestCats.Make(TestIds.Ryzhy, satiety: 25);
            var world = TestWorlds.Make(_config, cats: fed);
            ActivitySystem.Start(world, fed, TestIds.TrainStr, _events);
            Simulation.AdvanceTo(world, 30_000, _events);
            Assert.That(fed.Activity.IdleReason, Is.EqualTo(IdleReason.Hungry));

            ActivitySystem.Start(world, fed, TestIds.TrainStr, _events);
            Simulation.AdvanceTo(world, 30_000 + 60_000 * 3, _events);
            Assert.That(fed.Activity.IsWorking, Is.True, "manual hungry start keeps repeating while energy lasts");
            Assert.That(fed.Satiety, Is.EqualTo(0));
            Assert.That(fed.Activity.CycleDurationMs, Is.EqualTo(60_000L));
        }

        [Test]
        public void Training_StopsAtMaxLevel()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 9, aptitude: AttributeType.Strength);
            var world = TestWorlds.Make(_config, upgrades: new[] { TestWorlds.Level(TestIds.Wheel, 2) }, cats: cat);
            ActivitySystem.Start(world, cat, TestIds.TrainStr, _events);

            Simulation.AdvanceTo(world, 30_000 * 6, _events); // 106 XP needed, 19 per cycle

            Assert.That(cat.GetLevel(AttributeType.Strength), Is.EqualTo(10));
            Assert.That(cat.Activity.IdleReason, Is.EqualTo(IdleReason.AttributeMax));
        }

        [Test]
        public void Stop_CancelsWithoutCostOrReward_AndFreesStation()
        {
            var a = TestCats.Make(TestIds.Ryzhy);
            var b = TestCats.Make(TestIds.Shnyr);
            var world = TestWorlds.Make(_config, cats: new[] { a, b });
            ActivitySystem.Start(world, a, TestIds.TrainStr, _events);
            Simulation.AdvanceTo(world, 29_999, _events);

            ActivitySystem.Stop(a, IdleReason.Stopped, _events);
            Simulation.AdvanceTo(world, 60_000, _events);

            Assert.That((a.Energy, a.Satiety, a.GetXp(AttributeType.Strength)), Is.EqualTo((100, 100, 0)));
            Assert.That(_events.Single<ActivityStopped>().Reason, Is.EqualTo(IdleReason.Stopped));
            Assert.That(ActivitySystem.CanStart(world, b, TestIds.TrainStr), Is.EqualTo(ActivityError.None));
        }

        [Test]
        public void GetCycleProgress_IsClampedFraction()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, activity: ActivityState.Working(TestIds.Scavenge, 1000, 20_000, false));

            Assert.That(ActivitySystem.GetCycleProgress(cat, 0), Is.EqualTo(0f));
            Assert.That(ActivitySystem.GetCycleProgress(cat, 11_000), Is.EqualTo(0.5f));
            Assert.That(ActivitySystem.GetCycleProgress(cat, 50_000), Is.EqualTo(1f));
            Assert.That(ActivitySystem.GetCycleProgress(TestCats.Make(TestIds.Shnyr), 5000), Is.EqualTo(0f));
        }
    }
}
