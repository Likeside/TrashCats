using System;
using System.Collections.Generic;
using UnityEngine;
using TrashCats.Domain;

namespace TrashCats.Infrastructure
{
    /// <summary>
    /// Designer-facing balance and content data. One asset holds everything for the prototype so balance changes are a
    /// single reviewable diff. Converted into an immutable <see cref="GameConfig"/> by <see cref="ConfigLoader"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "TrashCats/Game Config")]
    public sealed class GameConfigAsset : ScriptableObject
    {
        [Header("Needs")]
        public int needsMax = 100;
        public int tiredThreshold = 20;
        public int hungryThreshold = 20;
        public int hungryDurationMultiplier = 2;
        public int satietyPerFood = 10;
        public int minEnergyForBattle = 20;

        [Header("Time")]
        public long offlineCapMs = 3_600_000;
        public long offlineSummaryMinMs = 60_000;

        [Header("Progression")]
        public int maxLevel = 10;
        public int xpBase = 10;
        public int xpPerLevel = 7;
        public int aptitudeBonusPct = 50;
        public int battleXpWin = 8;
        public int battleXpLoss = 3;

        [Header("Start")]
        public int startFood = 10;
        public int startJunk;
        public List<string> startCats = new List<string>();

        [Header("Content")]
        public List<CatEntry> cats = new List<CatEntry>();
        public List<ActivityEntry> activities = new List<ActivityEntry>();
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();
        public List<SkillEntry> skills = new List<SkillEntry>();

        [Serializable]
        public sealed class CatEntry
        {
            public string id;
            public string nameKey;
            public AttributeType aptitude;
            public int startStrength = 1;
            public int startAgility = 1;
            public int startToughness = 1;
            public int startEnergy = 100;
            public int startSatiety = 60;
            public string viewKey;
        }

        [Serializable]
        public sealed class ActivityEntry
        {
            public string id;
            public string nameKey;
            public ActivityKind kind;
            public long durationMs;
            public int energyCost;
            public int satietyCost;
            public int baseOutput;
            public AttributeType trainedAttribute;
            [Tooltip("0 = unlimited")] public int capacity;
        }

        public enum UpgradeEffectType
        {
            SleepEnergyBonus,
            ResourceOutputBonus,
            TrainingXpBonus,
        }

        [Serializable]
        public sealed class UpgradeEntry
        {
            public string id;
            public string nameKey;
            [Tooltip("Junk cost of level 1, 2, ...")] public int[] costs = Array.Empty<int>();
            public UpgradeEffectType effect;
            [Tooltip("Per level including level 0: Sleep energy bonus or training XP %")] public int[] valuesPerLevel = Array.Empty<int>();
            [Tooltip("ResourceOutputBonus only: extra Food per level including 0")] public int[] foodPerLevel = Array.Empty<int>();
            [Tooltip("ResourceOutputBonus only: extra Junk per level including 0")] public int[] junkPerLevel = Array.Empty<int>();
            [Tooltip("TrainingXpBonus only")] public AttributeType attribute;
        }

        [Serializable]
        public sealed class SkillEntry
        {
            public string id;
            public string nameKey;
            public bool isInnate;
            public AttributeType unlockAttribute;
            public int unlockLevel;
        }
    }
}
