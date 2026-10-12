using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class BattlesTests
    {
        private GameConfig _config;
        private List<DomainEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _events = new List<DomainEvent>();
        }

        private GameWorld World(params Cat[] cats) => new GameWorld(_config, 0, 7, cats, new Wallet(0, 0), new UpgradeLevels());

        private static Cat Strong(CatId id) => TestCats.Make(id, str: 10, agi: 10, tou: 10);

        [Test]
        public void NewWorld_FirstTerritoryAvailable_OthersLocked()
        {
            var world = World(Strong(TestIds.Ryzhy));
            Assert.That(world.Territories.Get(TestIds.Backyard).Status, Is.EqualTo(TerritoryStatus.Available));
            Assert.That(world.Territories.Get(TestIds.FishShop).Status, Is.EqualTo(TerritoryStatus.Locked));
        }

        [Test]
        public void CanStart_ReportsErrors()
        {
            var tired = TestCats.Make(TestIds.Shnyr, energy: 19);
            var world = World(Strong(TestIds.Ryzhy), tired);

            Assert.That(Battles.CanStart(world, new TerritoryId("ter_x"), new[] { TestIds.Ryzhy }), Is.EqualTo(BattleError.UnknownTerritory));
            Assert.That(Battles.CanStart(world, TestIds.FishShop, new[] { TestIds.Ryzhy }), Is.EqualTo(BattleError.TerritoryLocked));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new CatId[0]), Is.EqualTo(BattleError.InvalidTeam));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new[] { TestIds.Ryzhy, TestIds.Ryzhy }), Is.EqualTo(BattleError.InvalidTeam));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new[] { TestIds.Ponchik }), Is.EqualTo(BattleError.InvalidTeam));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new[] { TestIds.Shnyr }), Is.EqualTo(BattleError.CatTooTired));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new[] { TestIds.Ryzhy }), Is.EqualTo(BattleError.None));
        }

        [Test]
        public void Start_CancelsActivities_FixesResult_AndBlocksSecondBattle()
        {
            var cat = Strong(TestIds.Ryzhy);
            var world = World(cat);
            ActivitySystem.Start(world, cat, TestIds.Scavenge, _events);

            var battle = Battles.Start(world, TestIds.Backyard, new[] { TestIds.Ryzhy }, _events);

            Assert.That(cat.Activity.Status, Is.EqualTo(ActivityStatus.InBattle));
            Assert.That(world.PendingBattle, Is.SameAs(battle));
            Assert.That(world.Territories.Get(TestIds.Backyard).Attempts, Is.EqualTo(1));
            Assert.That(Battles.CanStart(world, TestIds.Backyard, new[] { TestIds.Ryzhy }), Is.EqualTo(BattleError.BattlePending));
            Assert.That(WorldInvariants.Check(world), Is.Empty);
            // time passes without touching the cat in battle
            Simulation.AdvanceBy(world, 120_000, _events);
            Assert.That(cat.Activity.Status, Is.EqualTo(ActivityStatus.InBattle));
        }

        [Test]
        public void FirstVictory_RewardsCapturesUnlocksAndRecruits()
        {
            var cat = Strong(TestIds.Ryzhy);
            var world = World(cat);
            Battles.Start(world, TestIds.Backyard, new[] { TestIds.Ryzhy }, _events);
            Assert.That(world.PendingBattle.Result.Outcome, Is.EqualTo(BattleOutcome.Victory));

            Battles.Complete(world, _events);

            Assert.That((world.Food, world.Junk), Is.EqualTo((10, 10)));
            Assert.That(world.Territories.Get(TestIds.Backyard).Status, Is.EqualTo(TerritoryStatus.Captured));
            Assert.That(world.Territories.Get(TestIds.FishShop).Status, Is.EqualTo(TerritoryStatus.Available));
            Assert.That(world.Cats.Select(c => c.Id), Is.EqualTo(new[] { TestIds.Ryzhy, TestIds.Shnyr }));
            Assert.That(cat.Energy, Is.EqualTo(70));
            Assert.That(cat.Satiety, Is.EqualTo(90));
            Assert.That(cat.Activity.IdleReason, Is.EqualTo(IdleReason.AfterBattle));
            Assert.That(world.PendingBattle, Is.Null);
            Assert.That(_events.OfType<CatRecruited>().Single().CatId, Is.EqualTo(TestIds.Shnyr));
            Assert.That(_events.OfType<BattleCompleted>().Single().FirstVictory, Is.True);
            Assert.That(WorldInvariants.Check(world), Is.Empty);
        }

        [Test]
        public void RepeatVictory_GivesThirtyPercentRoundedUp_NoSecondRecruit()
        {
            var world = World(Strong(TestIds.Ryzhy));
            Battles.Start(world, TestIds.Backyard, new[] { TestIds.Ryzhy }, _events);
            Battles.Complete(world, _events);
            world.GetCat(TestIds.Ryzhy).SetNeeds(100, 100);

            Battles.Start(world, TestIds.Backyard, new[] { TestIds.Ryzhy }, _events);
            _events.Clear();
            Battles.Complete(world, _events);

            Assert.That((world.Food, world.Junk), Is.EqualTo((13, 13)));
            Assert.That(world.Cats.Count, Is.EqualTo(2));
            Assert.That(_events.OfType<BattleCompleted>().Single().FirstVictory, Is.False);
        }

        [Test]
        public void Defeat_DrainsEnergyOfFightersOnly_GivesSmallXp_NoReward()
        {
            var weak = TestCats.Make(TestIds.Ryzhy, str: 1, agi: 1, tou: 1);
            var world = World(weak);
            world.Territories.Get(TestIds.Rooftop).Status = TerritoryStatus.Available;

            Battles.Start(world, TestIds.Rooftop, new[] { TestIds.Ryzhy }, _events);
            Assert.That(world.PendingBattle.Result.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Battles.Complete(world, _events);

            Assert.That(weak.Energy, Is.EqualTo(0));
            Assert.That(weak.GetXp(AttributeType.Strength), Is.EqualTo(3));
            Assert.That((world.Food, world.Junk), Is.EqualTo((0, 0)));
            Assert.That(world.Territories.Get(TestIds.Rooftop).Status, Is.EqualTo(TerritoryStatus.Available));
        }

        [Test]
        public void CapturedTerritoryBonus_AffectsActivities()
        {
            var world = World(Strong(TestIds.Ryzhy));
            world.Territories.Get(TestIds.FishShop).Status = TerritoryStatus.Captured;

            var modifiers = world.CollectModifiers();

            Assert.That(modifiers.FoodOutputPct, Is.EqualTo(25));
            Assert.That(ActivitySystem.PreviewOutput(world, world.Cats[0], _config.GetActivity(TestIds.Scavenge)), Is.EqualTo(4));
        }

        [Test]
        public void FinalVictory_SetsFinalFlag()
        {
            var world = World(Strong(TestIds.Ryzhy), Strong(TestIds.Shnyr), Strong(TestIds.Ponchik));
            world.Territories.Get(TestIds.Rooftop).Status = TerritoryStatus.Available;

            Battles.Start(world, TestIds.Rooftop, new[] { TestIds.Ryzhy, TestIds.Shnyr, TestIds.Ponchik }, _events);
            Battles.Complete(world, _events);

            Assert.That(world.FinalReached, Is.True);
            Assert.That(_events.OfType<FinalReached>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Save_WithPendingBattle_RestoresSameResultAndCatsInBattle()
        {
            var world = World(Strong(TestIds.Ryzhy), TestCats.Make(TestIds.Shnyr, str: 3, agi: 5, tou: 3));
            Battles.Start(world, TestIds.Backyard, new[] { TestIds.Ryzhy }, _events);
            var original = world.PendingBattle.Result;

            var loaded = SaveMapper.FromSave(SaveJson.Deserialize(SaveJson.Serialize(SaveMapper.ToSave(world, 0, null), false)), _config);

            var restored = loaded.World.PendingBattle;
            Assert.That(restored.Result.Outcome, Is.EqualTo(original.Outcome));
            Assert.That(restored.Result.Log.Count, Is.EqualTo(original.Log.Count));
            Assert.That(loaded.World.GetCat(TestIds.Ryzhy).Activity.Status, Is.EqualTo(ActivityStatus.InBattle));
            Assert.That(loaded.World.GetCat(TestIds.Shnyr).Activity.Status, Is.Not.EqualTo(ActivityStatus.InBattle));
            Assert.That(loaded.World.Territories.Get(TestIds.Backyard).Attempts, Is.EqualTo(1));
            Assert.That(loaded.Warnings, Is.Empty);
        }

        [Test]
        public void DefeatHints_SuggestDodgeAgainstBiters()
        {
            var world = World(TestCats.Make(TestIds.Ryzhy, str: 2, agi: 2, tou: 2));
            world.Territories.Get(TestIds.FishShop).Status = TerritoryStatus.Available;
            Battles.Start(world, TestIds.FishShop, new[] { TestIds.Ryzhy }, _events);

            var hints = DefeatHints.For(world.PendingBattle, _config);

            Assert.That(hints, Is.Not.Empty);
            Assert.That(hints.Count, Is.LessThanOrEqualTo(2));
        }
    }
}
