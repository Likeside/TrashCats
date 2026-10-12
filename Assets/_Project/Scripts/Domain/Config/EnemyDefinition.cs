using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public readonly struct EnemyId : IEquatable<EnemyId>
    {
        public string Value { get; }

        public EnemyId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool Equals(EnemyId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EnemyId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";
        public static bool operator ==(EnemyId a, EnemyId b) => a.Equals(b);
        public static bool operator !=(EnemyId a, EnemyId b) => !a.Equals(b);
    }

    public sealed class EnemyDefinition
    {
        public EnemyId Id { get; init; }
        public string NameKey { get; init; }
        public int Strength { get; init; } = 1;
        public int Agility { get; init; } = 1;
        public int Toughness { get; init; } = 1;
        public IReadOnlyList<SkillId> Skills { get; init; } = Array.Empty<SkillId>();

        /// <summary>Boss HP multiplier, permille (1500 = ×1.5).</summary>
        public int HpMultiplierPermille { get; init; } = 1000;

        public string ViewKey { get; init; }
    }
}
