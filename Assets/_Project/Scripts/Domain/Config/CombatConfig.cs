namespace TrashCats.Domain
{
    /// <summary>Combat formula coefficients (Docs/GDD/COMBAT_DESIGN.md §3-4). All integer, permille where fractional.</summary>
    public sealed class CombatConfig
    {
        public int TickMs { get; init; } = 100;
        public int HpBase { get; init; } = 40;
        public int HpPerEndurance { get; init; } = 12;
        public int GritBase { get; init; } = 20;
        public int GritPerEndurance { get; init; } = 5;
        public int GritRegenPerAction { get; init; } = 3;
        public int DamageBase { get; init; } = 4;
        public int DamagePerStrength { get; init; } = 2;
        public int IntervalBaseTicks { get; init; } = 16;
        public int IntervalTechniqueDivisor { get; init; } = 2;
        public int IntervalMinTicks { get; init; } = 10;
        public int HitBasePermille { get; init; } = 750;
        public int HitPerTechniqueDiffPermille { get; init; } = 40;
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
