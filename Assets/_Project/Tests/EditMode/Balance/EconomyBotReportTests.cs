using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>
    /// Runs the economy bot for 40 game minutes and writes TestResults/economy-bot.md.
    /// This is a report, not a balance gate: deviations from the GDD curve are discussed, not auto-fixed.
    /// </summary>
    [Category("Balance")]
    public sealed class EconomyBotReportTests
    {
        private static readonly (double Minute, string Name, (int, int, int) Ryzhy, (int, int, int)? Shnyr, (int, int, int)? Ponchik)[] Expected =
        {
            (4, "Задний двор", (4, 2, 2), null, null),
            (12, "Рыбная лавка", (6, 3, 3), (3, 6, 3), null),
            (21, "Тёмный переулок", (7, 3, 4), (4, 7, 4), null),
            (31, "Городской парк", (8, 4, 5), (5, 8, 4), (6, 4, 8)),
            (40, "Крыша", (9, 4, 7), (5, 9, 5), (6, 5, 10)),
        };

        [Test]
        public void FortyMinuteRun_ProducesReport_AndKeepsInvariants()
        {
            var config = TestConfigs.Shipped();
            var bot = new EconomyBot(config);
            var fights = EconomyBot.ExpectedFights(config);

            bot.Run(EconomyBot.Min(40), EconomyBot.Min(5), fights);

            var report = BuildReport(bot, config);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Path.Combine("TestResults", "economy-bot.md"), report);
            TestContext.WriteLine(report);

            Assert.That(bot.InvariantViolations, Is.Zero);
            Assert.That(bot.World.Cats.Count, Is.EqualTo(3));
        }

        private static string BuildReport(EconomyBot bot, GameConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Отчёт бота экономики (M1-13)");
            sb.AppendLine();
            sb.AppendLine("Жадная стратегия, бои засчитываются победами в ожидаемое время (PROGRESSION_AND_ECONOMY §6).");
            sb.AppendLine();
            sb.AppendLine("## Срез каждые 5 минут");
            sb.AppendLine();
            sb.AppendLine("| Время | Рыжий С/Л/К | Шнырь С/Л/К | Пончик С/Л/К | Еда | Хлам | Улучшения |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var s in bot.Snapshots.Where(x => x.BeforeFight == null))
                sb.AppendLine($"| {EconomyBot.Format(s.TimeMs)} | {Lv(s, "cat_ryzhy")} | {Lv(s, "cat_shnyr")} | {Lv(s, "cat_ponchik")} | {s.Food} | {s.Junk} | {s.Upgrades} |");

            sb.AppendLine();
            sb.AppendLine("## Ожидание GDD vs бот (перед боем)");
            sb.AppendLine();
            sb.AppendLine("| Бой | Мин | Рыжий ожид. / бот | Шнырь ожид. / бот | Пончик ожид. / бот |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var e in Expected)
            {
                var snap = bot.Snapshots.Single(s => s.BeforeFight == e.Name);
                sb.AppendLine($"| {e.Name} | {e.Minute} | {T(e.Ryzhy)} / {Lv(snap, "cat_ryzhy")} | {T(e.Shnyr)} / {Lv(snap, "cat_shnyr")} | {T(e.Ponchik)} / {Lv(snap, "cat_ponchik")} |");
            }

            var total = bot.BusyMs.Values.Sum();
            sb.AppendLine();
            sb.AppendLine("## Доля времени котов по занятиям (завершённые циклы)");
            sb.AppendLine();
            foreach (var kind in new[] { ActivityKind.Train, ActivityKind.Sleep, ActivityKind.Scavenge, ActivityKind.Rummage })
            {
                bot.BusyMs.TryGetValue(kind, out var ms);
                sb.AppendLine($"- {kind}: {100.0 * ms / total:0}%");
            }

            sb.AppendLine($"- Простой котов (суммарно): {bot.IdleCatMs / 60_000.0:0.0} кот-мин");
            sb.AppendLine();
            sb.AppendLine("## События");
            sb.AppendLine();
            foreach (var line in bot.Log)
                sb.AppendLine($"- {line}");
            return sb.ToString();
        }

        private static string Lv(EconomyBot.Snapshot s, string id) =>
            s.Levels.TryGetValue(new CatId(id), out var l) ? $"{l.Str}/{l.Agi}/{l.Tou}" : "—";

        private static string T((int, int, int)? v) => v == null ? "—" : $"{v.Value.Item1}/{v.Value.Item2}/{v.Value.Item3}";
    }
}
