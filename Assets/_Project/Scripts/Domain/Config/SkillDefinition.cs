namespace TrashCats.Domain
{
    public enum SkillEffectKind
    {
        /// <summary>One or more damaging hits.</summary>
        Damage = 0,

        /// <summary>Self buff: the next N landed hits deal reduced damage.</summary>
        DamageReduction = 1,

        /// <summary>Self buff: the next N enemy attacks miss.</summary>
        GuaranteedEvade = 2,

        /// <summary>Enemy debuff: lower hit chance for the enemy's next N actions.</summary>
        AccuracyDebuff = 3,
    }

    public sealed class SkillDefinition
    {
        public SkillId Id { get; init; }
        public string NameKey { get; init; }

        /// <summary>Innate skills (Scratch) are always available and never occupy a slot.</summary>
        public bool IsInnate { get; init; }

        public AttributeType UnlockAttribute { get; init; }
        public int UnlockLevel { get; init; }

        // --- combat (Docs/GDD/COMBAT_DESIGN.md §5) ---

        public int GritCost { get; init; }

        /// <summary>Own actions before the skill can be used again.</summary>
        public int CooldownActions { get; init; }

        public SkillEffectKind Effect { get; init; }

        /// <summary>Damage: multiplier of base damage per hit, permille.</summary>
        public int DamagePermille { get; init; } = 1000;

        /// <summary>Damage: hit chance modifier, permille points (negative = less accurate).</summary>
        public int HitModifierPermille { get; init; }

        /// <summary>Damage: number of hits, each rolled separately.</summary>
        public int Hits { get; init; } = 1;

        /// <summary>DamageReduction: damage multiplier while active, permille. AccuracyDebuff: hit chance change, permille points.</summary>
        public int EffectPermille { get; init; }

        /// <summary>DamageReduction / GuaranteedEvade: charges. AccuracyDebuff: affected enemy actions.</summary>
        public int EffectCount { get; init; }

        public bool IsHeavyAttack => Effect == SkillEffectKind.Damage && Hits == 1 && DamagePermille > 1000;
        public bool IsMultiHit => Effect == SkillEffectKind.Damage && Hits > 1;
    }
}
