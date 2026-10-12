using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>Consistency checks of a <see cref="GameWorld"/> (Docs/Architecture/DATA_MODELS.md §2). Empty result = consistent.</summary>
    public static class WorldInvariants
    {
        public static IReadOnlyList<string> Check(GameWorld world)
        {
            var errors = new List<string>();
            var config = world.Config;
            var max = config.Needs.Max;
            var maxLevel = config.Progression.MaxLevel;

            if (world.Wallet.Food < 0 || world.Wallet.Junk < 0)
                errors.Add($"Negative resources: Food {world.Wallet.Food}, Junk {world.Wallet.Junk}.");

            var ids = new HashSet<CatId>();
            var stationUse = new Dictionary<ActivityId, int>();
            foreach (var cat in world.Cats)
            {
                if (!ids.Add(cat.Id)) errors.Add($"Duplicate cat {cat.Id}.");
                if (cat.Energy < 0 || cat.Energy > max) errors.Add($"{cat.Id}: Energy {cat.Energy} out of range.");
                if (cat.Satiety < 0 || cat.Satiety > max) errors.Add($"{cat.Id}: Satiety {cat.Satiety} out of range.");

                foreach (var attribute in AttributeTypes.All)
                {
                    var level = cat.GetLevel(attribute);
                    var xp = cat.GetXp(attribute);
                    if (level < 1 || level > maxLevel) errors.Add($"{cat.Id}: {attribute} level {level} out of range.");
                    else if (level == maxLevel && xp != 0) errors.Add($"{cat.Id}: {attribute} has XP {xp} at max level.");
                    else if (level < maxLevel && (xp < 0 || xp >= config.Progression.XpToNext(level)))
                        errors.Add($"{cat.Id}: {attribute} XP {xp} out of range for level {level}.");
                }

                CheckSkills(cat, config, errors);
                CheckActivity(world, cat, stationUse, errors);
            }

            foreach (var pair in stationUse)
            {
                var capacity = config.GetActivity(pair.Key).Capacity;
                if (capacity > 0 && pair.Value > capacity)
                    errors.Add($"{pair.Key}: {pair.Value} cats on a station with capacity {capacity}.");
            }

            foreach (var upgrade in config.Upgrades)
            {
                var level = world.Upgrades.GetLevel(upgrade.Id);
                if (level < 0 || level > upgrade.MaxLevel) errors.Add($"{upgrade.Id}: level {level} out of range.");
            }

            return errors;
        }

        private static void CheckSkills(Cat cat, GameConfig config, List<string> errors)
        {
            var equipped = new HashSet<SkillId>();
            for (var slot = 0; slot < Cat.SkillSlotCount; slot++)
            {
                var skillId = cat.GetSkillInSlot(slot);
                if (skillId == null) continue;
                if (!equipped.Add(skillId.Value)) errors.Add($"{cat.Id}: skill {skillId} equipped twice.");
                if (!config.TryGetSkill(skillId.Value, out var skill)) errors.Add($"{cat.Id}: unknown skill {skillId}.");
                else if (skill.IsInnate || !CatRules.IsSkillUnlocked(cat, skill)) errors.Add($"{cat.Id}: skill {skillId} is not equippable.");
            }
        }

        private static void CheckActivity(GameWorld world, Cat cat, Dictionary<ActivityId, int> stationUse, List<string> errors)
        {
            var state = cat.Activity;
            if (!state.IsWorking) return;

            if (!world.Config.TryGetActivity(state.ActivityId, out _))
            {
                errors.Add($"{cat.Id}: unknown activity {state.ActivityId}.");
                return;
            }

            if (state.CycleDurationMs <= 0) errors.Add($"{cat.Id}: non-positive cycle duration.");
            if (state.CycleStartMs > world.GameTimeMs || state.CycleEndMs <= world.GameTimeMs)
                errors.Add($"{cat.Id}: cycle {state.CycleStartMs}..{state.CycleEndMs} does not contain time {world.GameTimeMs}.");

            stationUse.TryGetValue(state.ActivityId, out var count);
            stationUse[state.ActivityId] = count + 1;
        }
    }
}
