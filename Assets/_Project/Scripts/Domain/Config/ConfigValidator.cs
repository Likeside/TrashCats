using System.Collections.Generic;
using System.Linq;

namespace TrashCats.Domain
{
    /// <summary>Checks a <see cref="GameConfig"/> for inconsistencies. An empty result means the config is valid.</summary>
    public static class ConfigValidator
    {
        public static IReadOnlyList<string> Validate(GameConfig config)
        {
            var errors = new List<string>();
            ValidateGlobals(config, errors);
            ValidateCats(config, errors);
            ValidateActivities(config, errors);
            ValidateUpgrades(config, errors);
            ValidateSkills(config, errors);
            ValidateEnemies(config, errors);
            ValidateTerritories(config, errors);
            return errors;
        }

        private static void ValidateGlobals(GameConfig config, List<string> errors)
        {
            var needs = config.Needs;
            if (needs.Max <= 0) errors.Add("Needs.Max must be positive.");
            if (needs.TiredThreshold < 0 || needs.TiredThreshold > needs.Max) errors.Add("Needs.TiredThreshold must be within 0..Max.");
            if (needs.HungryThreshold < 0 || needs.HungryThreshold > needs.Max) errors.Add("Needs.HungryThreshold must be within 0..Max.");
            if (needs.HungryDurationMultiplier < 1) errors.Add("Needs.HungryDurationMultiplier must be >= 1.");
            if (needs.SatietyPerFood <= 0) errors.Add("Needs.SatietyPerFood must be positive.");

            if (config.Time.OfflineCapMs <= 0) errors.Add("Time.OfflineCapMs must be positive.");

            var progression = config.Progression;
            if (progression.MaxLevel < 1) errors.Add("Progression.MaxLevel must be >= 1.");
            if (progression.XpToNext(1) <= 0) errors.Add("Progression.XpBase must be positive.");
            if (progression.XpPerLevel < 0) errors.Add("Progression.XpPerLevel must not be negative.");

            if (config.Start.Food < 0 || config.Start.Junk < 0) errors.Add("Start resources must not be negative.");
            if (config.Start.Cats.Count == 0) errors.Add("Start.Cats must contain at least one cat.");
            foreach (var catId in config.Start.Cats)
                if (!config.TryGetCat(catId, out _)) errors.Add($"Start.Cats references unknown cat '{catId}'.");
        }

        private static void ValidateCats(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Cats.Select(c => c.Id.Value), "Cat", errors);
            foreach (var cat in config.Cats)
            {
                foreach (var attribute in AttributeTypes.All)
                {
                    var level = cat.GetStartLevel(attribute);
                    if (level < 1 || level > config.Progression.MaxLevel)
                        errors.Add($"Cat '{cat.Id}': start {attribute} {level} is outside 1..{config.Progression.MaxLevel}.");
                }

                if (cat.StartEnergy < 0 || cat.StartEnergy > config.Needs.Max) errors.Add($"Cat '{cat.Id}': StartEnergy out of range.");
                if (cat.StartSatiety < 0 || cat.StartSatiety > config.Needs.Max) errors.Add($"Cat '{cat.Id}': StartSatiety out of range.");
            }
        }

        private static void ValidateActivities(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Activities.Select(a => a.Id.Value), "Activity", errors);
            foreach (var activity in config.Activities)
            {
                if (activity.DurationMs <= 0) errors.Add($"Activity '{activity.Id}': DurationMs must be positive.");
                if (activity.EnergyCost < 0 || activity.SatietyCost < 0) errors.Add($"Activity '{activity.Id}': costs must not be negative.");
                if (activity.BaseOutput <= 0) errors.Add($"Activity '{activity.Id}': BaseOutput must be positive.");
                if (activity.Capacity < 0) errors.Add($"Activity '{activity.Id}': Capacity must not be negative.");
                if (activity.Kind == ActivityKind.Sleep && activity.EnergyCost != 0) errors.Add($"Activity '{activity.Id}': Sleep must not cost Energy.");
            }

            foreach (var kind in new[] { ActivityKind.Scavenge, ActivityKind.Rummage, ActivityKind.Sleep })
                if (config.Activities.Count(a => a.Kind == kind) != 1)
                    errors.Add($"Exactly one {kind} activity is expected.");
        }

        private static void ValidateUpgrades(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Upgrades.Select(u => u.Id.Value), "Upgrade", errors);
            foreach (var upgrade in config.Upgrades)
            {
                if (upgrade.MaxLevel == 0) errors.Add($"Upgrade '{upgrade.Id}': at least one level cost is required.");
                if (upgrade.Costs.Any(c => c <= 0)) errors.Add($"Upgrade '{upgrade.Id}': costs must be positive.");
                if (upgrade.Effect == null)
                    errors.Add($"Upgrade '{upgrade.Id}': Effect is missing.");
                else if (upgrade.Effect.LevelCount != upgrade.MaxLevel + 1)
                    errors.Add($"Upgrade '{upgrade.Id}': effect must define {upgrade.MaxLevel + 1} values (levels 0..{upgrade.MaxLevel}).");
            }
        }

        private static void ValidateSkills(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Skills.Select(s => s.Id.Value), "Skill", errors);
            foreach (var skill in config.Skills)
            {
                if (skill.IsInnate)
                    continue;
                if (skill.UnlockLevel < 1 || skill.UnlockLevel > config.Progression.MaxLevel)
                    errors.Add($"Skill '{skill.Id}': UnlockLevel must be within 1..{config.Progression.MaxLevel}.");
            }
        }

        private static void ValidateEnemies(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Enemies.Select(e => e.Id.Value), "Enemy", errors);
            foreach (var enemy in config.Enemies)
            {
                if (enemy.Strength < 1 || enemy.Technique < 1 || enemy.Endurance < 1) errors.Add($"Enemy '{enemy.Id}': attributes must be >= 1.");
                if (enemy.HpMultiplierPermille <= 0) errors.Add($"Enemy '{enemy.Id}': HpMultiplierPermille must be positive.");
                foreach (var skill in enemy.Skills)
                    if (!config.TryGetSkill(skill, out _)) errors.Add($"Enemy '{enemy.Id}' references unknown skill '{skill}'.");
            }

            if (config.Enemies.Count > 0 && !config.Skills.Any(s => s.IsInnate && s.Effect == SkillEffectKind.Damage))
                errors.Add("An innate damage skill (Scratch) is required for combat.");
        }

        private static void ValidateTerritories(GameConfig config, List<string> errors)
        {
            CheckUniqueIds(config.Territories.Select(t => t.Id.Value), "Territory", errors);
            foreach (var territory in config.Territories)
            {
                if (territory.EnemyQueue.Count == 0) errors.Add($"Territory '{territory.Id}': enemy queue is empty.");
                foreach (var enemy in territory.EnemyQueue)
                    if (!config.TryGetEnemy(enemy, out _)) errors.Add($"Territory '{territory.Id}' references unknown enemy '{enemy}'.");
                if (territory.RecruitCat is CatId cat && !config.TryGetCat(cat, out _))
                    errors.Add($"Territory '{territory.Id}' recruits unknown cat '{cat}'.");
                if (territory.FirstRewardFood < 0 || territory.FirstRewardJunk < 0) errors.Add($"Territory '{territory.Id}': rewards must not be negative.");
            }
        }

        private static void CheckUniqueIds(IEnumerable<string> ids, string kind, List<string> errors)
        {
            foreach (var group in ids.GroupBy(id => id).Where(g => g.Key == null || g.Count() > 1))
                errors.Add(group.Key == null ? $"{kind} with empty id." : $"Duplicate {kind} id '{group.Key}'.");
        }
    }
}
