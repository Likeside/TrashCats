using System;
using System.Collections.Generic;
using System.Linq;

namespace TrashCats.Domain
{
    public enum TerritoryStatus
    {
        Locked = 0,
        Available = 1,
        Captured = 2,
    }

    public sealed class TerritoryState
    {
        public TerritoryStatus Status { get; internal set; }
        public int Attempts { get; internal set; }

        public TerritoryState(TerritoryStatus status, int attempts)
        {
            Status = status;
            Attempts = attempts;
        }
    }

    /// <summary>Unlock state of the territories; the first one in config order starts available.</summary>
    public sealed class TerritoryProgress
    {
        private readonly Dictionary<TerritoryId, TerritoryState> _states = new Dictionary<TerritoryId, TerritoryState>();

        public TerritoryProgress(GameConfig config, IEnumerable<KeyValuePair<TerritoryId, TerritoryState>> saved = null)
        {
            for (var i = 0; i < config.Territories.Count; i++)
                _states[config.Territories[i].Id] = new TerritoryState(i == 0 ? TerritoryStatus.Available : TerritoryStatus.Locked, 0);
            if (saved == null)
                return;
            foreach (var pair in saved)
                if (_states.ContainsKey(pair.Key))
                    _states[pair.Key] = new TerritoryState(pair.Value.Status, Math.Max(0, pair.Value.Attempts));
        }

        public IReadOnlyDictionary<TerritoryId, TerritoryState> All => _states;

        public TerritoryState Get(TerritoryId id) => _states[id];

        public bool IsPlayable(TerritoryId id) => _states.TryGetValue(id, out var s) && s.Status != TerritoryStatus.Locked;

        internal int RegisterAttempt(TerritoryId id) => ++_states[id].Attempts;

        internal void Capture(TerritoryId id, GameConfig config)
        {
            _states[id].Status = TerritoryStatus.Captured;
            var list = config.Territories;
            for (var i = 0; i < list.Count - 1; i++)
                if (list[i].Id == id && _states[list[i + 1].Id].Status == TerritoryStatus.Locked)
                    _states[list[i + 1].Id].Status = TerritoryStatus.Available;
        }

        public void Contribute(GameConfig config, ModifierSet modifiers)
        {
            foreach (var territory in config.Territories)
                if (_states[territory.Id].Status == TerritoryStatus.Captured)
                    territory.Contribute(modifiers);
        }

        /// <summary>Food and Junk for winning: full reward the first time, RepeatRewardPct (rounded up) afterwards.</summary>
        public static (int Food, int Junk) Reward(TerritoryDefinition territory, bool firstVictory)
        {
            if (firstVictory)
                return (territory.FirstRewardFood, territory.FirstRewardJunk);
            int Part(int value) => (value * territory.RepeatRewardPct + 99) / 100;
            return (Part(territory.FirstRewardFood), Part(territory.FirstRewardJunk));
        }
    }

    /// <summary>A battle whose result is already fixed (simulated at "Fight!") but not yet applied.</summary>
    public sealed class PendingBattle
    {
        public TerritoryId Territory { get; }
        public IReadOnlyList<CatId> Order { get; }
        public int Seed { get; }
        public BattleSetup Setup { get; }
        public BattleResult Result { get; }

        public PendingBattle(TerritoryId territory, IReadOnlyList<CatId> order, int seed, BattleSetup setup, BattleResult result)
        {
            Territory = territory;
            Order = order.ToArray();
            Seed = seed;
            Setup = setup;
            Result = result;
        }
    }

    public sealed class BattleStarted : DomainEvent
    {
        public PendingBattle Battle { get; }

        public BattleStarted(PendingBattle battle) => Battle = battle;
    }

    public sealed class BattleCompleted : DomainEvent
    {
        public TerritoryId Territory { get; }
        public BattleOutcome Outcome { get; }
        public bool FirstVictory { get; }
        public int Food { get; }
        public int Junk { get; }
        public IReadOnlyList<CatId> Fought { get; }

        public BattleCompleted(TerritoryId territory, BattleOutcome outcome, bool firstVictory, int food, int junk, IReadOnlyList<CatId> fought)
        {
            Territory = territory;
            Outcome = outcome;
            FirstVictory = firstVictory;
            Food = food;
            Junk = junk;
            Fought = fought;
        }
    }

    public sealed class TerritoryCaptured : DomainEvent
    {
        public TerritoryId Territory { get; }
        public TerritoryId? Unlocked { get; }

        public TerritoryCaptured(TerritoryId territory, TerritoryId? unlocked)
        {
            Territory = territory;
            Unlocked = unlocked;
        }
    }

    public sealed class CatRecruited : DomainEvent
    {
        public CatId CatId { get; }

        public CatRecruited(CatId catId) => CatId = catId;
    }

    public sealed class FinalReached : DomainEvent
    {
    }
}
