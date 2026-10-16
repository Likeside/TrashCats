using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>
    /// Greedy scripted player for balance checks (roadmap M1-13). Fights are not simulated yet (M3/M4): they are
    /// assumed won at the expected times from PROGRESSION_AND_ECONOMY §6, granting their rewards, recruits and costs.
    /// </summary>
    public sealed class EconomyBot
    {
        public sealed class ScriptedFight
        {
            public string Name;
            public long AtMs;
            public int Food;
            public int Junk;
            public CatId? Recruit;
        }

        public sealed class Snapshot
        {
            public long TimeMs;
            public int Food;
            public int Junk;
            public string Upgrades;
            public string BeforeFight;
            public Dictionary<CatId, (int Str, int Agi, int Tou)> Levels = new Dictionary<CatId, (int, int, int)>();
        }

        private static readonly UpgradeId[] UpgradePriority = { new UpgradeId("upg_can"), new UpgradeId("upg_boxes"), new UpgradeId("upg_dumpster"), new UpgradeId("upg_street"), new UpgradeId("upg_mouse"), new UpgradeId("upg_wheel") };

        private readonly GameConfig _config;
        private readonly GameWorld _world;
        private readonly List<DomainEvent> _events = new List<DomainEvent>();
        private readonly Dictionary<ActivityKind, long> _busyMs = new Dictionary<ActivityKind, long>();
        private readonly List<string> _log = new List<string>();

        public List<Snapshot> Snapshots { get; } = new List<Snapshot>();
        public IReadOnlyList<string> Log => _log;
        public GameWorld World => _world;
        public int InvariantViolations { get; private set; }
        public long IdleCatMs { get; private set; }

        public EconomyBot(GameConfig config, int seed = 1)
        {
            _config = config;
            _world = GameWorld.CreateNew(config, seed);
        }

        public static IReadOnlyList<ScriptedFight> ExpectedFights(GameConfig config) => new[]
        {
            new ScriptedFight { Name = "Задний двор", AtMs = Min(4), Food = 10, Junk = 10, Recruit = new CatId("cat_shnyr") },
            new ScriptedFight { Name = "Рыбная лавка", AtMs = Min(12), Food = 15, Junk = 15 },
            new ScriptedFight { Name = "Тёмный переулок", AtMs = Min(21), Food = 15, Junk = 25, Recruit = new CatId("cat_ponchik") },
            new ScriptedFight { Name = "Городской парк", AtMs = Min(31), Food = 20, Junk = 30 },
            new ScriptedFight { Name = "Крыша", AtMs = Min(40), Food = 40, Junk = 40 },
        };

        public void Run(long durationMs, long snapshotEveryMs, IReadOnlyList<ScriptedFight> fights)
        {
            var pending = new Queue<ScriptedFight>(fights.OrderBy(f => f.AtMs));
            TakeSnapshot();

            for (var t = 1000L; t <= durationMs; t += 1000)
            {
                Simulation.AdvanceTo(_world, t, _events);
                Account();

                while (pending.Count > 0 && pending.Peek().AtMs <= t)
                {
                    var fight = pending.Dequeue();
                    TakeSnapshot(fight.Name);
                    Fight(fight);
                }

                Decide();
                IdleCatMs += 1000 * _world.Cats.Count(c => !c.Activity.IsWorking);

                if (WorldInvariants.Check(_world).Count > 0)
                    InvariantViolations++;
                if (t % snapshotEveryMs == 0)
                    TakeSnapshot();
            }
        }

        public sealed class FightRecord
        {
            public long AtMs;
            public string Territory;
            public bool Victory;
            public int TeamPower;
            public int EnemyPower;
        }

        public List<FightRecord> Fights { get; } = new List<FightRecord>();

        /// <summary>Seconds a battle replay takes for the player (the world keeps running meanwhile).</summary>
        public const long BattleReplayMs = 45_000;

        /// <summary>
        /// Full run with real battles (M5-09): plays like a sensible player and attacks the next territory when the
        /// rested team is roughly as strong as the enemies (or after waiting a while since a loss). Stops at the final
        /// victory or <paramref name="maxMs"/>. Returns the time of the final victory, or null.
        /// </summary>
        public long? RunWithBattles(long maxMs, double readiness = 0.9, long retryAfterLossMs = 240_000)
        {
            TakeSnapshot();
            var lastLoss = long.MinValue / 2;
            for (var t = 1000L; t <= maxMs; t += 1000)
            {
                Simulation.AdvanceTo(_world, t, _events);
                Account();
                Decide();
                EquipSkills();
                IdleCatMs += 1000 * _world.Cats.Count(c => !c.Activity.IsWorking);
                if (WorldInvariants.Check(_world).Count > 0)
                    InvariantViolations++;
                if (t % 300_000 == 0)
                    TakeSnapshot();

                var target = _config.Territories.FirstOrDefault(x => _world.Territories.Get(x.Id).Status == TerritoryStatus.Available);
                if (target == null)
                    continue;
                var team = _world.Cats.Where(c => c.Energy >= 50)
                    .OrderByDescending(c => AttributeTypes.All.Sum(c.GetLevel)).Take(Battles.MaxTeamSize).ToList();
                var teamPower = team.Sum(c => AttributeTypes.All.Sum(c.GetLevel));
                var enemyPower = target.EnemyQueue.Select(_config.GetEnemy).Sum(e => (e.Strength + e.Technique + e.Endurance) * e.HpMultiplierPermille / 1000);
                var ready = teamPower >= enemyPower * readiness || t - lastLoss >= retryAfterLossMs && Fights.Any(f => f.Territory == target.Id.Value);
                if (!ready || team.Count < Math.Min(_world.Cats.Count, Battles.MaxTeamSize) || t - lastLoss < 60_000)
                    continue;

                var events = new List<DomainEvent>();
                var battle = Battles.Start(_world, target.Id, team.Select(c => c.Id).ToList(), events);
                var victory = battle.Result.Outcome == BattleOutcome.Victory;
                Fights.Add(new FightRecord { AtMs = t, Territory = target.Id.Value, Victory = victory, TeamPower = teamPower, EnemyPower = enemyPower });
                _log.Add($"{Format(t)} бой {target.Id}: {(victory ? "победа" : "поражение")} (сила {teamPower} против {enemyPower})");
                t += BattleReplayMs;
                Simulation.AdvanceTo(_world, t, _events);
                Battles.Complete(_world, events);
                Account();
                if (!victory) lastLoss = t;
                if (victory && target.IsFinal)
                    return t;
            }

            return null;
        }

        /// <summary>Puts unlocked skills into free slots (damage skills first).</summary>
        private void EquipSkills()
        {
            var events = new List<DomainEvent>();
            foreach (var cat in _world.Cats)
            {
                var unlocked = _config.Skills.Where(s => !s.IsInnate && CatRules.IsSkillUnlocked(cat, s))
                    .OrderByDescending(s => s.IsHeavyAttack).ToList();
                foreach (var skill in unlocked)
                {
                    if (Enumerable.Range(0, Cat.SkillSlotCount).Any(i => Equals(cat.GetSkillInSlot(i), skill.Id)))
                        continue;
                    var free = Enumerable.Range(0, Cat.SkillSlotCount).Where(i => cat.GetSkillInSlot(i) == null).DefaultIfEmpty(-1).First();
                    if (free < 0 || CatRules.CanEquip(cat, free, skill.Id, _config) != SkillEquipError.None)
                        break;
                    CatRules.Equip(cat, free, skill.Id, _config, events);
                }
            }
        }

        public IReadOnlyDictionary<ActivityKind, long> BusyMs => _busyMs;

        private void Account()
        {
            foreach (var e in _events.OfType<ActivityCycleCompleted>())
            {
                _busyMs.TryGetValue(e.Kind, out var ms);
                _busyMs[e.Kind] = ms + _config.GetActivity(e.ActivityId).DurationMs;
            }

            _events.Clear();
        }

        private void Fight(ScriptedFight fight)
        {
            var events = new List<DomainEvent>();
            foreach (var cat in _world.Cats)
            {
                ActivitySystem.Stop(cat, IdleReason.AfterBattle, events);
                CatRules.ChangeNeeds(cat, -30, -10, _config, events);
                foreach (var attribute in AttributeTypes.All)
                    CatRules.AddXp(cat, attribute, _config.Progression.BattleXpWin, _config, events);
            }

            _world.Wallet.Add(ResourceType.Food, fight.Food, events);
            _world.Wallet.Add(ResourceType.Junk, fight.Junk, events);
            if (fight.Recruit != null)
                RecruitForTest(fight.Recruit.Value);

            _log.Add($"{Format(_world.GameTimeMs)} бой «{fight.Name}»: +{fight.Food} еды, +{fight.Junk} хлама{(fight.Recruit != null ? $", вербовка {fight.Recruit}" : "")}");
        }

        private void RecruitForTest(CatId id)
        {
            // Recruitment arrives with territories in M4; until then the bot rebuilds the world with the new cat.
            var cats = _world.Cats.ToList();
            cats.Add(CatFactory.Create(_config.GetCat(id), _config));
            var field = typeof(GameWorld).GetField("_cats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ((List<Cat>)field.GetValue(_world)).Add(cats.Last());
        }

        private void Decide()
        {
            var events = new List<DomainEvent>();

            foreach (var cat in _world.Cats)
                if (cat.Satiety < 30 && Feeding.CanFeed(cat, _world.Wallet, _config) == FeedError.None)
                    Feeding.Feed(cat, _world.Wallet, _config, events);

            BuyUpgrades(events);

            foreach (var cat in _world.Cats)
            {
                if (cat.Activity.IsWorking)
                    continue;
                var activity = ChooseActivity(cat);
                if (activity != null && ActivitySystem.CanStart(_world, cat, activity.Value) == ActivityError.None)
                    ActivitySystem.Start(_world, cat, activity.Value, events);
            }
        }

        private void BuyUpgrades(List<DomainEvent> events)
        {
            for (var level = 0; level < 2; level++)
            {
                foreach (var id in UpgradePriority)
                {
                    if (_world.Upgrades.GetLevel(id) != level)
                        continue;
                    if (Hideout.CanBuy(_world.Upgrades, _world.Wallet, _config, id) != UpgradeError.None)
                        return;
                    Hideout.Buy(_world.Upgrades, _world.Wallet, _config, id, events);
                    _log.Add($"{Format(_world.GameTimeMs)} улучшение {id} → {level + 1}");
                }
            }
        }

        private ActivityId? ChooseActivity(Cat cat)
        {
            if (CatRules.IsTired(cat, _config))
                return Id(ActivityKind.Sleep);
            if (_world.Food < 5 && !AnyoneDoing(ActivityKind.Scavenge))
                return Id(ActivityKind.Scavenge);
            if (CatRules.IsHungry(cat, _config) && _world.Food == 0)
                return Id(ActivityKind.Scavenge);
            if (!AnyoneDoing(ActivityKind.Rummage) && _world.Junk < NextUpgradeCost() && cat.Id == JunkCollector())
                return Id(ActivityKind.Rummage);

            // Specialise in the aptitude, but catch up an attribute that falls 3+ levels behind (a sensible player).
            var weakest = AttributeTypes.All.OrderBy(cat.GetLevel).First();
            var focus = cat.GetLevel(cat.Aptitude) - cat.GetLevel(weakest) >= 3 ? weakest : cat.Aptitude;
            var training = _config.Activities.Where(a => a.Kind == ActivityKind.Train)
                .Where(a => ActivitySystem.CanStart(_world, cat, a.Id) == ActivityError.None)
                .OrderByDescending(a => a.TrainedAttribute == focus)
                .ThenBy(a => cat.GetLevel(a.TrainedAttribute))
                .FirstOrDefault();
            if (training != null)
                return training.Id;

            return AnyoneDoing(ActivityKind.Rummage) ? Id(ActivityKind.Scavenge) : Id(ActivityKind.Rummage);
        }

        /// <summary>
        /// The cat furthest ahead in its aptitude attribute collects junk (a lone cat only after the first fight),
        /// so weaker cats keep training.
        /// </summary>
        private CatId? JunkCollector()
        {
            if (_world.Cats.Count == 1 && _world.GameTimeMs <= Min(4))
                return null;
            return _world.Cats.OrderByDescending(c => c.GetLevel(c.Aptitude)).First().Id;
        }

        private int NextUpgradeCost()
        {
            var costs = _config.Upgrades.Select(u => Hideout.NextCost(_world.Upgrades, u)).Where(c => c != null).Select(c => c.Value).ToList();
            return costs.Count == 0 ? 0 : costs.Min();
        }

        private bool AnyoneDoing(ActivityKind kind) =>
            _world.Cats.Any(c => c.Activity.IsWorking && _config.GetActivity(c.Activity.ActivityId).Kind == kind);

        private ActivityId Id(ActivityKind kind) => _config.Activities.First(a => a.Kind == kind).Id;

        private void TakeSnapshot(string beforeFight = null)
        {
            var snapshot = new Snapshot
            {
                BeforeFight = beforeFight,
                TimeMs = _world.GameTimeMs,
                Food = _world.Food,
                Junk = _world.Junk,
                Upgrades = string.Join(" ", UpgradePriority.Select(id => $"{id.Value.Replace("upg_", "")}{_world.Upgrades.GetLevel(id)}")),
            };
            foreach (var cat in _world.Cats)
                snapshot.Levels[cat.Id] = (cat.GetLevel(AttributeType.Strength), cat.GetLevel(AttributeType.Technique), cat.GetLevel(AttributeType.Endurance));
            Snapshots.Add(snapshot);
        }

        public static long Min(double minutes) => (long)(minutes * 60_000);
        public static string Format(long ms) => $"{ms / 60_000:00}:{ms / 1000 % 60:00}";
    }
}
