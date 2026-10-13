using System.Linq;
using System.Text;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>Full textual dump of a config, used to prove two configs carry the same balance.</summary>
    public static class ConfigDescription
    {
        public static string Of(GameConfig c)
        {
            var sb = new StringBuilder();
            var n = c.Needs;
            sb.AppendLine($"needs {n.Max} {n.TiredThreshold} {n.HungryThreshold} {n.HungryDurationMultiplier} {n.SatietyPerFood} {n.MinEnergyForBattle}");
            sb.AppendLine($"time {c.Time.OfflineCapMs} {c.Time.OfflineSummaryMinMs}");
            var p = c.Progression;
            sb.AppendLine($"progression {p.MaxLevel} {p.XpBase} {p.XpPerLevel} {p.AptitudeBonusPct} {p.BattleXpWin} {p.BattleXpLoss}");
            sb.AppendLine($"start {c.Start.Food} {c.Start.Junk} {string.Join(",", c.Start.Cats)}");
            foreach (var x in c.Cats)
                sb.AppendLine($"cat {x.Id} {x.NameKey} {x.Aptitude} {x.StartStrength}/{x.StartTechnique}/{x.StartEndurance} {x.StartEnergy} {x.StartSatiety} {x.ViewKey}");
            foreach (var x in c.Activities)
                sb.AppendLine($"activity {x.Id} {x.NameKey} {x.Kind} {x.DurationMs} {x.EnergyCost} {x.SatietyCost} {x.BaseOutput} {x.TrainedAttribute} {x.Capacity}");
            foreach (var x in c.Upgrades)
                sb.AppendLine($"upgrade {x.Id} {x.NameKey} [{string.Join(",", x.Costs)}] {Effect(x.Effect)}");
            foreach (var x in c.Skills)
                sb.AppendLine($"skill {x.Id} {x.NameKey} {x.IsInnate} {x.UnlockAttribute} {x.UnlockLevel} grit {x.GritCost} cd {x.CooldownActions} {x.Effect} dmg {x.DamagePermille} hit {x.HitModifierPermille} x{x.Hits} eff {x.EffectPermille}/{x.EffectCount}");
            var k = c.Combat;
            sb.AppendLine($"combat {k.TickMs} {k.HpBase} {k.HpPerEndurance} {k.GritBase} {k.GritPerEndurance} {k.GritRegenPerAction} {k.DamageBase} {k.DamagePerStrength} {k.IntervalBaseTicks} {k.IntervalTechniqueDivisor} {k.IntervalMinTicks} {k.HitBasePermille} {k.HitPerTechniqueDiffPermille} {k.HitMinPermille} {k.HitMaxPermille} {k.VarianceMinPermille} {k.VarianceMaxPermille} {k.SuddenDeathAfterTicks} {k.SuddenDeathPermille} {k.FighterEntryPauseTicks} {k.SafetyMaxTicks}");
            foreach (var x in c.Enemies)
                sb.AppendLine($"enemy {x.Id} {x.NameKey} {x.Strength}/{x.Technique}/{x.Endurance} [{string.Join(",", x.Skills)}] hp {x.HpMultiplierPermille} {x.ViewKey}");
            foreach (var x in c.Territories)
                sb.AppendLine($"territory {x.Id} {x.NameKey} [{string.Join(",", x.EnemyQueue)}] {x.FirstRewardFood}/{x.FirstRewardJunk} {x.RepeatRewardPct}% {x.Bonus}={x.BonusValue} recruit {x.RecruitCat?.Value ?? "-"} final {x.IsFinal} {x.ViewKey}");
            return sb.ToString();
        }

        private static string Effect(UpgradeEffect effect) => effect switch
        {
            SleepEnergyBonusEffect e => $"sleep[{string.Join(",", e.BonusPerLevel)}]",
            ResourceOutputBonusEffect e => $"resources food[{string.Join(",", e.FoodPerLevel)}] junk[{string.Join(",", e.JunkPerLevel)}]",
            TrainingXpBonusEffect e => $"training {e.Attribute}[{string.Join(",", e.PctPerLevel)}]",
            _ => "none",
        };
    }
}
