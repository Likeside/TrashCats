using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Graybox Russian strings in one place. Moves to a localization table in M5 (keys already exist in the config).
    /// </summary>
    public static class Texts
    {
        public static string CatName(CatId id) => id.Value switch
        {
            "cat_ryzhy" => Loc.T("texts.01"),
            "cat_shnyr" => Loc.T("texts.02"),
            "cat_ponchik" => Loc.T("texts.03"),
            _ => id.Value,
        };

        public static string Attribute(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => Loc.T("texts.04"),
            AttributeType.Agility => Loc.T("texts.05"),
            _ => Loc.T("texts.06"),
        };

        public static string AttributeGenitive(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => Loc.T("texts.07"),
            AttributeType.Agility => Loc.T("texts.08"),
            _ => Loc.T("texts.09"),
        };

        public static string ActivityName(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => Loc.T("texts.10"),
            ActivityKind.Rummage => Loc.T("texts.11"),
            ActivityKind.Sleep => Loc.T("texts.12"),
            _ => Loc.T("texts.13") + Attribute(a.TrainedAttribute),
        };

        public static string ActivityShort(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => Loc.T("texts.14"),
            ActivityKind.Rummage => Loc.T("texts.15"),
            ActivityKind.Sleep => Loc.T("texts.12"),
            _ => Attribute(a.TrainedAttribute),
        };

        public static string Doing(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => Loc.T("texts.16"),
            ActivityKind.Rummage => Loc.T("texts.17"),
            ActivityKind.Sleep => Loc.T("texts.18"),
            _ => Loc.T("texts.19") + AttributeGenitive(a.TrainedAttribute).ToLowerInvariant(),
        };

        public static string Output(ActivityDefinition a, int amount) => a.Kind switch
        {
            ActivityKind.Scavenge => Loc.T("texts.20", amount),
            ActivityKind.Rummage => Loc.T("texts.21", amount),
            ActivityKind.Sleep => Loc.T("texts.22", amount),
            _ => Loc.T("texts.23", amount),
        };

        public static string UpgradeName(UpgradeId id) => id.Value switch
        {
            "upg_boxes" => Loc.T("texts.24"),
            "upg_dumpster" => Loc.T("texts.25"),
            "upg_can" => Loc.T("texts.26"),
            "upg_mouse" => Loc.T("texts.27"),
            "upg_wheel" => Loc.T("texts.28"),
            _ => id.Value,
        };

        public static string UpgradeShort(UpgradeId id) => id.Value == "upg_dumpster" ? Loc.T("texts.29") : UpgradeName(id);

        public static string IdleBubble(IdleReason reason) => reason switch
        {
            IdleReason.Tired => Loc.T("texts.30"),
            IdleReason.Hungry => Loc.T("texts.31"),
            IdleReason.SleepDone => Loc.T("texts.32"),
            IdleReason.AttributeMax => "MAX",
            IdleReason.AfterBattle => Loc.T("texts.33"),
            _ => "",
        };

        public static string IdleStatus(IdleReason reason) => reason switch
        {
            IdleReason.Tired => Loc.T("texts.34"),
            IdleReason.Hungry => Loc.T("texts.35"),
            IdleReason.SleepDone => Loc.T("texts.32"),
            IdleReason.AttributeMax => Loc.T("texts.36"),
            _ => Loc.T("texts.37"),
        };

        public static string Error(CommandError error) => error switch
        {
            CommandError.Tired => Loc.T("texts.30"),
            CommandError.EnergyFull => Loc.T("texts.38"),
            CommandError.StationBusy => Loc.T("texts.39"),
            CommandError.AttributeMaxed => "MAX",
            CommandError.InBattle => Loc.T("texts.40"),
            CommandError.NoFood => Loc.T("texts.41"),
            CommandError.AlreadyFull => Loc.T("texts.42"),
            CommandError.NotEnoughJunk => Loc.T("texts.43"),
            CommandError.MaxLevel => Loc.T("texts.44"),
            CommandError.NotWorking => Loc.T("texts.45"),
            CommandError.TerritoryLocked => Loc.T("texts.46"),
            CommandError.InvalidSlot => Loc.T("texts.47"),
            CommandError.SkillLocked => Loc.T("texts.48"),
            CommandError.InvalidTeam => Loc.T("texts.49"),
            CommandError.CatTooTired => Loc.T("texts.50"),
            CommandError.BattlePending => Loc.T("texts.51"),
            _ => error.ToString(),
        };

        public static string TerritoryName(TerritoryId id) => id.Value switch
        {
            "ter_backyard" => Loc.T("texts.52"),
            "ter_fishshop" => Loc.T("texts.53"),
            "ter_alley" => Loc.T("texts.54"),
            "ter_park" => Loc.T("texts.55"),
            "ter_rooftop" => Loc.T("texts.56"),
            _ => id.Value,
        };

        public static string EnemyName(string id) => id switch
        {
            "enemy_rat" => Loc.T("texts.57"),
            "enemy_gull" => Loc.T("texts.58"),
            "enemy_shopcat" => Loc.T("texts.59"),
            "enemy_crow" => Loc.T("texts.60"),
            "enemy_bigrat" => Loc.T("texts.61"),
            "enemy_crow_boss" => Loc.T("texts.62"),
            "enemy_raccoon" => Loc.T("texts.63"),
            "enemy_pug" => Loc.T("texts.64"),
            "enemy_admiral" => Loc.T("texts.65"),
            _ => CatName(new CatId(id)),
        };

        public static string SkillName(SkillId id) => id.Value switch
        {
            "skill_scratch" => Loc.T("texts.66"),
            "skill_bite" => Loc.T("texts.67"),
            "skill_double_paw" => Loc.T("texts.68"),
            "skill_thick_fur" => Loc.T("texts.69"),
            "skill_dodge" => Loc.T("texts.70"),
            "skill_dirty_trick" => Loc.T("texts.71"),
            _ => id.Value,
        };

        public static string SkillDescription(SkillDefinition s) => s.Effect switch
        {
            SkillEffectKind.Damage when s.Hits > 1 => Loc.T("texts.72", s.Hits, s.DamagePermille / 10),
            SkillEffectKind.Damage => s.DamagePermille > 1000 ? Loc.T("texts.73", s.DamagePermille / 1000f, s.HitModifierPermille / 10) : Loc.T("texts.74"),
            SkillEffectKind.DamageReduction => Loc.T("texts.75", s.EffectCount, 100 - s.EffectPermille / 10),
            SkillEffectKind.GuaranteedEvade => Loc.T("texts.76"),
            _ => Loc.T("texts.77", s.EffectPermille / 10, s.EffectCount),
        };

        public static string Bonus(TerritoryDefinition t) => t.Bonus switch
        {
            TerritoryBonusKind.SleepEnergyFlat => Loc.T("texts.78", t.BonusValue),
            TerritoryBonusKind.FoodOutputPct => Loc.T("texts.79", t.BonusValue),
            TerritoryBonusKind.JunkOutputPct => Loc.T("texts.80", t.BonusValue),
            TerritoryBonusKind.TrainingXpPct => Loc.T("texts.81", t.BonusValue),
            _ => t.IsFinal ? Loc.T("texts.82") : "",
        };

        public static string Stats(int s, int a, int t) => Loc.T("texts.83", s, a, t);

        public static string Seconds(long ms) => Loc.T("texts.84", ms / 1000);

        public static string Duration(long ms)
        {
            var minutes = ms / 60_000;
            return minutes > 0 ? Loc.T("texts.85", minutes) : Loc.T("texts.84", ms / 1000);
        }

        /// <summary>Skin icon of an attribute.</summary>
        public static string AttributeIcon(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => "strength",
            AttributeType.Agility => "agility",
            _ => "toughness",
        };

        /// <summary>Skin icon of what an activity yields (food, junk, an attribute, energy).</summary>
        public static string OutcomeIcon(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => "food",
            ActivityKind.Rummage => "junk",
            ActivityKind.Sleep => "energy",
            _ => AttributeIcon(activity.TrainedAttribute),
        };
    }
}
