using System;
using System.Collections.Generic;
using System.Linq;

namespace TrashCats.Domain
{
    public enum Side
    {
        Player = 0,
        Enemy = 1,
    }

    public enum BattleOutcome
    {
        Victory = 0,
        Defeat = 1,
    }

    public enum CombatStatus
    {
        /// <summary>Thick Fur: landed hits deal reduced damage (value = charges).</summary>
        Fur = 0,

        /// <summary>Dodge: next enemy attack misses (value = charges).</summary>
        Evade = 1,

        /// <summary>Dirty Trick on this fighter: lower accuracy (value = actions left).</summary>
        Blinded = 2,
    }

    /// <summary>Immutable description of one fighter at the moment the battle starts.</summary>
    public sealed class FighterSnapshot
    {
        public string Id { get; }
        public Side Side { get; }
        public int Strength { get; }
        public int Agility { get; }
        public int Toughness { get; }
        public IReadOnlyList<SkillId> Skills { get; }
        public int HpMultiplierPermille { get; }
        public string ViewKey { get; }

        public FighterSnapshot(string id, Side side, int strength, int agility, int toughness, IEnumerable<SkillId> skills,
            int hpMultiplierPermille = 1000, string viewKey = null)
        {
            Id = id;
            Side = side;
            Strength = strength;
            Agility = agility;
            Toughness = toughness;
            Skills = (skills ?? Enumerable.Empty<SkillId>()).ToArray();
            HpMultiplierPermille = hpMultiplierPermille;
            ViewKey = viewKey ?? id;
        }

        /// <summary>A gang cat with its equipped skills (Scratch is innate and always available).</summary>
        public static FighterSnapshot FromCat(IReadOnlyCat cat, GameConfig config)
        {
            var skills = new List<SkillId>();
            for (var slot = 0; slot < Cat.SkillSlotCount; slot++)
                if (cat.GetSkillInSlot(slot) is SkillId id)
                    skills.Add(id);
            return new FighterSnapshot(cat.Id.Value, Side.Player,
                cat.GetLevel(AttributeType.Strength), cat.GetLevel(AttributeType.Agility), cat.GetLevel(AttributeType.Toughness),
                skills, 1000, config.GetCat(cat.Id).ViewKey);
        }

        public static FighterSnapshot FromEnemy(EnemyDefinition enemy) =>
            new FighterSnapshot(enemy.Id.Value, Side.Enemy, enemy.Strength, enemy.Agility, enemy.Toughness,
                enemy.Skills, enemy.HpMultiplierPermille, enemy.ViewKey);
    }

    public sealed class BattleSetup
    {
        public IReadOnlyList<FighterSnapshot> PlayerQueue { get; }
        public IReadOnlyList<FighterSnapshot> EnemyQueue { get; }

        public BattleSetup(IEnumerable<FighterSnapshot> playerQueue, IEnumerable<FighterSnapshot> enemyQueue)
        {
            PlayerQueue = playerQueue.ToArray();
            EnemyQueue = enemyQueue.ToArray();
            if (PlayerQueue.Count == 0 || EnemyQueue.Count == 0)
                throw new ArgumentException("Both sides need at least one fighter.");
        }
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }

        /// <summary>Player fighters that actually entered the ring.</summary>
        public IReadOnlyList<string> FoughtPlayerIds { get; }

        public int TotalTicks { get; }
        public IReadOnlyList<BattleEvent> Log { get; }

        public BattleResult(BattleOutcome outcome, IReadOnlyList<string> foughtPlayerIds, int totalTicks, IReadOnlyList<BattleEvent> log)
        {
            Outcome = outcome;
            FoughtPlayerIds = foughtPlayerIds;
            TotalTicks = totalTicks;
            Log = log;
        }
    }

    // --- Battle log: everything Presentation needs to replay a battle without the simulator ---

    public abstract class BattleEvent
    {
        public int Tick { get; }

        protected BattleEvent(int tick) => Tick = tick;
    }

    public sealed class FighterEntered : BattleEvent
    {
        public Side Side { get; }
        public string FighterId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Grit { get; }
        public int MaxGrit { get; }

        public FighterEntered(int tick, Side side, string fighterId, int hp, int maxHp, int grit, int maxGrit) : base(tick)
        {
            Side = side;
            FighterId = fighterId;
            Hp = hp;
            MaxHp = maxHp;
            Grit = grit;
            MaxGrit = maxGrit;
        }

        public override string ToString() => $"{Tick}: enter {Side} {FighterId} hp {Hp}/{MaxHp} grit {Grit}/{MaxGrit}";
    }

    public sealed class ActionPerformed : BattleEvent
    {
        public Side Side { get; }
        public string FighterId { get; }
        public SkillId SkillId { get; }
        public int GritAfter { get; }

        public ActionPerformed(int tick, Side side, string fighterId, SkillId skillId, int gritAfter) : base(tick)
        {
            Side = side;
            FighterId = fighterId;
            SkillId = skillId;
            GritAfter = gritAfter;
        }

        public override string ToString() => $"{Tick}: {Side} {FighterId} uses {SkillId} (grit {GritAfter})";
    }

    public sealed class HitResolved : BattleEvent
    {
        public Side AttackerSide { get; }
        public int HitIndex { get; }
        public bool Hit { get; }
        public int Damage { get; }
        public int TargetHpAfter { get; }
        public bool Evaded { get; }
        public bool Reduced { get; }

        public HitResolved(int tick, Side attackerSide, int hitIndex, bool hit, int damage, int targetHpAfter, bool evaded, bool reduced) : base(tick)
        {
            AttackerSide = attackerSide;
            HitIndex = hitIndex;
            Hit = hit;
            Damage = damage;
            TargetHpAfter = targetHpAfter;
            Evaded = evaded;
            Reduced = reduced;
        }

        public override string ToString() =>
            $"{Tick}: {AttackerSide} hit#{HitIndex} {(Hit ? $"-{Damage} -> {TargetHpAfter}" : Evaded ? "evaded" : "miss")}{(Reduced ? " (fur)" : "")}";
    }

    public sealed class StatusChanged : BattleEvent
    {
        public Side Side { get; }
        public CombatStatus Status { get; }
        public int Value { get; }

        public StatusChanged(int tick, Side side, CombatStatus status, int value) : base(tick)
        {
            Side = side;
            Status = status;
            Value = value;
        }

        public override string ToString() => $"{Tick}: {Side} {Status}={Value}";
    }

    public sealed class SuddenDeathStarted : BattleEvent
    {
        public SuddenDeathStarted(int tick) : base(tick)
        {
        }

        public override string ToString() => $"{Tick}: sudden death";
    }

    public sealed class FighterKnockedOut : BattleEvent
    {
        public Side Side { get; }
        public string FighterId { get; }

        public FighterKnockedOut(int tick, Side side, string fighterId) : base(tick)
        {
            Side = side;
            FighterId = fighterId;
        }

        public override string ToString() => $"{Tick}: KO {Side} {FighterId}";
    }

    public sealed class BattleEnded : BattleEvent
    {
        public BattleOutcome Outcome { get; }

        public BattleEnded(int tick, BattleOutcome outcome) : base(tick) => Outcome = outcome;

        public override string ToString() => $"{Tick}: end {Outcome}";
    }
}
