using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.App
{
    public sealed class SaveCorruptedException : Exception
    {
        public SaveCorruptedException(string message) : base(message)
        {
        }
    }

    /// <summary>Result of turning a <see cref="SaveData"/> into a live world.</summary>
    public sealed class LoadedGame
    {
        public GameWorld World { get; }
        public long LastTickUtcMs { get; }
        public SettingsSave Settings { get; }

        /// <summary>Recoverable problems that were repaired (unknown ids, out-of-range values...).</summary>
        public IReadOnlyList<string> Warnings { get; }

        public LoadedGame(GameWorld world, long lastTickUtcMs, SettingsSave settings, IReadOnlyList<string> warnings)
        {
            World = world;
            LastTickUtcMs = lastTickUtcMs;
            Settings = settings;
            Warnings = warnings;
        }
    }

    /// <summary>Maps between the domain world and save DTOs, repairing recoverable damage on load.</summary>
    public static class SaveMapper
    {
        public static SaveData ToSave(IReadOnlyGameWorld world, long savedAtUtcMs, SettingsSave settings)
        {
            return new SaveData
            {
                Version = SaveData.CurrentVersion,
                SavedAtUtcMs = savedAtUtcMs,
                GameTimeMs = world.GameTimeMs,
                SaveSeed = world.SaveSeed,
                Food = world.Food,
                Junk = world.Junk,
                Cats = world.Cats.Select(ToSave).ToList(),
                Upgrades = world.Config.Upgrades
                    .Where(u => world.GetUpgradeLevel(u.Id) > 0)
                    .ToDictionary(u => u.Id.Value, u => world.GetUpgradeLevel(u.Id)),
                Settings = settings ?? new SettingsSave(),
            };
        }

        /// <summary>Builds a world from a save. Throws <see cref="SaveCorruptedException"/> if it cannot be repaired.</summary>
        public static LoadedGame FromSave(SaveData data, GameConfig config)
        {
            if (data == null) throw new SaveCorruptedException("Save is empty.");

            var warnings = new List<string>();
            var gameTime = Math.Max(0, data.GameTimeMs);
            var cats = new List<Cat>();
            foreach (var catSave in data.Cats ?? new List<CatSave>())
            {
                var cat = FromSave(catSave, config, gameTime, warnings);
                if (cat == null) continue;
                if (cats.Any(c => c.Id == cat.Id))
                {
                    warnings.Add($"Duplicate cat {cat.Id} dropped.");
                    continue;
                }

                cats.Add(cat);
            }

            if (cats.Count == 0)
                throw new SaveCorruptedException("Save contains no known cats.");

            var upgrades = new List<KeyValuePair<UpgradeId, int>>();
            foreach (var pair in data.Upgrades ?? new Dictionary<string, int>())
            {
                if (string.IsNullOrEmpty(pair.Key) || !config.TryGetUpgrade(new UpgradeId(pair.Key), out var definition))
                {
                    warnings.Add($"Unknown upgrade '{pair.Key}' dropped.");
                    continue;
                }

                upgrades.Add(new KeyValuePair<UpgradeId, int>(definition.Id, Clamp(pair.Value, 0, definition.MaxLevel, $"upgrade {pair.Key}", warnings)));
            }

            var world = new GameWorld(config, gameTime, data.SaveSeed, cats,
                new Wallet(Clamp(data.Food, 0, int.MaxValue, "Food", warnings), Clamp(data.Junk, 0, int.MaxValue, "Junk", warnings)),
                new UpgradeLevels(upgrades));

            RepairStations(world, warnings);
            var violations = WorldInvariants.Check(world);
            if (violations.Count > 0)
                throw new SaveCorruptedException("Save is inconsistent: " + string.Join("; ", violations));

            return new LoadedGame(world, data.SavedAtUtcMs, data.Settings ?? new SettingsSave(), warnings);
        }

        private static CatSave ToSave(IReadOnlyCat cat)
        {
            var activity = cat.Activity;
            return new CatSave
            {
                Id = cat.Id.Value,
                Levels = AttributeTypes.All.Select(cat.GetLevel).ToArray(),
                Xp = AttributeTypes.All.Select(cat.GetXp).ToArray(),
                Energy = cat.Energy,
                Satiety = cat.Satiety,
                Skills = Enumerable.Range(0, Cat.SkillSlotCount).Select(i => cat.GetSkillInSlot(i)?.Value).ToArray(),
                Activity = new ActivitySave
                {
                    State = activity.Status.ToString(),
                    IdleReason = activity.Status == ActivityStatus.Idle ? activity.IdleReason.ToString() : null,
                    ActivityId = activity.IsWorking ? activity.ActivityId.Value : null,
                    CycleStartMs = activity.CycleStartMs,
                    CycleDurationMs = activity.CycleDurationMs,
                    HungryOverride = activity.HungryOverride,
                },
            };
        }

        private static Cat FromSave(CatSave save, GameConfig config, long gameTime, List<string> warnings)
        {
            if (save == null || string.IsNullOrEmpty(save.Id) || !config.TryGetCat(new CatId(save.Id), out var definition))
            {
                warnings.Add($"Unknown cat '{save?.Id}' dropped.");
                return null;
            }

            var id = definition.Id;
            var maxLevel = config.Progression.MaxLevel;
            var levels = new int[AttributeTypes.Count];
            var xp = new int[AttributeTypes.Count];
            foreach (var attribute in AttributeTypes.All)
            {
                var i = (int)attribute;
                levels[i] = Clamp(At(save.Levels, i, 1), 1, maxLevel, $"{id} {attribute} level", warnings);
                xp[i] = levels[i] >= maxLevel ? 0 : Clamp(At(save.Xp, i, 0), 0, config.Progression.XpToNext(levels[i]) - 1, $"{id} {attribute} XP", warnings);
            }

            var energy = Clamp(save.Energy, 0, config.Needs.Max, $"{id} Energy", warnings);
            var satiety = Clamp(save.Satiety, 0, config.Needs.Max, $"{id} Satiety", warnings);
            var activity = FromSave(save.Activity, config, gameTime, id, warnings);
            var cat = new Cat(id, definition.Aptitude, levels, xp, energy, satiety, new SkillId?[Cat.SkillSlotCount], activity);

            for (var slot = 0; slot < Cat.SkillSlotCount; slot++)
            {
                var raw = save.Skills != null && slot < save.Skills.Length ? save.Skills[slot] : null;
                if (string.IsNullOrEmpty(raw)) continue;
                var skillId = new SkillId(raw);
                if (CatRules.CanEquip(cat, slot, skillId, config) != SkillEquipError.None)
                {
                    warnings.Add($"{id}: skill '{raw}' in slot {slot} dropped.");
                    continue;
                }

                CatRules.Equip(cat, slot, skillId, config, new List<DomainEvent>());
            }

            return cat;
        }

        private static ActivityState FromSave(ActivitySave save, GameConfig config, long gameTime, CatId id, List<string> warnings)
        {
            if (save == null || save.State == nameof(ActivityStatus.Idle))
            {
                Enum.TryParse(save?.IdleReason, out IdleReason reason);
                return ActivityState.Idle(reason);
            }

            if (save.State == nameof(ActivityStatus.Working)
                && !string.IsNullOrEmpty(save.ActivityId)
                && config.TryGetActivity(new ActivityId(save.ActivityId), out var definition)
                && save.CycleDurationMs > 0
                && save.CycleStartMs <= gameTime
                && save.CycleStartMs + save.CycleDurationMs > gameTime)
            {
                return ActivityState.Working(definition.Id, save.CycleStartMs, save.CycleDurationMs, save.HungryOverride);
            }

            // InBattle without a pending battle (battles arrive in M4) or a broken cycle: the cat simply stands idle.
            warnings.Add($"{id}: activity state '{save.State}/{save.ActivityId}' reset to idle.");
            return ActivityState.Idle(IdleReason.None);
        }

        private static void RepairStations(GameWorld world, List<string> warnings)
        {
            var usage = new Dictionary<ActivityId, int>();
            foreach (var cat in world.Cats)
            {
                if (!cat.Activity.IsWorking) continue;
                var definition = world.Config.GetActivity(cat.Activity.ActivityId);
                usage.TryGetValue(definition.Id, out var count);
                if (definition.Capacity > 0 && count >= definition.Capacity)
                {
                    warnings.Add($"{cat.Id}: station {definition.Id} already taken, cat set idle.");
                    ActivitySystem.Stop(cat, IdleReason.None, new List<DomainEvent>());
                    continue;
                }

                usage[definition.Id] = count + 1;
            }
        }

        private static int At(int[] values, int index, int fallback) =>
            values != null && index < values.Length ? values[index] : fallback;

        private static int Clamp(int value, int min, int max, string what, List<string> warnings)
        {
            if (value >= min && value <= max) return value;
            warnings.Add($"{what} {value} clamped to {min}..{max}.");
            return Math.Clamp(value, min, max);
        }
    }
}
