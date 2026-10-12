using System;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.Infrastructure
{
    public sealed class InvalidConfigException : Exception
    {
        public InvalidConfigException(string message) : base(message)
        {
        }
    }

    /// <summary>Converts the designer asset into the immutable domain config and validates it.</summary>
    public static class ConfigLoader
    {
        public static GameConfig Load(GameConfigAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            GameConfig config;
            try
            {
                config = Map(asset);
            }
            catch (ArgumentException e)
            {
                throw new InvalidConfigException($"Game config '{asset.name}' has invalid data: {e.Message}");
            }

            var errors = ConfigValidator.Validate(config);
            if (errors.Count > 0)
                throw new InvalidConfigException($"Game config '{asset.name}' is invalid:\n" + string.Join("\n", errors));
            return config;
        }

        public static GameConfig Map(GameConfigAsset a) => new GameConfig
        {
            Needs = new NeedsConfig
            {
                Max = a.needsMax,
                TiredThreshold = a.tiredThreshold,
                HungryThreshold = a.hungryThreshold,
                HungryDurationMultiplier = a.hungryDurationMultiplier,
                SatietyPerFood = a.satietyPerFood,
                MinEnergyForBattle = a.minEnergyForBattle,
            },
            Time = new TimeConfig { OfflineCapMs = a.offlineCapMs, OfflineSummaryMinMs = a.offlineSummaryMinMs },
            Progression = new ProgressionConfig
            {
                MaxLevel = a.maxLevel,
                XpBase = a.xpBase,
                XpPerLevel = a.xpPerLevel,
                AptitudeBonusPct = a.aptitudeBonusPct,
                BattleXpWin = a.battleXpWin,
                BattleXpLoss = a.battleXpLoss,
            },
            Start = new StartConfig
            {
                Food = a.startFood,
                Junk = a.startJunk,
                Cats = a.startCats.Select(id => new CatId(id)).ToArray(),
            },
            Cats = a.cats.Select(c => new CatDefinition
            {
                Id = new CatId(c.id),
                NameKey = c.nameKey,
                Aptitude = c.aptitude,
                StartStrength = c.startStrength,
                StartAgility = c.startAgility,
                StartToughness = c.startToughness,
                StartEnergy = c.startEnergy,
                StartSatiety = c.startSatiety,
                ViewKey = c.viewKey,
            }).ToArray(),
            Activities = a.activities.Select(x => new ActivityDefinition
            {
                Id = new ActivityId(x.id),
                NameKey = x.nameKey,
                Kind = x.kind,
                DurationMs = x.durationMs,
                EnergyCost = x.energyCost,
                SatietyCost = x.satietyCost,
                BaseOutput = x.baseOutput,
                TrainedAttribute = x.trainedAttribute,
                Capacity = x.capacity,
            }).ToArray(),
            Upgrades = a.upgrades.Select(u => new UpgradeDefinition
            {
                Id = new UpgradeId(u.id),
                NameKey = u.nameKey,
                Costs = u.costs.ToArray(),
                Effect = MapEffect(u),
            }).ToArray(),
            Skills = a.skills.Select(s => new SkillDefinition
            {
                Id = new SkillId(s.id),
                NameKey = s.nameKey,
                IsInnate = s.isInnate,
                UnlockAttribute = s.unlockAttribute,
                UnlockLevel = s.unlockLevel,
                GritCost = s.gritCost,
                CooldownActions = s.cooldownActions,
                Effect = s.effect,
                DamagePermille = s.damagePermille,
                HitModifierPermille = s.hitModifierPermille,
                Hits = s.hits,
                EffectPermille = s.effectPermille,
                EffectCount = s.effectCount,
            }).ToArray(),
            Combat = new CombatConfig
            {
                TickMs = a.combat.tickMs,
                HpBase = a.combat.hpBase,
                HpPerToughness = a.combat.hpPerToughness,
                GritBase = a.combat.gritBase,
                GritPerToughness = a.combat.gritPerToughness,
                GritRegenPerAction = a.combat.gritRegenPerAction,
                DamageBase = a.combat.damageBase,
                DamagePerStrength = a.combat.damagePerStrength,
                IntervalBaseTicks = a.combat.intervalBaseTicks,
                IntervalAgilityDivisor = a.combat.intervalAgilityDivisor,
                IntervalMinTicks = a.combat.intervalMinTicks,
                HitBasePermille = a.combat.hitBasePermille,
                HitPerAgilityDiffPermille = a.combat.hitPerAgilityDiffPermille,
                HitMinPermille = a.combat.hitMinPermille,
                HitMaxPermille = a.combat.hitMaxPermille,
                VarianceMinPermille = a.combat.varianceMinPermille,
                VarianceMaxPermille = a.combat.varianceMaxPermille,
                SuddenDeathAfterTicks = a.combat.suddenDeathAfterTicks,
                SuddenDeathPermille = a.combat.suddenDeathPermille,
                FighterEntryPauseTicks = a.combat.fighterEntryPauseTicks,
                SafetyMaxTicks = a.combat.safetyMaxTicks,
            },
            Enemies = a.enemies.Select(e => new EnemyDefinition
            {
                Id = new EnemyId(e.id),
                NameKey = e.nameKey,
                Strength = e.strength,
                Agility = e.agility,
                Toughness = e.toughness,
                Skills = e.skills.Select(id => new SkillId(id)).ToArray(),
                HpMultiplierPermille = e.hpMultiplierPermille,
                ViewKey = e.viewKey,
            }).ToArray(),
            Territories = a.territories.Select(t => new TerritoryDefinition
            {
                Id = new TerritoryId(t.id),
                NameKey = t.nameKey,
                EnemyQueue = t.enemyQueue.Select(id => new EnemyId(id)).ToArray(),
                FirstRewardFood = t.firstRewardFood,
                FirstRewardJunk = t.firstRewardJunk,
                RepeatRewardPct = t.repeatRewardPct,
                Bonus = t.bonus,
                BonusValue = t.bonusValue,
                RecruitCat = string.IsNullOrEmpty(t.recruitCat) ? (CatId?)null : new CatId(t.recruitCat),
                IsFinal = t.isFinal,
                ViewKey = t.viewKey,
            }).ToArray(),
        };

        private static UpgradeEffect MapEffect(GameConfigAsset.UpgradeEntry u) => u.effect switch
        {
            GameConfigAsset.UpgradeEffectType.SleepEnergyBonus => new SleepEnergyBonusEffect { BonusPerLevel = u.valuesPerLevel.ToArray() },
            GameConfigAsset.UpgradeEffectType.ResourceOutputBonus => new ResourceOutputBonusEffect { FoodPerLevel = u.foodPerLevel.ToArray(), JunkPerLevel = u.junkPerLevel.ToArray() },
            _ => new TrainingXpBonusEffect { Attribute = u.attribute, PctPerLevel = u.valuesPerLevel.ToArray() },
        };
    }
}
