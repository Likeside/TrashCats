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
                Territories = world.Territories.All.ToDictionary(p => p.Key.Value,
                    p => new TerritorySave { Status = p.Value.Status.ToString(), Attempts = p.Value.Attempts }),
                PendingBattle = ToSave(world.PendingBattle),
                FinalReached = world.FinalReached,
                Tutorial = new TutorialSave { Completed = world.Tutorial.Completed.OrderBy(s => s).ToList(), Skipped = world.Tutorial.Skipped },
            };
        }

        private static PendingBattleSave ToSave(PendingBattle battle)
        {
            if (battle == null) return null;
            FighterSave F(FighterSnapshot f) => new FighterSave
            {
                Id = f.Id, Strength = f.Strength, Agility = f.Technique, Toughness = f.Endurance,
                Skills = f.Skills.Select(k => k.Value).ToArray(), HpMultiplierPermille = f.HpMultiplierPermille, ViewKey = f.ViewKey,
            };
            return new PendingBattleSave
            {
                Territory = battle.Territory.Value,
                Order = battle.Order.Select(c => c.Value).ToArray(),
                Seed = battle.Seed,
                Players = battle.Setup.PlayerQueue.Select(F).ToList(),
                Enemies = battle.Setup.EnemyQueue.Select(F).ToList(),
            };
        }

        private static PendingBattle FromSave(PendingBattleSave save, GameConfig config, List<string> warnings)
        {
            if (save == null) return null;
            try
            {
                var territory = new TerritoryId(save.Territory);
                if (!config.TryGetTerritory(territory, out _)) throw new SaveCorruptedException($"unknown territory {save.Territory}");
                FighterSnapshot F(FighterSave f, Side side) => new FighterSnapshot(f.Id, side, f.Strength, f.Agility, f.Toughness,
                    (f.Skills ?? new string[0]).Select(k => new SkillId(k)).Where(k => config.TryGetSkill(k, out _)), f.HpMultiplierPermille, f.ViewKey);
                var setup = new BattleSetup(save.Players.Select(f => F(f, Side.Player)), save.Enemies.Select(f => F(f, Side.Enemy)));
                var order = save.Order.Select(c => new CatId(c)).ToArray();
                return new PendingBattle(territory, order, save.Seed, setup, BattleSimulator.Simulate(setup, save.Seed, config));
            }
            catch (Exception e)
            {
                warnings.Add($"Pending battle dropped: {e.Message}");
                return null;
            }
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

            var territories = new List<KeyValuePair<TerritoryId, TerritoryState>>();
            foreach (var pair in data.Territories ?? new Dictionary<string, TerritorySave>())
            {
                if (string.IsNullOrEmpty(pair.Key) || !config.TryGetTerritory(new TerritoryId(pair.Key), out var definition))
                {
                    warnings.Add($"Unknown territory '{pair.Key}' dropped.");
                    continue;
                }

                Enum.TryParse(pair.Value?.Status, out TerritoryStatus status);
                territories.Add(new KeyValuePair<TerritoryId, TerritoryState>(definition.Id, new TerritoryState(status, pair.Value?.Attempts ?? 0)));
            }

            var pending = FromSave(data.PendingBattle, config, warnings);
            var inBattle = new HashSet<CatId>(pending?.Order ?? new CatId[0]);
            if (pending != null && pending.Order.Any(id => cats.All(c => c.Id != id)))
            {
                warnings.Add("Pending battle references a cat that is not in the gang; dropped.");
                pending = null;
                inBattle.Clear();
            }

            foreach (var cat in cats)
            {
                if (inBattle.Contains(cat.Id))
                    ForceInBattle(cat);
            }

            var world = new GameWorld(config, gameTime, data.SaveSeed, cats,
                new Wallet(Clamp(data.Food, 0, int.MaxValue, "Food", warnings), Clamp(data.Junk, 0, int.MaxValue, "Junk", warnings)),
                new UpgradeLevels(upgrades), new TerritoryProgress(config, territories), pending, data.FinalReached,
                new TutorialState(data.Tutorial?.Completed, data.Tutorial?.Skipped ?? false));

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
            if (save != null && save.State == nameof(ActivityStatus.InBattle))
                return ActivityState.Idle(IdleReason.None); // re-attached to the pending battle below, if it still exists

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

            // A broken cycle (unknown activity, impossible times): the cat simply stands idle.
            warnings.Add($"{id}: activity state '{save.State}/{save.ActivityId}' reset to idle.");
            return ActivityState.Idle(IdleReason.None);
        }

        private static void ForceInBattle(Cat cat)
        {
            // A cat restored for a pending battle: its saved activity was reset to idle, the battle owns it now.
            var events = new List<DomainEvent>();
            ActivitySystem.Stop(cat, IdleReason.None, events);
            BattleRestore.MarkInBattle(cat);
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
