using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class HideoutTests
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
        public void Buy_SpendsJunkAndRaisesLevel()
        {
            var upgrades = new UpgradeLevels();
            var wallet = new Wallet(0, 90);

            Hideout.Buy(upgrades, wallet, _config, TestIds.Can, _events);
            Hideout.Buy(upgrades, wallet, _config, TestIds.Can, _events);

            Assert.That(upgrades.GetLevel(TestIds.Can), Is.EqualTo(2));
            Assert.That(wallet.Junk, Is.EqualTo(90 - 20 - 60));
            Assert.That(_events.OfType<UpgradePurchased>().Select(e => e.NewLevel), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void CanBuy_ReportsErrors_AndBuyThrows()
        {
            var upgrades = new UpgradeLevels(new[] { new KeyValuePair<UpgradeId, int>(TestIds.Boxes, 2) });

            Assert.That(Hideout.CanBuy(upgrades, new Wallet(0, 100), _config, TestIds.Boxes), Is.EqualTo(UpgradeError.MaxLevel));
            Assert.That(Hideout.CanBuy(upgrades, new Wallet(0, 19), _config, TestIds.Can), Is.EqualTo(UpgradeError.NotEnoughJunk));
            Assert.That(Hideout.CanBuy(upgrades, new Wallet(0, 100), _config, new UpgradeId("upg_unknown")), Is.EqualTo(UpgradeError.UnknownUpgrade));
            Assert.That(Hideout.CanBuy(upgrades, new Wallet(0, 20), _config, TestIds.Can), Is.EqualTo(UpgradeError.None));

            var wallet = new Wallet(0, 14);
            Assert.Throws<InvalidOperationException>(() => Hideout.Buy(upgrades, wallet, _config, TestIds.Can, _events));
            Assert.That(wallet.Junk, Is.EqualTo(14));
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void NextCost_FollowsCostTable()
        {
            var definition = _config.GetUpgrade(TestIds.Dumpster);

            Assert.That(Hideout.NextCost(Levels(0), definition), Is.EqualTo(20));
            Assert.That(Hideout.NextCost(Levels(1), definition), Is.EqualTo(60));
            Assert.That(Hideout.NextCost(Levels(2), definition), Is.Null);

            static UpgradeLevels Levels(int level) =>
                new UpgradeLevels(new[] { new KeyValuePair<UpgradeId, int>(TestIds.Dumpster, level) });
        }

        [Test]
        public void Contribute_AggregatesAllUpgradeEffects()
        {
            var upgrades = new UpgradeLevels(new[]
            {
                new KeyValuePair<UpgradeId, int>(TestIds.Boxes, 1),
                new KeyValuePair<UpgradeId, int>(TestIds.Dumpster, 2),
                new KeyValuePair<UpgradeId, int>(TestIds.Can, 2),
                new KeyValuePair<UpgradeId, int>(TestIds.Wheel, 1),
            });
            var modifiers = new ModifierSet();

            upgrades.Contribute(_config, modifiers);

            Assert.That(modifiers.SleepEnergyFlat, Is.EqualTo(10));
            Assert.That((modifiers.FoodOutputFlat, modifiers.JunkOutputFlat), Is.EqualTo((2, 2)));
            Assert.That(modifiers.GetTrainingXpPct(AttributeType.Strength), Is.EqualTo(40));
            Assert.That(modifiers.GetTrainingXpPct(AttributeType.Agility), Is.EqualTo(0));
            Assert.That(modifiers.GetTrainingXpPct(AttributeType.Toughness), Is.EqualTo(20));
        }

        [Test]
        public void NoUpgrades_ContributeNothing()
        {
            var modifiers = new ModifierSet();
            new UpgradeLevels().Contribute(_config, modifiers);

            Assert.That(modifiers.SleepEnergyFlat + modifiers.FoodOutputFlat + modifiers.JunkOutputFlat, Is.Zero);
            Assert.That(AttributeTypes.All.Select(modifiers.GetTrainingXpPct), Has.All.Zero);
        }
    }

    public sealed class BonusesTests
    {
        [TestCase(4, 25, 5)]
        [TestCase(5, 25, 6)]   // 6.25 -> 6
        [TestCase(6, 25, 8)]   // 7.5 -> 8 (half up)
        [TestCase(3, 0, 3)]
        public void ApplyPctRounded_RoundsHalfUp(int value, int pct, int expected)
        {
            Assert.That(Bonuses.ApplyPctRounded(value, pct), Is.EqualTo(expected));
        }

        [TestCase(10, 50, 15)]
        [TestCase(10, 75, 17)]  // 17.5 -> 17
        [TestCase(10, 110, 21)]
        public void ApplyPctFloor_RoundsDown(int value, int pct, int expected)
        {
            Assert.That(Bonuses.ApplyPctFloor(value, pct), Is.EqualTo(expected));
        }
    }
}
