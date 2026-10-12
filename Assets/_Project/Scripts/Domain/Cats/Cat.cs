using System;

namespace TrashCats.Domain
{
    public interface IReadOnlyCat
    {
        CatId Id { get; }
        AttributeType Aptitude { get; }
        int Energy { get; }
        int Satiety { get; }
        ActivityState Activity { get; }
        int GetLevel(AttributeType attribute);
        int GetXp(AttributeType attribute);
        SkillId? GetSkillInSlot(int slot);
    }

    /// <summary>
    /// Mutable state of a recruited cat. Rules that change it live in <see cref="CatRules"/>,
    /// <see cref="ActivitySystem"/> and other domain systems.
    /// </summary>
    public sealed class Cat : IReadOnlyCat
    {
        public const int SkillSlotCount = 3;

        private readonly int[] _levels = new int[AttributeTypes.Count];
        private readonly int[] _xp = new int[AttributeTypes.Count];
        private readonly SkillId?[] _skills = new SkillId?[SkillSlotCount];

        public CatId Id { get; }
        public AttributeType Aptitude { get; }
        public int Energy { get; private set; }
        public int Satiety { get; private set; }
        public ActivityState Activity { get; internal set; }

        /// <summary>Restores a cat with explicit state (new cats are created by <see cref="CatFactory"/>).</summary>
        public Cat(CatId id, AttributeType aptitude, int[] levels, int[] xp, int energy, int satiety, SkillId?[] skills, ActivityState activity)
        {
            if (levels == null || levels.Length != AttributeTypes.Count) throw new ArgumentException("Three levels expected.", nameof(levels));
            if (xp == null || xp.Length != AttributeTypes.Count) throw new ArgumentException("Three XP values expected.", nameof(xp));
            if (skills == null || skills.Length != SkillSlotCount) throw new ArgumentException("Three skill slots expected.", nameof(skills));

            Id = id;
            Aptitude = aptitude;
            Array.Copy(levels, _levels, _levels.Length);
            Array.Copy(xp, _xp, _xp.Length);
            Array.Copy(skills, _skills, _skills.Length);
            Energy = energy;
            Satiety = satiety;
            Activity = activity ?? ActivityState.Idle(IdleReason.None);
        }

        public int GetLevel(AttributeType attribute) => _levels[(int)attribute];
        public int GetXp(AttributeType attribute) => _xp[(int)attribute];
        public SkillId? GetSkillInSlot(int slot) => _skills[slot];

        internal void SetProgress(AttributeType attribute, int level, int xp)
        {
            _levels[(int)attribute] = level;
            _xp[(int)attribute] = xp;
        }

        internal void SetNeeds(int energy, int satiety)
        {
            Energy = energy;
            Satiety = satiety;
        }

        internal void SetSkillInSlot(int slot, SkillId? skill) => _skills[slot] = skill;
    }
}
