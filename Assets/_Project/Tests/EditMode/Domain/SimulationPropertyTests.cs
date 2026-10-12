using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>Randomised checks of the time model (roadmap M1-12).</summary>
    public sealed class SimulationPropertyTests
    {
        private const long Minute = 60_000;

        private GameConfig _config;

        [SetUp]
        public void SetUp() => _config = TestConfigs.Default();

        [Test]
        public void OneHourInOneStep_EqualsOneHourIn100MsSteps()
        {
            var big = ThreeBusyCats();
            var small = ThreeBusyCats();

            Simulation.AdvanceTo(big, 60 * Minute, new List<DomainEvent>());
            var events = new List<DomainEvent>();
            for (long t = 100; t <= 60 * Minute; t += 100)
                Simulation.AdvanceTo(small, t, events);

            Assert.That(WorldFingerprint.Of(small), Is.EqualTo(WorldFingerprint.Of(big)));
            Assert.That(WorldInvariants.Check(big), Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RandomPlay_StepSizeDoesNotChangeResult_AndInvariantsHold(int seed)
        {
            // The same random player actions are replayed against two worlds: one jumps straight to each action time,
            // the other walks there in small irregular steps. States must match after every action.
            var actions = RandomActions(seed, 300, 90 * Minute);
            var jumping = WorldWithAllCats();
            var walking = WorldWithAllCats();
            var walkRng = new DeterministicRng(seed * 31);
            var events = new List<DomainEvent>();

            foreach (var action in actions)
            {
                Simulation.AdvanceTo(jumping, action.TimeMs, events);
                while (walking.GameTimeMs < action.TimeMs)
                    Simulation.AdvanceTo(walking, System.Math.Min(action.TimeMs, walking.GameTimeMs + walkRng.NextInt(1, 7_000)), events);

                action.Apply(jumping, _config);
                action.Apply(walking, _config);

                Assert.That(WorldFingerprint.Of(walking), Is.EqualTo(WorldFingerprint.Of(jumping)), $"seed {seed}, t={action.TimeMs}");
                Assert.That(WorldInvariants.Check(jumping), Is.Empty, $"seed {seed}, t={action.TimeMs}");
            }
        }

        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        public void NoDeadlock_WithoutFood_GangCanAlwaysGetFoodAgain(int seed)
        {
            var rng = new DeterministicRng(seed);
            var cats = new[] { TestIds.Ryzhy, TestIds.Shnyr, TestIds.Ponchik }
                .Select(id => TestCats.Make(id, energy: rng.NextInt(0, 101), satiety: rng.NextInt(0, 101)))
                .ToArray();
            var world = TestWorlds.Make(_config, food: 0, cats: cats);
            var events = new List<DomainEvent>();

            // Naive player: whoever is idle scavenges if possible, otherwise sleeps.
            for (var step = 0; step < 20 && world.Food == 0; step++)
            {
                foreach (var cat in world.Cats.Where(c => !c.Activity.IsWorking))
                {
                    var activity = ActivitySystem.CanStart(world, cat, TestIds.Scavenge) == ActivityError.None ? TestIds.Scavenge : TestIds.Sleep;
                    Assert.That(ActivitySystem.CanStart(world, cat, activity), Is.EqualTo(ActivityError.None),
                        $"{cat.Id} (E{cat.Energy} S{cat.Satiety}) can neither scavenge nor sleep");
                    ActivitySystem.Start(world, cat, activity, events);
                }

                Simulation.AdvanceBy(world, 30_000, events);
            }

            Assert.That(world.Food, Is.GreaterThan(0));
        }

        private GameWorld ThreeBusyCats()
        {
            var world = WorldWithAllCats();
            var events = new List<DomainEvent>();
            ActivitySystem.Start(world, world.GetCat(TestIds.Ryzhy), TestIds.TrainStr, events);
            ActivitySystem.Start(world, world.GetCat(TestIds.Shnyr), TestIds.Rummage, events);
            ActivitySystem.Start(world, world.GetCat(TestIds.Ponchik), TestIds.Scavenge, events);
            return world;
        }

        private GameWorld WorldWithAllCats()
        {
            var cats = new[] { TestIds.Ryzhy, TestIds.Shnyr, TestIds.Ponchik }.Select(id => CatFactory.Create(_config.GetCat(id), _config));
            return new GameWorld(_config, 0, 7, cats, new Wallet(10, 0), new UpgradeLevels());
        }

        private List<PlayerAction> RandomActions(int seed, int count, long duration)
        {
            var rng = new DeterministicRng(seed);
            var cats = new[] { TestIds.Ryzhy, TestIds.Shnyr, TestIds.Ponchik };
            var activities = _config.Activities.Select(a => a.Id).ToArray();
            var upgrades = _config.Upgrades.Select(u => u.Id).ToArray();
            var times = Enumerable.Range(0, count).Select(_ => (long)rng.NextInt(0, (int)duration)).OrderBy(t => t).ToArray();

            return times.Select(t => new PlayerAction
            {
                TimeMs = t,
                Kind = rng.NextInt(0, 10),
                Cat = cats[rng.NextInt(0, cats.Length)],
                Activity = activities[rng.NextInt(0, activities.Length)],
                Upgrade = upgrades[rng.NextInt(0, upgrades.Length)],
            }).ToList();
        }

        private sealed class PlayerAction
        {
            public long TimeMs;
            public int Kind;
            public CatId Cat;
            public ActivityId Activity;
            public UpgradeId Upgrade;

            public void Apply(GameWorld world, GameConfig config)
            {
                var events = new List<DomainEvent>();
                var cat = world.GetCat(Cat);
                switch (Kind)
                {
                    case 0:
                    case 1:
                        if (Feeding.CanFeed(cat, world.Wallet, config) == FeedError.None)
                            Feeding.Feed(cat, world.Wallet, config, events);
                        break;
                    case 2:
                        ActivitySystem.Stop(cat, IdleReason.Stopped, events);
                        break;
                    case 3:
                        world.Wallet.Add(ResourceType.Junk, 10, events);
                        if (Hideout.CanBuy(world.Upgrades, world.Wallet, config, Upgrade) == UpgradeError.None)
                            Hideout.Buy(world.Upgrades, world.Wallet, config, Upgrade, events);
                        break;
                    default:
                        if (ActivitySystem.CanStart(world, cat, Activity) == ActivityError.None)
                            ActivitySystem.Start(world, cat, Activity, events);
                        break;
                }
            }
        }
    }
}
