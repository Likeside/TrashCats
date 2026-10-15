using TrashCats.Domain;

namespace TrashCats.Tests
{
    public static class TestIds
    {
        public static readonly CatId Ryzhy = new CatId("cat_ryzhy");
        public static readonly CatId Shnyr = new CatId("cat_shnyr");
        public static readonly CatId Ponchik = new CatId("cat_ponchik");

        public static readonly ActivityId Scavenge = new ActivityId("act_scavenge");
        public static readonly ActivityId Rummage = new ActivityId("act_rummage");
        public static readonly ActivityId TrainStr = new ActivityId("act_train_str");
        public static readonly ActivityId TrainAgi = new ActivityId("act_train_agi");
        public static readonly ActivityId TrainTou = new ActivityId("act_train_tou");
        public static readonly ActivityId Sleep = new ActivityId("act_sleep");

        public static readonly UpgradeId Boxes = new UpgradeId("upg_boxes");
        public static readonly UpgradeId Dumpster = new UpgradeId("upg_dumpster");
        public static readonly UpgradeId Can = new UpgradeId("upg_can");
        public static readonly UpgradeId Mouse = new UpgradeId("upg_mouse");
        public static readonly UpgradeId Wheel = new UpgradeId("upg_wheel");

        public static readonly SkillId Scratch = new SkillId("skill_scratch");
        public static readonly SkillId Bite = new SkillId("skill_bite");
        public static readonly SkillId DoublePaw = new SkillId("skill_double_paw");
        public static readonly SkillId ThickFur = new SkillId("skill_thick_fur");
        public static readonly SkillId Dodge = new SkillId("skill_dodge");
        public static readonly SkillId SecondWind = new SkillId("skill_second_wind");
        public static readonly SkillId DirtyTrick = new SkillId("skill_dirty_trick");

        public static readonly EnemyId Rat = new EnemyId("enemy_rat");
        public static readonly EnemyId Gull = new EnemyId("enemy_gull");
        public static readonly EnemyId ShopCat = new EnemyId("enemy_shopcat");
        public static readonly EnemyId Crow = new EnemyId("enemy_crow");
        public static readonly EnemyId BigRat = new EnemyId("enemy_bigrat");
        public static readonly EnemyId CrowBoss = new EnemyId("enemy_crow_boss");
        public static readonly EnemyId Raccoon = new EnemyId("enemy_raccoon");
        public static readonly EnemyId Pug = new EnemyId("enemy_pug");
        public static readonly EnemyId Admiral = new EnemyId("enemy_admiral");

        public static readonly TerritoryId Backyard = new TerritoryId("ter_backyard");
        public static readonly TerritoryId FishShop = new TerritoryId("ter_fishshop");
        public static readonly TerritoryId Alley = new TerritoryId("ter_alley");
        public static readonly TerritoryId Park = new TerritoryId("ter_park");
        public static readonly TerritoryId Rooftop = new TerritoryId("ter_rooftop");
    }

    /// <summary>
    /// Game config with the starting values from Docs/GDD (PROGRESSION_AND_ECONOMY "Сводная таблица параметров").
    /// </summary>
    public static class TestConfigs
    {
        public static GameConfig Default() => new GameConfig
        {
            Needs = new NeedsConfig(),
            Time = new TimeConfig(),
            Progression = new ProgressionConfig { XpPerLevel = 12 },
            Start = new StartConfig { Food = 10, Junk = 0, Cats = new[] { TestIds.Ryzhy } },
            Cats = new[]
            {
                new CatDefinition
                {
                    Id = TestIds.Ryzhy, NameKey = "cat.ryzhy", Aptitude = AttributeType.Strength,
                    StartStrength = 3, StartTechnique = 2, StartEndurance = 2, StartEnergy = 100, StartSatiety = 60,
                    ViewKey = "ryzhy",
                },
                new CatDefinition
                {
                    Id = TestIds.Shnyr, NameKey = "cat.shnyr", Aptitude = AttributeType.Technique,
                    StartStrength = 3, StartTechnique = 5, StartEndurance = 3, StartEnergy = 100, StartSatiety = 50,
                    ViewKey = "shnyr",
                },
                new CatDefinition
                {
                    Id = TestIds.Ponchik, NameKey = "cat.ponchik", Aptitude = AttributeType.Endurance,
                    StartStrength = 5, StartTechnique = 4, StartEndurance = 7, StartEnergy = 100, StartSatiety = 50,
                    ViewKey = "ponchik",
                },
            },
            Activities = new[]
            {
                new ActivityDefinition
                {
                    Id = TestIds.Scavenge, NameKey = "act.scavenge", Kind = ActivityKind.Scavenge,
                    DurationMs = 20_000, EnergyCost = 10, SatietyCost = 4, BaseOutput = 3, Capacity = 1,
                },
                new ActivityDefinition
                {
                    Id = TestIds.Rummage, NameKey = "act.rummage", Kind = ActivityKind.Rummage,
                    DurationMs = 25_000, EnergyCost = 10, SatietyCost = 4, BaseOutput = 3, Capacity = 1,
                },
                Training(TestIds.TrainStr, "act.train_str", AttributeType.Strength),
                Training(TestIds.TrainAgi, "act.train_agi", AttributeType.Technique),
                Training(TestIds.TrainTou, "act.train_tou", AttributeType.Endurance),
                new ActivityDefinition
                {
                    Id = TestIds.Sleep, NameKey = "act.sleep", Kind = ActivityKind.Sleep,
                    DurationMs = 15_000, EnergyCost = 0, SatietyCost = 0, BaseOutput = 25, Capacity = 1,
                },
            },
            Upgrades = new[]
            {
                new UpgradeDefinition
                {
                    Id = TestIds.Boxes, NameKey = "upg.boxes", Costs = new[] { 20, 60 },
                    Effect = new SleepEnergyBonusEffect { BonusPerLevel = new[] { 0, 10, 20 } },
                },
                new UpgradeDefinition
                {
                    Id = TestIds.Dumpster, NameKey = "upg.dumpster", Costs = new[] { 20, 60 },
                    Effect = new ResourceOutputBonusEffect { FoodPerLevel = new[] { 0, 1, 2 }, JunkPerLevel = new[] { 0, 1, 2 } },
                },
                Station(TestIds.Can, "upg.can", AttributeType.Technique),
                Station(TestIds.Mouse, "upg.mouse", AttributeType.Endurance),
                Station(TestIds.Wheel, "upg.wheel", AttributeType.Strength),
            },
            Combat = new CombatConfig(),
            Skills = new[]
            {
                new SkillDefinition { Id = TestIds.Scratch, NameKey = "skill.scratch", IsInnate = true, Effect = SkillEffectKind.Damage, DamagePermille = 1000 },
                new SkillDefinition
                {
                    Id = TestIds.Bite, NameKey = "skill.bite", UnlockAttribute = AttributeType.Strength, UnlockLevel = 4,
                    GritCost = 12, CooldownActions = 3, Effect = SkillEffectKind.Damage, DamagePermille = 2000, HitModifierPermille = -100,
                },
                new SkillDefinition
                {
                    Id = TestIds.DoublePaw, NameKey = "skill.double_paw", UnlockAttribute = AttributeType.Technique, UnlockLevel = 4,
                    GritCost = 10, CooldownActions = 2, Effect = SkillEffectKind.Damage, DamagePermille = 700, Hits = 2,
                },
                new SkillDefinition
                {
                    Id = TestIds.ThickFur, NameKey = "skill.thick_fur", UnlockAttribute = AttributeType.Endurance, UnlockLevel = 4,
                    GritCost = 10, CooldownActions = 5, Effect = SkillEffectKind.DamageReduction, EffectPermille = 600, EffectCount = 3,
                },
                new SkillDefinition
                {
                    Id = TestIds.Dodge, NameKey = "skill.dodge", UnlockAttribute = AttributeType.Technique, UnlockLevel = 7,
                    GritCost = 8, CooldownActions = 4, Effect = SkillEffectKind.GuaranteedEvade, EffectCount = 1,
                },
                new SkillDefinition
                {
                    Id = TestIds.DirtyTrick, NameKey = "skill.dirty_trick", UnlockAttribute = AttributeType.Strength, UnlockLevel = 7,
                    GritCost = 12, CooldownActions = 6, Effect = SkillEffectKind.AccuracyDebuff, EffectPermille = 200, EffectCount = 3,
                },
                new SkillDefinition
                {
                    Id = TestIds.SecondWind, NameKey = "skill.second_wind", UnlockAttribute = AttributeType.Endurance, UnlockLevel = 7,
                    GritCost = 15, CooldownActions = 99, Effect = SkillEffectKind.Heal, EffectPermille = 300,
                },
            },
            Enemies = new[]
            {
                Enemy(TestIds.Rat, "enemy.rat", 2, 2, 1),
                Enemy(TestIds.Gull, "enemy.gull", 3, 5, 2, TestIds.DoublePaw),
                Enemy(TestIds.ShopCat, "enemy.shopcat", 5, 3, 4, TestIds.Bite),
                Enemy(TestIds.Crow, "enemy.crow", 4, 6, 3, TestIds.DoublePaw, TestIds.Dodge),
                Enemy(TestIds.BigRat, "enemy.bigrat", 5, 4, 4, TestIds.Bite, TestIds.DirtyTrick),
                Enemy(TestIds.CrowBoss, "enemy.crow_boss", 5, 7, 3, TestIds.DoublePaw, TestIds.Dodge),
                Enemy(TestIds.Raccoon, "enemy.raccoon", 7, 5, 5, TestIds.Bite, TestIds.DirtyTrick),
                Enemy(TestIds.Pug, "enemy.pug", 5, 3, 9, TestIds.ThickFur, TestIds.Bite),
                new EnemyDefinition
                {
                    Id = TestIds.Admiral, NameKey = "enemy.admiral", Strength = 9, Technique = 7, Endurance = 10,
                    Skills = new[] { TestIds.Bite, TestIds.ThickFur, TestIds.DirtyTrick }, HpMultiplierPermille = 2200, ViewKey = "admiral",
                },
            },
            Territories = new[]
            {
                new TerritoryDefinition
                {
                    Id = TestIds.Backyard, NameKey = "ter.backyard", EnemyQueue = new[] { TestIds.Rat }, FirstRewardFood = 10, FirstRewardJunk = 10,
                    Bonus = TerritoryBonusKind.SleepEnergyFlat, BonusValue = 5, RecruitCat = TestIds.Shnyr, ViewKey = "backyard",
                },
                new TerritoryDefinition
                {
                    Id = TestIds.FishShop, NameKey = "ter.fishshop", EnemyQueue = new[] { TestIds.Gull, TestIds.ShopCat }, FirstRewardFood = 15, FirstRewardJunk = 15,
                    Bonus = TerritoryBonusKind.FoodOutputPct, BonusValue = 25, ViewKey = "fishshop",
                },
                new TerritoryDefinition
                {
                    Id = TestIds.Alley, NameKey = "ter.alley", EnemyQueue = new[] { TestIds.Crow, TestIds.BigRat }, FirstRewardFood = 15, FirstRewardJunk = 25,
                    Bonus = TerritoryBonusKind.JunkOutputPct, BonusValue = 25, RecruitCat = TestIds.Ponchik, ViewKey = "alley",
                },
                new TerritoryDefinition
                {
                    Id = TestIds.Park, NameKey = "ter.park", EnemyQueue = new[] { TestIds.CrowBoss, TestIds.Raccoon, TestIds.Pug }, FirstRewardFood = 20, FirstRewardJunk = 30,
                    Bonus = TerritoryBonusKind.TrainingXpPct, BonusValue = 10, ViewKey = "park",
                },
                new TerritoryDefinition
                {
                    Id = TestIds.Rooftop, NameKey = "ter.rooftop", EnemyQueue = new[] { TestIds.Admiral }, FirstRewardFood = 40, FirstRewardJunk = 40,
                    IsFinal = true, ViewKey = "rooftop",
                },
            },
        };

        private static ActivityDefinition Training(ActivityId id, string nameKey, AttributeType attribute) => new ActivityDefinition
        {
            Id = id, NameKey = nameKey, Kind = ActivityKind.Train, TrainedAttribute = attribute,
            DurationMs = 30_000, EnergyCost = 15, SatietyCost = 14, BaseOutput = 10, Capacity = 1,
        };

        private static UpgradeDefinition Station(UpgradeId id, string nameKey, AttributeType attribute) => new UpgradeDefinition
        {
            Id = id, NameKey = nameKey, Costs = new[] { 20, 60 },
            Effect = new TrainingXpBonusEffect { Attribute = attribute, PctPerLevel = new[] { 0, 20, 40 } },
        };

        private static EnemyDefinition Enemy(EnemyId id, string nameKey, int str, int agi, int tou, params SkillId[] skills) => new EnemyDefinition
        {
            Id = id, NameKey = nameKey, Strength = str, Technique = agi, Endurance = tou, Skills = skills, ViewKey = id.Value.Replace("enemy_", ""),
        };
    }
}
