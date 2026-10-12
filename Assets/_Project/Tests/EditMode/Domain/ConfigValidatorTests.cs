using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class ConfigValidatorTests
    {
        [Test]
        public void DefaultConfig_IsValid()
        {
            Assert.That(ConfigValidator.Validate(TestConfigs.Default()), Is.Empty);
        }

        [Test]
        public void DuplicateActivityId_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var config = With(baseConfig, activities: baseConfig.Activities.Append(baseConfig.Activities[0]).ToArray());

            AssertHasError(config, "Duplicate Activity id 'act_scavenge'");
        }

        [Test]
        public void StartCatMissingFromCats_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var config = With(baseConfig, start: new StartConfig { Food = 10, Cats = new[] { new CatId("cat_unknown") } });

            AssertHasError(config, "unknown cat 'cat_unknown'");
        }

        [Test]
        public void ZeroDuration_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var broken = new ActivityDefinition { Id = new ActivityId("act_broken"), Kind = ActivityKind.Train, BaseOutput = 1, DurationMs = 0 };
            var config = With(baseConfig, activities: baseConfig.Activities.Append(broken).ToArray());

            AssertHasError(config, "'act_broken': DurationMs must be positive");
        }

        [Test]
        public void UpgradeEffectLevelMismatch_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var broken = new UpgradeDefinition
            {
                Id = new UpgradeId("upg_broken"), Costs = new[] { 10, 20 },
                Effect = new SleepEnergyBonusEffect { BonusPerLevel = new[] { 0, 5 } },
            };
            var config = With(baseConfig, upgrades: baseConfig.Upgrades.Append(broken).ToArray());

            AssertHasError(config, "'upg_broken': effect must define 3 values");
        }

        [Test]
        public void CatStartLevelAboveMax_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var broken = new CatDefinition { Id = new CatId("cat_broken"), StartStrength = 11 };
            var config = With(baseConfig, cats: baseConfig.Cats.Append(broken).ToArray());

            AssertHasError(config, "'cat_broken': start Strength 11");
        }

        [Test]
        public void MissingSleepActivity_IsReported()
        {
            var baseConfig = TestConfigs.Default();
            var config = With(baseConfig, activities: baseConfig.Activities.Where(a => a.Kind != ActivityKind.Sleep).ToArray());

            AssertHasError(config, "Exactly one Sleep activity");
        }

        [Test]
        public void Lookup_ReturnsDefinition_AndThrowsForUnknown()
        {
            var config = TestConfigs.Default();

            Assert.That(config.GetActivity(TestIds.Sleep).Kind, Is.EqualTo(ActivityKind.Sleep));
            Assert.That(config.TryGetCat(new CatId("cat_unknown"), out _), Is.False);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => config.GetSkill(new SkillId("skill_unknown")));
        }

        [Test]
        public void XpToNext_FollowsGddCurve()
        {
            var progression = TestConfigs.Default().Progression;
            var expected = new[] { 10, 22, 34, 46, 58, 70, 82, 94, 106 };

            Assert.That(Enumerable.Range(1, 9).Select(progression.XpToNext), Is.EqualTo(expected));
            Assert.That(expected.Sum(), Is.EqualTo(522));
        }

        private static void AssertHasError(GameConfig config, string fragment)
        {
            var errors = ConfigValidator.Validate(config);
            Assert.That(errors.Any(e => e.Contains(fragment)), Is.True,
                $"Expected an error containing \"{fragment}\", got:\n{string.Join("\n", errors)}");
        }

        private static GameConfig With(GameConfig c,
            StartConfig start = null,
            CatDefinition[] cats = null,
            ActivityDefinition[] activities = null,
            UpgradeDefinition[] upgrades = null) => new GameConfig
        {
            Needs = c.Needs, Time = c.Time, Progression = c.Progression,
            Start = start ?? c.Start,
            Cats = cats ?? c.Cats,
            Activities = activities ?? c.Activities,
            Upgrades = upgrades ?? c.Upgrades,
            Skills = c.Skills,
        };
    }
}
