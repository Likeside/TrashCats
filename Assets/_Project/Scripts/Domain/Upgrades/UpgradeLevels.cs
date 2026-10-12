using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>Current level of each hideout object (0 = not upgraded).</summary>
    public sealed class UpgradeLevels
    {
        private readonly Dictionary<UpgradeId, int> _levels = new Dictionary<UpgradeId, int>();

        public UpgradeLevels(IEnumerable<KeyValuePair<UpgradeId, int>> levels = null)
        {
            if (levels == null) return;
            foreach (var pair in levels)
            {
                if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(levels), "Upgrade level must not be negative.");
                if (pair.Value > 0) _levels[pair.Key] = pair.Value;
            }
        }

        public IReadOnlyDictionary<UpgradeId, int> All => _levels;

        public int GetLevel(UpgradeId id) => _levels.TryGetValue(id, out var level) ? level : 0;

        internal void SetLevel(UpgradeId id, int level) => _levels[id] = level;

        /// <summary>Adds the effects of all current levels to <paramref name="modifiers"/>.</summary>
        public void Contribute(GameConfig config, ModifierSet modifiers)
        {
            foreach (var upgrade in config.Upgrades)
                upgrade.Effect.Contribute(Math.Min(GetLevel(upgrade.Id), upgrade.MaxLevel), modifiers);
        }
    }

    public sealed class UpgradePurchased : DomainEvent
    {
        public UpgradeId UpgradeId { get; }
        public int NewLevel { get; }

        public UpgradePurchased(UpgradeId upgradeId, int newLevel)
        {
            UpgradeId = upgradeId;
            NewLevel = newLevel;
        }
    }
}
