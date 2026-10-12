using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public enum UpgradeError
    {
        None = 0,
        UnknownUpgrade,
        MaxLevel,
        NotEnoughJunk,
    }

    /// <summary>Buying hideout upgrades with Junk.</summary>
    public static class Hideout
    {
        /// <summary>Junk cost of the next level, or null when the object is at max level.</summary>
        public static int? NextCost(UpgradeLevels upgrades, UpgradeDefinition definition)
        {
            var level = upgrades.GetLevel(definition.Id);
            return level >= definition.MaxLevel ? (int?)null : definition.Costs[level];
        }

        public static UpgradeError CanBuy(UpgradeLevels upgrades, Wallet wallet, GameConfig config, UpgradeId id)
        {
            if (!config.TryGetUpgrade(id, out var definition)) return UpgradeError.UnknownUpgrade;
            var cost = NextCost(upgrades, definition);
            if (cost == null) return UpgradeError.MaxLevel;
            if (!wallet.Has(ResourceType.Junk, cost.Value)) return UpgradeError.NotEnoughJunk;
            return UpgradeError.None;
        }

        /// <summary>Spends Junk and raises the level by one. Throws if <see cref="CanBuy"/> fails.</summary>
        public static void Buy(UpgradeLevels upgrades, Wallet wallet, GameConfig config, UpgradeId id, List<DomainEvent> events)
        {
            var error = CanBuy(upgrades, wallet, config, id);
            if (error != UpgradeError.None)
                throw new InvalidOperationException($"Cannot buy upgrade {id}: {error}.");

            var definition = config.GetUpgrade(id);
            var level = upgrades.GetLevel(id);
            wallet.TrySpend(ResourceType.Junk, definition.Costs[level], events);
            upgrades.SetLevel(id, level + 1);
            events.Add(new UpgradePurchased(id, level + 1));
        }
    }
}
