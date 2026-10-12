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
        public static readonly SkillId DirtyTrick = new SkillId("skill_dirty_trick");
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
            Progression = new ProgressionConfig(),
            Start = new StartConfig { Food = 10, Junk = 0, Cats = new[] { TestIds.Ryzhy } },
            Cats = new[]
            {
                new CatDefinition
                {
                    Id = TestIds.Ryzhy, NameKey = "cat.ryzhy", Aptitude = AttributeType.Strength,
                    StartStrength = 3, StartAgility = 2, StartToughness = 2, StartEnergy = 100, StartSatiety = 60,
                    ViewKey = "ryzhy",
                },
                new CatDefinition
                {
                    Id = TestIds.Shnyr, NameKey = "cat.shnyr", Aptitude = AttributeType.Agility,
                    StartStrength = 3, StartAgility = 5, StartToughness = 3, StartEnergy = 100, StartSatiety = 50,
                    ViewKey = "shnyr",
                },
                new CatDefinition
                {
                    Id = TestIds.Ponchik, NameKey = "cat.ponchik", Aptitude = AttributeType.Toughness,
                    StartStrength = 5, StartAgility = 4, StartToughness = 7, StartEnergy = 100, StartSatiety = 50,
                    ViewKey = "ponchik",
                },
            },
            Activities = new[]
            {
                new ActivityDefinition
                {
                    Id = TestIds.Scavenge, NameKey = "act.scavenge", Kind = ActivityKind.Scavenge,
                    DurationMs = 20_000, EnergyCost = 10, SatietyCost = 4, BaseOutput = 3,
                },
                new ActivityDefinition
                {
                    Id = TestIds.Rummage, NameKey = "act.rummage", Kind = ActivityKind.Rummage,
                    DurationMs = 25_000, EnergyCost = 10, SatietyCost = 4, BaseOutput = 3,
                },
                Training(TestIds.TrainStr, "act.train_str", AttributeType.Strength),
                Training(TestIds.TrainAgi, "act.train_agi", AttributeType.Agility),
                Training(TestIds.TrainTou, "act.train_tou", AttributeType.Toughness),
                new ActivityDefinition
                {
                    Id = TestIds.Sleep, NameKey = "act.sleep", Kind = ActivityKind.Sleep,
                    DurationMs = 15_000, EnergyCost = 0, SatietyCost = 0, BaseOutput = 25,
                },
            },
            Upgrades = new[]
            {
                new UpgradeDefinition
                {
                    Id = TestIds.Boxes, NameKey = "upg.boxes", Costs = new[] { 15, 40 },
                    Effect = new SleepEnergyBonusEffect { BonusPerLevel = new[] { 0, 10, 20 } },
                },
                new UpgradeDefinition
                {
                    Id = TestIds.Dumpster, NameKey = "upg.dumpster", Costs = new[] { 15, 40 },
                    Effect = new ResourceOutputBonusEffect { FoodPerLevel = new[] { 0, 1, 2 }, JunkPerLevel = new[] { 0, 1, 2 } },
                },
                Station(TestIds.Can, "upg.can", AttributeType.Strength),
                Station(TestIds.Mouse, "upg.mouse", AttributeType.Agility),
                Station(TestIds.Wheel, "upg.wheel", AttributeType.Toughness),
            },
            Skills = new[]
            {
                new SkillDefinition { Id = TestIds.Scratch, NameKey = "skill.scratch", IsInnate = true },
                Skill(TestIds.Bite, "skill.bite", AttributeType.Strength, 4),
                Skill(TestIds.DoublePaw, "skill.double_paw", AttributeType.Agility, 4),
                Skill(TestIds.ThickFur, "skill.thick_fur", AttributeType.Toughness, 4),
                Skill(TestIds.Dodge, "skill.dodge", AttributeType.Agility, 7),
                Skill(TestIds.DirtyTrick, "skill.dirty_trick", AttributeType.Strength, 7),
            },
        };

        private static ActivityDefinition Training(ActivityId id, string nameKey, AttributeType attribute) => new ActivityDefinition
        {
            Id = id, NameKey = nameKey, Kind = ActivityKind.Train, TrainedAttribute = attribute,
            DurationMs = 30_000, EnergyCost = 15, SatietyCost = 10, BaseOutput = 10, Capacity = 1,
        };

        private static UpgradeDefinition Station(UpgradeId id, string nameKey, AttributeType attribute) => new UpgradeDefinition
        {
            Id = id, NameKey = nameKey, Costs = new[] { 15, 40 },
            Effect = new TrainingXpBonusEffect { Attribute = attribute, PctPerLevel = new[] { 0, 25, 50 } },
        };

        private static SkillDefinition Skill(SkillId id, string nameKey, AttributeType attribute, int level) => new SkillDefinition
        {
            Id = id, NameKey = nameKey, UnlockAttribute = attribute, UnlockLevel = level,
        };
    }
}
