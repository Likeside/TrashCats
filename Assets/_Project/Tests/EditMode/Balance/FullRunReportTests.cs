using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>
    /// Full playthrough without acceleration (M5-09): the bot plays with real battles over 10 save seeds and writes
    /// TestResults/full-run.md (time to the final boss, battles, losses). A report, not a gate: the GDD target is 30–45 min.
    /// </summary>
    [Category("Balance")]
    public sealed class FullRunReportTests
    {
        [Test]
        public void TenPlaythroughs_ReachTheBoss_AndReportTime()
        {
            var config = TestConfigs.Default();
            var runs = new List<(int Seed, long? FinalMs, EconomyBot Bot)>();
            for (var seed = 1; seed <= 10; seed++)
            {
                var bot = new EconomyBot(config, seed);
                runs.Add((seed, bot.RunWithBattles(EconomyBot.Min(120)), bot));
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Полное прохождение ботом (M5-09)\n");
            sb.AppendLine("Бот играет без ускорения: кормит, тренирует по склонности, качает умения, покупает улучшения; нападает, когда отдохнувшая банда примерно равна врагам по сумме статов (или через 4 мин после поражения). Каждый бой — настоящий симулятор, просмотр боя +45 с.\n");
            sb.AppendLine("| Сид | До босса | Боёв | Поражений | Порядок захватов (мин) |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var (seed, final, bot) in runs)
            {
                var wins = string.Join(", ", bot.Fights.Where(f => f.Victory).Select(f => $"{f.Territory.Replace("ter_", "")} {f.AtMs / 60000.0:0.0}"));
                sb.AppendLine($"| {seed} | {(final != null ? EconomyBot.Format(final.Value) : "не дошёл")} | {bot.Fights.Count} | {bot.Fights.Count(f => !f.Victory)} | {wins} |");
            }

            var times = runs.Where(r => r.FinalMs != null).Select(r => r.FinalMs.Value / 60000.0).OrderBy(x => x).ToList();
            if (times.Count > 0)
                sb.AppendLine($"\n**Медиана: {times[times.Count / 2]:0.0} мин**, разброс {times.First():0.0}–{times.Last():0.0} мин. Цель GDD: 30–45 мин.");
            var byTerritory = runs.SelectMany(r => r.Bot.Fights).GroupBy(f => f.Territory)
                .Select(g => $"{g.Key.Replace("ter_", "")}: {g.Count(f => !f.Victory)} пораж. из {g.Count()}");
            sb.AppendLine("\nПоражения по территориям (все прогоны): " + string.Join("; ", byTerritory));

            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Path.Combine("TestResults", "full-run.md"), sb.ToString());
            TestContext.WriteLine(sb.ToString());

            Assert.That(runs.All(r => r.Bot.InvariantViolations == 0));
            Assert.That(times.Count, Is.EqualTo(10), "every run reaches the final boss within 2 hours");
        }
    }
}
