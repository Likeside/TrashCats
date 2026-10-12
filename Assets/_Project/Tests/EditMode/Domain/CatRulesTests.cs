using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class CatRulesTests
    {
        private GameConfig _config;
        private List<DomainEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _events = new List<DomainEvent>();
        }

        [Test]
        public void AddXp_BelowThreshold_AccumulatesWithoutLevelUp()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 1);

            CatRules.AddXp(cat, AttributeType.Strength, 9, _config, _events);

            Assert.That(cat.GetLevel(AttributeType.Strength), Is.EqualTo(1));
            Assert.That(cat.GetXp(AttributeType.Strength), Is.EqualTo(9));
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void AddXp_ExactThreshold_LevelsUpWithZeroCarry()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 1);

            CatRules.AddXp(cat, AttributeType.Strength, 10, _config, _events);

            Assert.That(cat.GetLevel(AttributeType.Strength), Is.EqualTo(2));
            Assert.That(cat.GetXp(AttributeType.Strength), Is.EqualTo(0));
            Assert.That(_events.OfType<AttributeLevelUp>().Single().NewLevel, Is.EqualTo(2));
        }

        [Test]
        public void AddXp_LargeAmount_LevelsUpSeveralTimesAndCarriesRemainder()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 1);

            // 10 (1->2) + 22 (2->3) + 34 (3->4) = 66; 4 remain.
            CatRules.AddXp(cat, AttributeType.Strength, 70, _config, _events);

            Assert.That(cat.GetLevel(AttributeType.Strength), Is.EqualTo(4));
            Assert.That(cat.GetXp(AttributeType.Strength), Is.EqualTo(4));
            Assert.That(_events.OfType<AttributeLevelUp>().Select(e => e.NewLevel), Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void AddXp_AtMaxLevel_IsDiscarded()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 9);

            CatRules.AddXp(cat, AttributeType.Strength, 1000, _config, _events);
            Assert.That(cat.GetLevel(AttributeType.Strength), Is.EqualTo(10));
            Assert.That(cat.GetXp(AttributeType.Strength), Is.EqualTo(0));

            _events.Clear();
            CatRules.AddXp(cat, AttributeType.Strength, 50, _config, _events);
            Assert.That(cat.GetXp(AttributeType.Strength), Is.EqualTo(0));
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void AddXp_Negative_Throws()
        {
            var cat = TestCats.Make(TestIds.Ryzhy);
            Assert.Throws<ArgumentOutOfRangeException>(() => CatRules.AddXp(cat, AttributeType.Strength, -1, _config, _events));
        }

        [Test]
        public void ReachingUnlockLevel_UnlocksAndAutoEquipsSkill()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 3);

            CatRules.AddXp(cat, AttributeType.Strength, 34, _config, _events);

            Assert.That(_events.OfType<SkillUnlocked>().Single().SkillId, Is.EqualTo(TestIds.Bite));
            Assert.That(cat.GetSkillInSlot(0), Is.EqualTo(TestIds.Bite));
            Assert.That(_events.OfType<SkillEquipped>().Single().Slot, Is.EqualTo(0));
        }

        [Test]
        public void UnlockWithAllSlotsFull_DoesNotReplaceEquippedSkills()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 6, agi: 4, tou: 4);
            CatRules.Equip(cat, 0, TestIds.Bite, _config, _events);
            CatRules.Equip(cat, 1, TestIds.DoublePaw, _config, _events);
            CatRules.Equip(cat, 2, TestIds.ThickFur, _config, _events);
            _events.Clear();

            CatRules.AddXp(cat, AttributeType.Strength, 70, _config, _events);

            Assert.That(_events.OfType<SkillUnlocked>().Single().SkillId, Is.EqualTo(TestIds.DirtyTrick));
            Assert.That(_events.OfType<SkillEquipped>(), Is.Empty);
            Assert.That(Enumerable.Range(0, 3).Select(cat.GetSkillInSlot), Has.None.EqualTo((SkillId?)TestIds.DirtyTrick));
        }

        [Test]
        public void CanEquip_ReportsEveryError()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 4);
            CatRules.Equip(cat, 0, TestIds.Bite, _config, _events);

            Assert.That(CatRules.CanEquip(cat, 3, TestIds.Bite, _config), Is.EqualTo(SkillEquipError.InvalidSlot));
            Assert.That(CatRules.CanEquip(cat, 1, new SkillId("skill_unknown"), _config), Is.EqualTo(SkillEquipError.UnknownSkill));
            Assert.That(CatRules.CanEquip(cat, 1, TestIds.Scratch, _config), Is.EqualTo(SkillEquipError.NotEquippable));
            Assert.That(CatRules.CanEquip(cat, 1, TestIds.DoublePaw, _config), Is.EqualTo(SkillEquipError.SkillLocked));
            Assert.That(CatRules.CanEquip(cat, 1, TestIds.Bite, _config), Is.EqualTo(SkillEquipError.AlreadyEquipped));
            Assert.That(CatRules.CanEquip(cat, 0, TestIds.Bite, _config), Is.EqualTo(SkillEquipError.None));
            Assert.That(CatRules.CanEquip(cat, 1, null, _config), Is.EqualTo(SkillEquipError.None));
        }

        [Test]
        public void Equip_Null_ClearsSlot_AndInvalidEquipThrows()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, str: 4);
            CatRules.Equip(cat, 0, TestIds.Bite, _config, _events);

            CatRules.Equip(cat, 0, null, _config, _events);

            Assert.That(cat.GetSkillInSlot(0), Is.Null);
            Assert.That(_events.OfType<SkillEquipped>().Last().SkillId, Is.Null);
            Assert.Throws<InvalidOperationException>(() => CatRules.Equip(cat, 1, TestIds.Dodge, _config, _events));
        }

        [Test]
        public void ChangeNeeds_ClampsAndReportsOnlyRealChanges()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, energy: 10, satiety: 95);

            CatRules.ChangeNeeds(cat, -15, +10, _config, _events);
            Assert.That((cat.Energy, cat.Satiety), Is.EqualTo((0, 100)));
            Assert.That(_events.Count, Is.EqualTo(1));

            CatRules.ChangeNeeds(cat, -5, +5, _config, _events);
            Assert.That(_events.Count, Is.EqualTo(1));
        }

        [Test]
        public void TiredAndHungry_UseThresholds()
        {
            Assert.That(CatRules.IsTired(TestCats.Make(TestIds.Ryzhy, energy: 19), _config), Is.True);
            Assert.That(CatRules.IsTired(TestCats.Make(TestIds.Ryzhy, energy: 20), _config), Is.False);
            Assert.That(CatRules.IsHungry(TestCats.Make(TestIds.Ryzhy, satiety: 19), _config), Is.True);
            Assert.That(CatRules.IsHungry(TestCats.Make(TestIds.Ryzhy, satiety: 20), _config), Is.False);
        }
    }

    public sealed class CatFactoryTests
    {
        [Test]
        public void Create_UsesDefinitionStartValues()
        {
            var config = TestConfigs.Default();
            var cat = CatFactory.Create(config.GetCat(TestIds.Ryzhy), config);

            Assert.That(cat.Aptitude, Is.EqualTo(AttributeType.Strength));
            Assert.That(AttributeTypes.All.Select(cat.GetLevel), Is.EqualTo(new[] { 3, 2, 2 }));
            Assert.That((cat.Energy, cat.Satiety), Is.EqualTo((100, 60)));
            Assert.That(cat.Activity.Status, Is.EqualTo(ActivityStatus.Idle));
            Assert.That(Enumerable.Range(0, 3).Select(cat.GetSkillInSlot), Has.All.Null);
        }

        [Test]
        public void Create_AutoEquipsSkillsUnlockedByStartLevels()
        {
            var config = TestConfigs.Default();
            var ponchik = CatFactory.Create(config.GetCat(TestIds.Ponchik), config);

            Assert.That(Enumerable.Range(0, 3).Select(ponchik.GetSkillInSlot),
                Is.EqualTo(new SkillId?[] { TestIds.Bite, TestIds.DoublePaw, TestIds.ThickFur }));
        }
    }

    public sealed class FeedingTests
    {
        private GameConfig _config;
        private List<DomainEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Default();
            _events = new List<DomainEvent>();
        }

        [Test]
        public void Feed_FillsToMax_SpendingRoundedUpFood()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, satiety: 95);
            var wallet = new Wallet(10, 0);

            var spent = Feeding.Feed(cat, wallet, _config, _events);

            Assert.That(spent, Is.EqualTo(1));
            Assert.That(cat.Satiety, Is.EqualTo(100));
            Assert.That(wallet.Food, Is.EqualTo(9));
        }

        [Test]
        public void Feed_NotEnoughFood_SpendsAll()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, satiety: 10);
            var wallet = new Wallet(3, 0);

            var spent = Feeding.Feed(cat, wallet, _config, _events);

            Assert.That(spent, Is.EqualTo(3));
            Assert.That(cat.Satiety, Is.EqualTo(40));
            Assert.That(wallet.Food, Is.EqualTo(0));
            Assert.That(_events.OfType<ResourcesChanged>().Single().FoodDelta, Is.EqualTo(-3));
            Assert.That(_events.OfType<NeedsChanged>().Single().Satiety, Is.EqualTo(40));
        }

        [Test]
        public void CanFeed_ReportsErrors_AndFeedThrowsOnError()
        {
            Assert.That(Feeding.CanFeed(TestCats.Make(TestIds.Ryzhy, satiety: 100), new Wallet(5, 0), _config), Is.EqualTo(FeedError.AlreadyFull));
            Assert.That(Feeding.CanFeed(TestCats.Make(TestIds.Ryzhy, satiety: 50), new Wallet(0, 0), _config), Is.EqualTo(FeedError.NoFood));
            var fighter = TestCats.Make(TestIds.Ryzhy, satiety: 50, activity: ActivityState.InBattle);
            Assert.That(Feeding.CanFeed(fighter, new Wallet(5, 0), _config), Is.EqualTo(FeedError.InBattle));

            Assert.Throws<InvalidOperationException>(() => Feeding.Feed(fighter, new Wallet(5, 0), _config, _events));
        }

        [Test]
        public void Feed_WhileWorking_IsAllowed()
        {
            var cat = TestCats.Make(TestIds.Ryzhy, satiety: 50, activity: ActivityState.Working(TestIds.TrainStr, 0, 30_000, false));

            Assert.That(Feeding.CanFeed(cat, new Wallet(5, 0), _config), Is.EqualTo(FeedError.None));
        }
    }

    public sealed class WalletTests
    {
        [Test]
        public void AddAndSpend_UpdateBalanceAndEmitEvents()
        {
            var events = new List<DomainEvent>();
            var wallet = new Wallet(5, 2);

            wallet.Add(ResourceType.Junk, 3, events);
            Assert.That(wallet.TrySpend(ResourceType.Food, 4, events), Is.True);

            Assert.That((wallet.Food, wallet.Junk), Is.EqualTo((1, 5)));
            Assert.That(events.Cast<ResourcesChanged>().Select(e => (e.FoodDelta, e.JunkDelta)), Is.EqualTo(new[] { (0, 3), (-4, 0) }));
        }

        [Test]
        public void TrySpend_Insufficient_ChangesNothing()
        {
            var events = new List<DomainEvent>();
            var wallet = new Wallet(1, 0);

            Assert.That(wallet.TrySpend(ResourceType.Junk, 1, events), Is.False);
            Assert.That(wallet.Junk, Is.EqualTo(0));
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            var events = new List<DomainEvent>();
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(0, 0).Add(ResourceType.Food, -1, events));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(0, 0).TrySpend(ResourceType.Food, -1, events));
        }
    }
}
