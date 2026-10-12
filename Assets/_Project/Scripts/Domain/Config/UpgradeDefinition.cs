using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public sealed class UpgradeDefinition
    {
        public UpgradeId Id { get; init; }
        public string NameKey { get; init; }

        /// <summary>Junk cost of each level: Costs[0] buys level 1, Costs[1] buys level 2, ...</summary>
        public IReadOnlyList<int> Costs { get; init; } = Array.Empty<int>();

        public UpgradeEffect Effect { get; init; }

        public int MaxLevel => Costs.Count;
    }

    /// <summary>
    /// Effect of an upgrade. Per-level lists are indexed by level and include level 0 (no upgrade).
    /// </summary>
    public abstract class UpgradeEffect
    {
        public abstract void Contribute(int level, ModifierSet modifiers);
        public abstract int LevelCount { get; }
    }

    /// <summary>Extra Energy restored per Sleep cycle.</summary>
    public sealed class SleepEnergyBonusEffect : UpgradeEffect
    {
        public IReadOnlyList<int> BonusPerLevel { get; init; } = Array.Empty<int>();

        public override int LevelCount => BonusPerLevel.Count;

        public override void Contribute(int level, ModifierSet modifiers) =>
            modifiers.SleepEnergyFlat += BonusPerLevel[level];
    }

    /// <summary>Extra flat Food per Scavenge cycle and Junk per Rummage cycle.</summary>
    public sealed class ResourceOutputBonusEffect : UpgradeEffect
    {
        public IReadOnlyList<int> FoodPerLevel { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> JunkPerLevel { get; init; } = Array.Empty<int>();

        public override int LevelCount => Math.Min(FoodPerLevel.Count, JunkPerLevel.Count);

        public override void Contribute(int level, ModifierSet modifiers)
        {
            modifiers.FoodOutputFlat += FoodPerLevel[level];
            modifiers.JunkOutputFlat += JunkPerLevel[level];
        }
    }

    /// <summary>Extra training XP percent for one attribute (training station upgrade).</summary>
    public sealed class TrainingXpBonusEffect : UpgradeEffect
    {
        public AttributeType Attribute { get; init; }
        public IReadOnlyList<int> PctPerLevel { get; init; } = Array.Empty<int>();

        public override int LevelCount => PctPerLevel.Count;

        public override void Contribute(int level, ModifierSet modifiers) =>
            modifiers.AddTrainingXpPct(Attribute, PctPerLevel[level]);
    }
}
