using System;

namespace TrashCats.Domain
{
    /// <summary>Strongly typed identifier of a config entry, e.g. "cat_ryzhy".</summary>
    public readonly struct CatId : IEquatable<CatId>
    {
        public string Value { get; }

        public CatId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool IsEmpty => Value == null;

        public bool Equals(CatId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CatId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";

        public static bool operator ==(CatId a, CatId b) => a.Equals(b);
        public static bool operator !=(CatId a, CatId b) => !a.Equals(b);
    }

    /// <summary>Strongly typed identifier of a config entry, e.g. "act_train_str".</summary>
    public readonly struct ActivityId : IEquatable<ActivityId>
    {
        public string Value { get; }

        public ActivityId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool IsEmpty => Value == null;

        public bool Equals(ActivityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ActivityId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";

        public static bool operator ==(ActivityId a, ActivityId b) => a.Equals(b);
        public static bool operator !=(ActivityId a, ActivityId b) => !a.Equals(b);
    }

    /// <summary>Strongly typed identifier of a config entry, e.g. "upg_can".</summary>
    public readonly struct UpgradeId : IEquatable<UpgradeId>
    {
        public string Value { get; }

        public UpgradeId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool IsEmpty => Value == null;

        public bool Equals(UpgradeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is UpgradeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";

        public static bool operator ==(UpgradeId a, UpgradeId b) => a.Equals(b);
        public static bool operator !=(UpgradeId a, UpgradeId b) => !a.Equals(b);
    }

    /// <summary>Strongly typed identifier of a config entry, e.g. "skill_bite".</summary>
    public readonly struct SkillId : IEquatable<SkillId>
    {
        public string Value { get; }

        public SkillId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Id must not be empty.", nameof(value));
            Value = value;
        }

        public bool IsEmpty => Value == null;

        public bool Equals(SkillId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SkillId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<empty>";

        public static bool operator ==(SkillId a, SkillId b) => a.Equals(b);
        public static bool operator !=(SkillId a, SkillId b) => !a.Equals(b);
    }
}
