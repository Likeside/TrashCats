using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>
    /// Balance simulator (roadmap M3-07): win rate per territory over many seeds for expected gangs.
    /// Writes TestResults/combat-balance.md. Target (PROGRESSION_AND_ECONOMY §6): 50-70% at the expected time.
    /// </summary>
    [Category("Balance")]
    public sealed class CombatBalanceReportTests
    {
        private const int Seeds = 1000;

        public static readonly (string Name, TerritoryId Territory, (CatId Cat, int S, int A, int T)[] Gdd, (CatId Cat, int S, int A, int T)[] Bot)[] Fights =
        {
            ("Задний двор", TestIds.Backyard,
                new[] { (TestIds.Ryzhy, 4, 2, 2) },
                new[] { (TestIds.Ryzhy, 4, 2, 2) }),
            ("Рыбная лавка", TestIds.FishShop,
                new[] { (TestIds.Ryzhy, 6, 3, 3), (TestIds.Shnyr, 3, 6, 3) },
                new[] { (TestIds.Ryzhy, 5, 4, 2), (TestIds.Shnyr, 4, 5, 3) }),
            ("Тёмный переулок", TestIds.Alley,
                new[] { (TestIds.Ryzhy, 7, 3, 4), (TestIds.Shnyr, 4, 7, 4) },
                new[] { (TestIds.Ryzhy, 5, 4, 4), (TestIds.Shnyr, 4, 6, 3) }),
            ("Городской парк", TestIds.Park,
                new[] { (TestIds.Ryzhy, 8, 4, 5), (TestIds.Shnyr, 5, 8, 4), (TestIds.Ponchik, 6, 4, 8) },
                new[] { (TestIds.Ryzhy, 7, 4, 4), (TestIds.Shnyr, 4, 7, 5), (TestIds.Ponchik, 6, 4, 7) }),
            ("Крыша", TestIds.Rooftop,
                new[] { (TestIds.Ryzhy, 9, 4, 7), (TestIds.Shnyr, 5, 9, 5), (TestIds.Ponchik, 6, 5, 10) },
                new[] { (TestIds.Ryzhy, 8, 6, 5), (TestIds.Shnyr, 5, 8, 6), (TestIds.Ponchik, 7, 4, 7) }),
        };

        [Test]
        public void WinRates_Report()
        {
            var config = TestConfigs.Default();
            var sb = new StringBuilder();
            sb.AppendLine("# Баланс боя (M3-07)");
            sb.AppendLine();
            sb.AppendLine($"{Seeds} боёв на строку; умения — автоэкипировка открытых по порогам. Цель: 50–70% в ожидаемое время.");
            sb.AppendLine();
            sb.AppendLine("| Территория | Состав GDD | Победы | Состав бота (M1-13, сбалансированная стратегия) | Победы |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var fight in Fights)
            {
                var gdd = WinRate(config, fight.Territory, fight.Gdd);
                var bot = WinRate(config, fight.Territory, fight.Bot);
                sb.AppendLine($"| {fight.Name} | {Team(fight.Gdd)} | {gdd:P0} | {Team(fight.Bot)} | {bot:P0} |");
            }

            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Path.Combine("TestResults", "combat-balance.md"), sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        public static double WinRate(GameConfig config, TerritoryId territory, (CatId Cat, int S, int A, int T)[] team, int seeds = Seeds)
        {
            var players = team.Select(m => FighterSnapshot.FromCat(MakeCat(config, m), config)).ToArray();
            var enemies = config.GetTerritory(territory).EnemyQueue.Select(id => FighterSnapshot.FromEnemy(config.GetEnemy(id))).ToArray();
            var setup = new BattleSetup(players, enemies);
            var wins = 0;
            for (var seed = 0; seed < seeds; seed++)
                if (BattleSimulator.Simulate(setup, seed, config).Outcome == BattleOutcome.Victory)
                    wins++;
            return (double)wins / seeds;
        }

        public static Cat MakeCat(GameConfig config, (CatId Cat, int S, int A, int T) m)
        {
            var def = config.GetCat(m.Cat);
            var cat = new Cat(m.Cat, def.Aptitude, new[] { m.S, m.A, m.T }, new int[3], 100, 100, new SkillId?[Cat.SkillSlotCount], null);
            var slot = 0;
            foreach (var skill in config.Skills.Where(s => !s.IsInnate && CatRules.IsSkillUnlocked(cat, s)))
                if (slot < Cat.SkillSlotCount)
                    CatRules.Equip(cat, slot++, skill.Id, config, new System.Collections.Generic.List<DomainEvent>());
            return cat;
        }

        private static string Team((CatId Cat, int S, int A, int T)[] team) =>
            string.Join(", ", team.Select(m => $"{m.Cat.Value.Replace("cat_", "")} {m.S}/{m.A}/{m.T}"));
    }
}
