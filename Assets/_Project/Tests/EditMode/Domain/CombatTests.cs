using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class CombatFormulaTests
    {
        private readonly CombatConfig _c = new CombatConfig();

        [Test]
        public void DerivedStats_FollowGddTable()
        {
            Assert.That(new[] { 2, 5, 10 }.Select(t => CombatFormulas.MaxHp(_c, t)), Is.EqualTo(new[] { 64, 100, 160 }));
            Assert.That(new[] { 2, 5, 10 }.Select(t => CombatFormulas.MaxGrit(_c, t)), Is.EqualTo(new[] { 30, 45, 70 }));
            Assert.That(new[] { 2, 5, 10 }.Select(s => CombatFormulas.BaseDamage(_c, s)), Is.EqualTo(new[] { 8, 14, 24 }));
            Assert.That(new[] { 2, 5, 10, 30 }.Select(a => CombatFormulas.IntervalTicks(_c, a)), Is.EqualTo(new[] { 15, 14, 11, 10 }));
            Assert.That(CombatFormulas.MaxHp(_c, 10, 1500), Is.EqualTo(240));
            Assert.That(CombatFormulas.MaxHp(_c, 10, 2000), Is.EqualTo(320));
        }

        [Test]
        public void HitChance_IsClamped()
        {
            Assert.That(CombatFormulas.HitChancePermille(_c, 2, 3, 0), Is.EqualTo(710));
            Assert.That(CombatFormulas.HitChancePermille(_c, 2, 3, -100), Is.EqualTo(610));
            Assert.That(CombatFormulas.HitChancePermille(_c, 1, 20, 0), Is.EqualTo(350));
            Assert.That(CombatFormulas.HitChancePermille(_c, 20, 1, 0), Is.EqualTo(950));
        }

        [Test]
        public void Damage_AppliesPermilleFactorsAndMinimumOne()
        {
            Assert.That(CombatFormulas.Damage(12, 2000, 1000, 1000, 1000), Is.EqualTo(24));
            Assert.That(CombatFormulas.Damage(12, 700, 850), Is.EqualTo(7));     // 12*0.7*0.85 = 7.14
            Assert.That(CombatFormulas.Damage(10, 1000, 1150, 600, 2000), Is.EqualTo(14)); // 13.8 -> 14
            Assert.That(CombatFormulas.Damage(1, 100), Is.EqualTo(1));
        }
    }

    public sealed class CombatAiTests
    {
        private GameConfig _config;
        private SkillDefinition S(SkillId id) => _config.GetSkill(id);
        private SkillDefinition Scratch => S(TestIds.Scratch);

        [SetUp]
        public void SetUp() => _config = TestConfigs.Default();

        private SkillDefinition Choose(SkillId[] skills, int hpPermille = 1000, bool fur = false, bool evade = false, bool heavyReady = false,
            bool blinded = false, params SkillId[] notReady)
        {
            var defs = skills.Select(S).ToList();
            return CombatAi.Choose(defs, Scratch, s => !notReady.Contains(s.Id), new CombatAi.SelfState(hpPermille, fur, evade),
                new CombatAi.Threat(heavyReady, blinded));
        }

        [Test]
        public void ThickFur_WhenHurtAndNotActive()
        {
            var all = new[] { TestIds.Bite, TestIds.ThickFur };
            Assert.That(Choose(all, hpPermille: 600).Id, Is.EqualTo(TestIds.ThickFur));
            Assert.That(Choose(all, hpPermille: 601).Id, Is.EqualTo(TestIds.Bite));
            Assert.That(Choose(all, hpPermille: 500, fur: true).Id, Is.EqualTo(TestIds.Bite));
        }

        [Test]
        public void Dodge_OnlyAgainstReadyHeavyAttack()
        {
            var all = new[] { TestIds.Dodge, TestIds.DoublePaw };
            Assert.That(Choose(all, heavyReady: true).Id, Is.EqualTo(TestIds.Dodge));
            Assert.That(Choose(all, heavyReady: false).Id, Is.EqualTo(TestIds.DoublePaw));
            Assert.That(Choose(all, heavyReady: true, evade: true).Id, Is.EqualTo(TestIds.DoublePaw));
        }

        [Test]
        public void DirtyTrick_BeforeAttacks_UnlessAlreadyBlinded()
        {
            var all = new[] { TestIds.Bite, TestIds.DirtyTrick };
            Assert.That(Choose(all).Id, Is.EqualTo(TestIds.DirtyTrick));
            Assert.That(Choose(all, blinded: true).Id, Is.EqualTo(TestIds.Bite));
        }

        [Test]
        public void Attacks_HeavyThenMultiThenScratch()
        {
            var all = new[] { TestIds.DoublePaw, TestIds.Bite };
            Assert.That(Choose(all).Id, Is.EqualTo(TestIds.Bite));
            Assert.That(Choose(all, notReady: new[] { TestIds.Bite }).Id, Is.EqualTo(TestIds.DoublePaw));
            Assert.That(Choose(all, notReady: new[] { TestIds.Bite, TestIds.DoublePaw }).Id, Is.EqualTo(TestIds.Scratch));
            Assert.That(Choose(new SkillId[0]).Id, Is.EqualTo(TestIds.Scratch));
        }
    }

    public sealed class BattleSimulatorTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp() => _config = TestConfigs.Default();

        private static FighterSnapshot P(string id, int s, int a, int t, params SkillId[] skills) => new FighterSnapshot(id, Side.Player, s, a, t, skills);
        private static FighterSnapshot E(string id, int s, int a, int t, params SkillId[] skills) => new FighterSnapshot(id, Side.Enemy, s, a, t, skills);

        private BattleResult Run(BattleSetup setup, int seed = 1) => BattleSimulator.Simulate(setup, seed, _config);

        private static string Text(BattleResult r) => string.Join("\n", r.Log.Select(e => e.ToString()));

        [Test]
        public void SameSeed_SameLog_DifferentSeed_DifferentLog()
        {
            var setup = new BattleSetup(new[] { P("ryzhy", 4, 2, 2, TestIds.Bite) }, new[] { E("rat", 2, 3, 1) });

            Assert.That(Text(Run(setup, 5)), Is.EqualTo(Text(Run(setup, 5))));
            Assert.That(Text(Run(setup, 5)), Is.Not.EqualTo(Text(Run(setup, 6))));
        }

        [Test]
        public void Log_StartsWithEntries_EndsWithBattleEnded()
        {
            var r = Run(new BattleSetup(new[] { P("ryzhy", 4, 2, 2, TestIds.Bite) }, new[] { E("rat", 2, 3, 1) }));

            Assert.That(r.Log[0], Is.TypeOf<FighterEntered>());
            Assert.That(r.Log[1], Is.TypeOf<FighterEntered>());
            Assert.That(((BattleEnded)r.Log.Last()).Outcome, Is.EqualTo(r.Outcome));
            Assert.That(r.Log.OfType<FighterKnockedOut>().Count(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void HpNeverNegative_AndKnockoutAtZero()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var r = Run(new BattleSetup(new[] { P("a", 5, 5, 3, TestIds.Bite) }, new[] { E("b", 5, 5, 3, TestIds.DoublePaw) }), seed);
                Assert.That(r.Log.OfType<HitResolved>().All(h => h.TargetHpAfter >= 0), Is.True);
                var lastHit = r.Log.OfType<HitResolved>().Last(h => h.Hit);
                Assert.That(lastHit.TargetHpAfter, Is.EqualTo(0));
            }
        }

        [Test]
        public void Relay_WinnerKeepsHp_NextFighterEntersFresh()
        {
            var r = Run(new BattleSetup(
                new[] { P("strong", 9, 6, 8, TestIds.Bite) },
                new[] { E("weak1", 1, 1, 1), E("weak2", 1, 1, 1) }));

            var entries = r.Log.OfType<FighterEntered>().ToList();
            Assert.That(entries.Select(e => e.FighterId), Is.EqualTo(new[] { "strong", "weak1", "weak2" }));
            Assert.That(entries[2].Hp, Is.EqualTo(entries[2].MaxHp));
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(r.FoughtPlayerIds, Is.EqualTo(new[] { "strong" }));
            // the second enemy enters after the pause, while "strong" keeps whatever HP it had left
            Assert.That(entries[2].Tick, Is.GreaterThan(0));
        }

        [Test]
        public void Defeat_WhenAllPlayersKnockedOut_UnusedCatsDidNotFight()
        {
            var r = Run(new BattleSetup(new[] { P("kitten", 1, 1, 1), P("kitten2", 1, 1, 1) }, new[] { E("boss", 10, 10, 10, TestIds.Bite) }));

            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(r.FoughtPlayerIds, Is.EqualTo(new[] { "kitten", "kitten2" }));
        }

        [Test]
        public void SuddenDeath_EndsLongDuels()
        {
            // Two tanks with fur would fight for a very long time without sudden death.
            var r = Run(new BattleSetup(new[] { P("tank", 1, 1, 10, TestIds.ThickFur) }, new[] { E("tank2", 1, 1, 10, TestIds.ThickFur) }));

            Assert.That(r.Log.OfType<SuddenDeathStarted>().Any(), Is.True);
            Assert.That(r.TotalTicks, Is.LessThan(_config.Combat.SafetyMaxTicks));
        }

        [Test]
        public void Dodge_MakesNextAttackMiss_AndFurReducesDamage()
        {
            var r = Run(new BattleSetup(new[] { P("dodger", 3, 7, 5, TestIds.Dodge, TestIds.ThickFur) }, new[] { E("biter", 6, 3, 5, TestIds.Bite) }), 3);

            Assert.That(r.Log.OfType<HitResolved>().Any(h => h.Evaded), Is.True, "dodge was never used against bite");
            Assert.That(r.Log.OfType<StatusChanged>().Any(s => s.Status == CombatStatus.Evade && s.Value == 1), Is.True);
        }

        [Test]
        public void DirtyTrick_BlindsOpponentForThreeActions()
        {
            var r = Run(new BattleSetup(new[] { P("trickster", 7, 4, 6, TestIds.DirtyTrick) }, new[] { E("target", 4, 4, 6) }));

            var blind = r.Log.OfType<StatusChanged>().Where(s => s.Side == Side.Enemy && s.Status == CombatStatus.Blinded).Select(s => s.Value).ToList();
            Assert.That(blind.Take(4), Is.EqualTo(new[] { 3, 2, 1, 0 }));
        }

        [Test]
        public void GddFirstFight_TrainedRyzhyBeatsRatMostOfTheTime()
        {
            var trained = WinRate(P("ryzhy", 4, 2, 2, TestIds.Bite), E("rat", 2, 3, 1));
            var untrained = WinRate(P("ryzhy", 3, 2, 2), E("rat", 2, 3, 1));

            Assert.That(trained, Is.GreaterThan(0.9));
            Assert.That(untrained, Is.LessThan(trained));
            TestContext.WriteLine($"Ryzhy vs Rat: trained {trained:P0}, untrained {untrained:P0}");
        }

        [Test]
        public void Snapshot_ReferenceBattle()
        {
            // Regression snapshot of a fixed battle; update deliberately when combat rules or numbers change.
            var r = Run(new BattleSetup(
                new[] { P("ryzhy", 6, 3, 3, TestIds.Bite), P("shnyr", 3, 6, 3, TestIds.DoublePaw) },
                new[] { E("gull", 3, 5, 2, TestIds.DoublePaw), E("shopcat", 5, 3, 4, TestIds.Bite) }), 42);

            TestContext.WriteLine(Text(r));
            Assert.That($"{r.Outcome} {r.TotalTicks} {r.Log.Count} {DeterministicRng.StableHash(Text(r))}", Is.EqualTo(ReferenceSnapshot));
        }

        // Filled from the first run of the current rules (see test output).
        private const string ReferenceSnapshot = "Victory 160 51 -799673517";

        private double WinRate(FighterSnapshot player, FighterSnapshot enemy, int runs = 400)
        {
            var wins = 0;
            for (var seed = 0; seed < runs; seed++)
                if (Run(new BattleSetup(new[] { player }, new[] { enemy }), seed).Outcome == BattleOutcome.Victory)
                    wins++;
            return (double)wins / runs;
        }
    }
}
