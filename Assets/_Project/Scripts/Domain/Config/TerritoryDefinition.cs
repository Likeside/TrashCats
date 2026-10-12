using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public readonly struct TerritoryId : IEquatable<TerritoryId>
    {
        public string Value { get; }

        public TerritoryId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool Equals(TerritoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TerritoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";
        public static bool operator ==(TerritoryId a, TerritoryId b) => a.Equals(b);
        public static bool operator !=(TerritoryId a, TerritoryId b) => !a.Equals(b);
    }

    public enum TerritoryBonusKind
    {
        None = 0,
        SleepEnergyFlat = 1,
        FoodOutputPct = 2,
        JunkOutputPct = 3,
        TrainingXpPct = 4,
    }

    /// <summary>A battle location (Docs/GDD/PROGRESSION_AND_ECONOMY.md §4). Listed in unlock order.</summary>
    public sealed class TerritoryDefinition
    {
        public TerritoryId Id { get; init; }
        public string NameKey { get; init; }
        public IReadOnlyList<EnemyId> EnemyQueue { get; init; } = Array.Empty<EnemyId>();
        public int FirstRewardFood { get; init; }
        public int FirstRewardJunk { get; init; }
        public int RepeatRewardPct { get; init; } = 30;
        public TerritoryBonusKind Bonus { get; init; }
        public int BonusValue { get; init; }
        public CatId? RecruitCat { get; init; }
        public bool IsFinal { get; init; }
        public string ViewKey { get; init; }

        public void Contribute(ModifierSet modifiers)
        {
            switch (Bonus)
            {
                case TerritoryBonusKind.SleepEnergyFlat: modifiers.SleepEnergyFlat += BonusValue; break;
                case TerritoryBonusKind.FoodOutputPct: modifiers.FoodOutputPct += BonusValue; break;
                case TerritoryBonusKind.JunkOutputPct: modifiers.JunkOutputPct += BonusValue; break;
                case TerritoryBonusKind.TrainingXpPct: modifiers.TrainingXpPctAll += BonusValue; break;
            }
        }
    }
}
