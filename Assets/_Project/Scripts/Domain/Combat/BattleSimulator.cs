using System;
using System.Collections.Generic;
using System.Linq;

namespace TrashCats.Domain
{
    /// <summary>Integer combat formulas (Docs/GDD/COMBAT_DESIGN.md §4).</summary>
    public static class CombatFormulas
    {
        public static int MaxHp(CombatConfig c, int toughness, int multiplierPermille = 1000) =>
            (int)((long)(c.HpBase + c.HpPerToughness * toughness) * multiplierPermille / 1000);

        public static int MaxGrit(CombatConfig c, int toughness) => c.GritBase + c.GritPerToughness * toughness;

        public static int BaseDamage(CombatConfig c, int strength) => c.DamageBase + c.DamagePerStrength * strength;

        public static int IntervalTicks(CombatConfig c, int agility) =>
            Math.Max(c.IntervalMinTicks, c.IntervalBaseTicks - agility / c.IntervalAgilityDivisor);

        public static int HitChancePermille(CombatConfig c, int attackerAgility, int defenderAgility, int modifierPermille) =>
            Math.Clamp(c.HitBasePermille + c.HitPerAgilityDiffPermille * (attackerAgility - defenderAgility) + modifierPermille,
                c.HitMinPermille, c.HitMaxPermille);

        /// <summary>Damage of one landed hit; every factor in permille, rounded once at the end, at least 1.</summary>
        public static int Damage(int baseDamage, params int[] permilleFactors)
        {
            long value = baseDamage * 1000L;
            foreach (var factor in permilleFactors)
                value = value * factor / 1000;
            return Math.Max(1, (int)((value + 500) / 1000));
        }
    }

    /// <summary>
    /// Deterministic relay battle: one fighter per side in the ring, the winner stays with its current HP and Grit.
    /// Pure function of (setup, seed, config); produces the full event log up front for Presentation to replay.
    /// </summary>
    public static class BattleSimulator
    {
        public static BattleResult Simulate(BattleSetup setup, int seed, GameConfig config)
        {
            return new Run(setup, seed, config).Execute();
        }

        private sealed class Fighter
        {
            public FighterSnapshot Snapshot;
            public Side Side;
            public int Hp, MaxHp, Grit, MaxGrit, Interval, Timer;
            public int Fur, Evade, Blinded;
            public readonly Dictionary<SkillId, int> Cooldowns = new Dictionary<SkillId, int>();
            public List<SkillDefinition> Skills;
            public SkillDefinition Innate;
        }

        private sealed class Run
        {
            private readonly BattleSetup _setup;
            private readonly GameConfig _config;
            private readonly CombatConfig _c;
            private readonly DeterministicRng _rng;
            private readonly List<BattleEvent> _log = new List<BattleEvent>();
            private readonly List<string> _fought = new List<string>();
            private readonly SkillDefinition _innate;
            private readonly int _furPermille;
            private readonly int _blindPenaltyPermille;
            private int _tick;
            private int _duelStart;
            private bool _suddenDeath;
            private int _pause;
            private int _nextPlayer, _nextEnemy;
            private Fighter _player, _enemy;

            public Run(BattleSetup setup, int seed, GameConfig config)
            {
                _setup = setup;
                _config = config;
                _c = config.Combat;
                _rng = new DeterministicRng(seed);
                _innate = config.Skills.FirstOrDefault(s => s.IsInnate && s.Effect == SkillEffectKind.Damage)
                          ?? throw new InvalidOperationException("Config needs an innate damage skill (Scratch).");
                _furPermille = config.Skills.FirstOrDefault(k => k.Effect == SkillEffectKind.DamageReduction)?.EffectPermille ?? 1000;
                _blindPenaltyPermille = config.Skills.FirstOrDefault(k => k.Effect == SkillEffectKind.AccuracyDebuff)?.EffectPermille ?? 0;
            }

            public BattleResult Execute()
            {
                _player = Enter(Side.Player);
                _enemy = Enter(Side.Enemy);
                _duelStart = 0;

                while (true)
                {
                    _tick++;
                    if (_tick > _c.SafetyMaxTicks)
                        return Finish(BattleOutcome.Defeat);

                    if (_pause > 0)
                    {
                        _pause--;
                        continue;
                    }

                    if (!_suddenDeath && _tick - _duelStart > _c.SuddenDeathAfterTicks)
                    {
                        _suddenDeath = true;
                        _log.Add(new SuddenDeathStarted(_tick));
                    }

                    _player.Timer--;
                    _enemy.Timer--;
                    foreach (var actor in ActingOrder())
                    {
                        var target = actor == _player ? _enemy : _player;
                        Act(actor, target);
                        if (target.Hp > 0)
                            continue;

                        _log.Add(new FighterKnockedOut(_tick, target.Side, target.Snapshot.Id));
                        var replacement = Enter(target.Side);
                        if (replacement == null)
                            return Finish(target.Side == Side.Enemy ? BattleOutcome.Victory : BattleOutcome.Defeat);

                        ResetWinner(actor);
                        if (target.Side == Side.Player) _player = replacement;
                        else _enemy = replacement;
                        _duelStart = _tick;
                        _suddenDeath = false;
                        _pause = _c.FighterEntryPauseTicks;
                        break;
                    }
                }
            }

            private IEnumerable<Fighter> ActingOrder()
            {
                var pReady = _player.Timer <= 0;
                var eReady = _enemy.Timer <= 0;
                if (pReady && eReady)
                {
                    var playerFirst = _player.Snapshot.Agility >= _enemy.Snapshot.Agility;
                    return playerFirst ? new[] { _player, _enemy } : new[] { _enemy, _player };
                }

                if (pReady) return new[] { _player };
                if (eReady) return new[] { _enemy };
                return Array.Empty<Fighter>();
            }

            private Fighter Enter(Side side)
            {
                var queue = side == Side.Player ? _setup.PlayerQueue : _setup.EnemyQueue;
                ref var next = ref side == Side.Player ? ref _nextPlayer : ref _nextEnemy;
                if (next >= queue.Count)
                    return null;

                var s = queue[next++];
                var f = new Fighter
                {
                    Snapshot = s,
                    Side = side,
                    MaxHp = CombatFormulas.MaxHp(_c, s.Toughness, s.HpMultiplierPermille),
                    MaxGrit = CombatFormulas.MaxGrit(_c, s.Toughness),
                    Interval = CombatFormulas.IntervalTicks(_c, s.Agility),
                    Skills = s.Skills.Select(_config.GetSkill).Where(k => !k.IsInnate).ToList(),
                    Innate = _innate,
                };
                f.Hp = f.MaxHp;
                f.Grit = f.MaxGrit;
                f.Timer = f.Interval;
                if (side == Side.Player)
                    _fought.Add(s.Id);
                _log.Add(new FighterEntered(_tick, side, s.Id, f.Hp, f.MaxHp, f.Grit, f.MaxGrit));
                return f;
            }

            private void ResetWinner(Fighter winner)
            {
                winner.Cooldowns.Clear();
                winner.Timer = winner.Interval;
                foreach (var (status, value) in new[] { (CombatStatus.Fur, winner.Fur), (CombatStatus.Evade, winner.Evade), (CombatStatus.Blinded, winner.Blinded) })
                    if (value > 0)
                        _log.Add(new StatusChanged(_tick, winner.Side, status, 0));
                winner.Fur = winner.Evade = winner.Blinded = 0;
            }

            private void Act(Fighter self, Fighter opponent)
            {
                self.Timer = self.Interval;
                foreach (var key in self.Cooldowns.Keys.ToList())
                    if (self.Cooldowns[key] > 0)
                        self.Cooldowns[key]--;

                var skill = CombatAi.Choose(self.Skills, self.Innate, Ready(self), SelfState(self), OpponentThreat(opponent));
                self.Grit -= skill.GritCost;
                if (skill.CooldownActions > 0)
                    self.Cooldowns[skill.Id] = skill.CooldownActions;
                self.Grit = Math.Min(self.MaxGrit, self.Grit + _c.GritRegenPerAction);
                _log.Add(new ActionPerformed(_tick, self.Side, self.Snapshot.Id, skill.Id, self.Grit));

                switch (skill.Effect)
                {
                    case SkillEffectKind.Damage:
                        Attack(self, opponent, skill);
                        break;
                    case SkillEffectKind.DamageReduction:
                        self.Fur = skill.EffectCount;
                        _log.Add(new StatusChanged(_tick, self.Side, CombatStatus.Fur, self.Fur));
                        break;
                    case SkillEffectKind.GuaranteedEvade:
                        self.Evade = skill.EffectCount;
                        _log.Add(new StatusChanged(_tick, self.Side, CombatStatus.Evade, self.Evade));
                        break;
                    case SkillEffectKind.AccuracyDebuff:
                        opponent.Blinded = skill.EffectCount;
                        _log.Add(new StatusChanged(_tick, opponent.Side, CombatStatus.Blinded, opponent.Blinded));
                        break;
                }

                // Dirty Trick lasts for the blinded fighter's next N actions of any kind.
                if (self.Blinded > 0 && skill.Effect != SkillEffectKind.AccuracyDebuff)
                {
                    self.Blinded--;
                    _log.Add(new StatusChanged(_tick, self.Side, CombatStatus.Blinded, self.Blinded));
                }
            }

            private void Attack(Fighter self, Fighter opponent, SkillDefinition skill)
            {
                var evaded = opponent.Evade > 0;
                if (evaded)
                {
                    opponent.Evade--;
                    for (var hit = 0; hit < skill.Hits; hit++)
                        _log.Add(new HitResolved(_tick, self.Side, hit, false, 0, opponent.Hp, true, false));
                    _log.Add(new StatusChanged(_tick, opponent.Side, CombatStatus.Evade, opponent.Evade));
                    return;
                }

                var blind = self.Blinded > 0 ? -_blindPenaltyPermille : 0;
                var chance = CombatFormulas.HitChancePermille(_c, self.Snapshot.Agility, opponent.Snapshot.Agility, skill.HitModifierPermille + blind);
                var baseDamage = CombatFormulas.BaseDamage(_c, self.Snapshot.Strength);
                for (var hit = 0; hit < skill.Hits && opponent.Hp > 0; hit++)
                {
                    if (!_rng.RollPermille(chance))
                    {
                        _log.Add(new HitResolved(_tick, self.Side, hit, false, 0, opponent.Hp, false, false));
                        continue;
                    }

                    var reduced = opponent.Fur > 0;
                    var variance = _rng.NextInt(_c.VarianceMinPermille, _c.VarianceMaxPermille + 1);
                    var damage = CombatFormulas.Damage(baseDamage, skill.DamagePermille, variance,
                        reduced ? _furPermille : 1000, _suddenDeath ? _c.SuddenDeathPermille : 1000);
                    opponent.Hp = Math.Max(0, opponent.Hp - damage);
                    _log.Add(new HitResolved(_tick, self.Side, hit, true, damage, opponent.Hp, false, reduced));
                    if (reduced)
                    {
                        opponent.Fur--;
                        _log.Add(new StatusChanged(_tick, opponent.Side, CombatStatus.Fur, opponent.Fur));
                    }
                }
            }

            private Func<SkillDefinition, bool> Ready(Fighter f) =>
                s => (!f.Cooldowns.TryGetValue(s.Id, out var cd) || cd <= 0) && f.Grit >= s.GritCost;

            private static CombatAi.SelfState SelfState(Fighter f) =>
                new CombatAi.SelfState(f.Hp * 1000 / Math.Max(1, f.MaxHp), f.Fur > 0, f.Evade > 0);

            private static CombatAi.Threat OpponentThreat(Fighter o)
            {
                var heavyReady = o.Skills.Any(s => s.IsHeavyAttack
                                                   && (!o.Cooldowns.TryGetValue(s.Id, out var cd) || cd <= 0)
                                                   && o.Grit >= s.GritCost);
                return new CombatAi.Threat(heavyReady, o.Blinded > 0);
            }

            private BattleResult Finish(BattleOutcome outcome)
            {
                _log.Add(new BattleEnded(_tick, outcome));
                return new BattleResult(outcome, _fought, _tick, _log);
            }
        }
    }

    /// <summary>Fixed-priority fighter AI shared by cats and enemies (Docs/GDD/COMBAT_DESIGN.md §6).</summary>
    public static class CombatAi
    {
        public readonly struct SelfState
        {
            public int HpPermille { get; }
            public bool FurActive { get; }
            public bool EvadeActive { get; }

            public SelfState(int hpPermille, bool furActive, bool evadeActive)
            {
                HpPermille = hpPermille;
                FurActive = furActive;
                EvadeActive = evadeActive;
            }
        }

        public readonly struct Threat
        {
            public bool HeavyAttackReady { get; }
            public bool AlreadyBlinded { get; }

            public Threat(bool heavyAttackReady, bool alreadyBlinded)
            {
                HeavyAttackReady = heavyAttackReady;
                AlreadyBlinded = alreadyBlinded;
            }
        }

        public const int FurHpThresholdPermille = 600;

        public static SkillDefinition Choose(IReadOnlyList<SkillDefinition> skills, SkillDefinition innate, Func<SkillDefinition, bool> ready,
            SelfState self, Threat opponent)
        {
            SkillDefinition First(Func<SkillDefinition, bool> match) => skills.FirstOrDefault(s => match(s) && ready(s));

            if (!self.FurActive && self.HpPermille <= FurHpThresholdPermille && First(s => s.Effect == SkillEffectKind.DamageReduction) is { } fur)
                return fur;
            if (!self.EvadeActive && opponent.HeavyAttackReady && First(s => s.Effect == SkillEffectKind.GuaranteedEvade) is { } dodge)
                return dodge;
            if (!opponent.AlreadyBlinded && First(s => s.Effect == SkillEffectKind.AccuracyDebuff) is { } trick)
                return trick;
            if (First(s => s.IsHeavyAttack) is { } heavy)
                return heavy;
            if (First(s => s.IsMultiHit) is { } multi)
                return multi;
            return innate;
        }
    }
}
