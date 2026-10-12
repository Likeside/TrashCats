using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>
    /// Immutable balance and content description. Built once at startup (from ScriptableObjects or test builders)
    /// and validated with <see cref="ConfigValidator"/>.
    /// </summary>
    public sealed class GameConfig
    {
        public NeedsConfig Needs { get; init; } = new NeedsConfig();
        public TimeConfig Time { get; init; } = new TimeConfig();
        public ProgressionConfig Progression { get; init; } = new ProgressionConfig();
        public StartConfig Start { get; init; } = new StartConfig();

        public IReadOnlyList<CatDefinition> Cats { get; init; } = Array.Empty<CatDefinition>();
        public IReadOnlyList<ActivityDefinition> Activities { get; init; } = Array.Empty<ActivityDefinition>();
        public IReadOnlyList<UpgradeDefinition> Upgrades { get; init; } = Array.Empty<UpgradeDefinition>();
        public IReadOnlyList<SkillDefinition> Skills { get; init; } = Array.Empty<SkillDefinition>();

        private Dictionary<CatId, CatDefinition> _cats;
        private Dictionary<ActivityId, ActivityDefinition> _activities;
        private Dictionary<UpgradeId, UpgradeDefinition> _upgrades;
        private Dictionary<SkillId, SkillDefinition> _skills;

        public CatDefinition GetCat(CatId id) => Get(ref _cats, Cats, d => d.Id, id);
        public ActivityDefinition GetActivity(ActivityId id) => Get(ref _activities, Activities, d => d.Id, id);
        public UpgradeDefinition GetUpgrade(UpgradeId id) => Get(ref _upgrades, Upgrades, d => d.Id, id);
        public SkillDefinition GetSkill(SkillId id) => Get(ref _skills, Skills, d => d.Id, id);

        public bool TryGetCat(CatId id, out CatDefinition def) => TryGet(ref _cats, Cats, d => d.Id, id, out def);
        public bool TryGetActivity(ActivityId id, out ActivityDefinition def) => TryGet(ref _activities, Activities, d => d.Id, id, out def);
        public bool TryGetUpgrade(UpgradeId id, out UpgradeDefinition def) => TryGet(ref _upgrades, Upgrades, d => d.Id, id, out def);
        public bool TryGetSkill(SkillId id, out SkillDefinition def) => TryGet(ref _skills, Skills, d => d.Id, id, out def);

        private static T Get<TId, T>(ref Dictionary<TId, T> cache, IReadOnlyList<T> list, Func<T, TId> key, TId id)
        {
            if (TryGet(ref cache, list, key, id, out var def))
                return def;
            throw new KeyNotFoundException($"{typeof(T).Name} '{id}' is not defined in GameConfig.");
        }

        private static bool TryGet<TId, T>(ref Dictionary<TId, T> cache, IReadOnlyList<T> list, Func<T, TId> key, TId id, out T def)
        {
            if (cache == null)
            {
                cache = new Dictionary<TId, T>(list.Count);
                foreach (var item in list)
                    cache[key(item)] = item;
            }

            return cache.TryGetValue(id, out def);
        }
    }

    public sealed class NeedsConfig
    {
        public int Max { get; init; } = 100;
        public int TiredThreshold { get; init; } = 20;
        public int HungryThreshold { get; init; } = 20;
        public int HungryDurationMultiplier { get; init; } = 2;
        public int SatietyPerFood { get; init; } = 10;
        public int MinEnergyForBattle { get; init; } = 20;
    }

    public sealed class TimeConfig
    {
        public long OfflineCapMs { get; init; } = 60L * 60 * 1000;
        public long OfflineSummaryMinMs { get; init; } = 60L * 1000;
    }

    public sealed class ProgressionConfig
    {
        public int MaxLevel { get; init; } = 10;
        public int XpBase { get; init; } = 10;
        public int XpPerLevel { get; init; } = 7;
        public int AptitudeBonusPct { get; init; } = 50;
        public int BattleXpWin { get; init; } = 8;
        public int BattleXpLoss { get; init; } = 3;

        /// <summary>Experience needed to go from <paramref name="level"/> to the next level.</summary>
        public int XpToNext(int level) => XpBase + XpPerLevel * (level - 1);
    }

    public sealed class StartConfig
    {
        public int Food { get; init; } = 10;
        public int Junk { get; init; }
        public IReadOnlyList<CatId> Cats { get; init; } = Array.Empty<CatId>();
    }
}
