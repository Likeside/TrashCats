namespace TrashCats.Domain
{
    /// <summary>Combat formula coefficients (Docs/GDD/COMBAT_DESIGN.md §3-4). All integer, permille where fractional.</summary>
    public sealed class CombatConfig
    {
        public int TickMs { get; init; } = 100;

        /// <summary>
        /// Every attribute level adds the same share of its stat's base (decision 2026-10-15): Strength — damage,
        /// Technique — attack speed, Endurance — HP. Accuracy is the same for everyone.
        /// </summary>
        public int AttributePctPerLevel { get; init; } = 10;

        public int HpBase { get; init; } = 70;
        /// <summary>Grit builds up during a fight (Punch Club style): a fighter enters with <see cref="GritStart"/>, gains
        /// it with its own actions and with the hits it takes, up to <see cref="GritMax"/>; no attribute raises it.</summary>
        public int GritMax { get; init; } = 60;
        public int GritStart { get; init; } = 12;
        public int GritPerAction { get; init; } = 5;
        public int GritPerHitTaken { get; init; } = 3;
        public int DamageBase { get; init; } = 10;
        /// <summary>Ticks between actions at Technique 0; each level makes attacks <see cref="AttributePctPerLevel"/>% faster.</summary>
        public int IntervalBaseTicks { get; init; } = 16;
        public int IntervalMinTicks { get; init; } = 5;
        public int HitBasePermille { get; init; } = 800;
        public int HitMinPermille { get; init; } = 350;
        public int HitMaxPermille { get; init; } = 950;
        public int VarianceMinPermille { get; init; } = 850;
        public int VarianceMaxPermille { get; init; } = 1150;
        public int SuddenDeathAfterTicks { get; init; } = 450;
        public int SuddenDeathPermille { get; init; } = 2000;
        public int FighterEntryPauseTicks { get; init; } = 10;
        public int SafetyMaxTicks { get; init; } = 10_000;
    }
}
