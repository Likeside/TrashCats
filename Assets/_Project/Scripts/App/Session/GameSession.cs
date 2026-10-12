using System;
using System.Collections.Generic;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>
    /// The single entry point that changes the world. Every command first catches the world up to "now",
    /// then validates (no change on failure), applies, publishes the resulting events and requests a save.
    /// </summary>
    public sealed class GameSession
    {
        private readonly GameWorld _world;
        private readonly IClock _clock;
        private readonly EventBus _bus;
        private readonly ISaveRequests _save;

        public IReadOnlyGameWorld World => _world;
        public GameConfig Config => _world.Config;

        /// <summary>Wall-clock time of the last tick; persisted so the next launch can catch up.</summary>
        public long LastTickUtcMs { get; private set; }

        /// <summary>Development-only speed-up of game time.</summary>
        public int DebugTimeScale { get; set; } = 1;

        public GameSession(GameWorld world, IClock clock, EventBus bus, ISaveRequests save, long lastTickUtcMs)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _save = save ?? NoSaveRequests.Instance;
            LastTickUtcMs = lastTickUtcMs;
        }

        /// <summary>Internal access for application services that own their own rules (saving, battles).</summary>
        internal GameWorld MutableWorld => _world;

        /// <summary>
        /// Moves game time forward by the real time elapsed since the last tick, capped at the offline limit.
        /// A clock moved backwards yields no progress. Long gaps produce an <see cref="OfflineProgressApplied"/> event.
        /// </summary>
        public void Tick()
        {
            var now = _clock.UtcNowMs;
            var realDelta = now - LastTickUtcMs;
            LastTickUtcMs = now;
            if (realDelta <= 0)
                return;

            var simulated = Math.Min(realDelta, Config.Time.OfflineCapMs) * Math.Max(1, DebugTimeScale);
            var events = new List<DomainEvent>();
            Simulation.AdvanceBy(_world, simulated, events);
            _bus.PublishAll(events);

            if (realDelta >= Config.Time.OfflineSummaryMinMs)
                _bus.Publish(new OfflineProgressApplied(new OfflineSummary(realDelta, simulated, events)));
            if (events.Count > 0)
                _save.MarkDirty(SaveUrgency.Deferred);
        }

        // --- Read-only queries for UI (no time catch-up, no state change) -------------------------------

        public CommandError CanAssign(CatId catId, ActivityId activityId) =>
            _world.TryGetCat(catId, out var cat) ? CommandErrors.From(ActivitySystem.CanStart(_world, cat, activityId)) : CommandError.UnknownCat;

        public CommandError CanFeed(CatId catId) =>
            _world.TryGetCat(catId, out var cat) ? CommandErrors.From(Feeding.CanFeed(cat, _world.Wallet, Config)) : CommandError.UnknownCat;

        /// <summary>Food that feeding would spend right now (limited by the stockpile).</summary>
        public int FoodToFeed(CatId catId) =>
            _world.TryGetCat(catId, out var cat) ? Math.Min(Feeding.FoodToFill(cat, Config), _world.Wallet.Food) : 0;

        public long PreviewDuration(CatId catId, ActivityId activityId) =>
            ActivitySystem.CycleDuration(_world, _world.GetCat(catId), Config.GetActivity(activityId));

        public int PreviewOutput(CatId catId, ActivityId activityId) =>
            ActivitySystem.PreviewOutput(_world, _world.GetCat(catId), Config.GetActivity(activityId));

        // --- Commands --------------------------------------------------------------------------------

        public CommandResult AssignActivity(CatId catId, ActivityId activityId) => Execute(catId,
            cat => CommandErrors.From(ActivitySystem.CanStart(_world, cat, activityId)),
            (cat, events) => ActivitySystem.Start(_world, cat, activityId, events),
            SaveUrgency.Deferred);

        public CommandResult StopActivity(CatId catId) => Execute(catId,
            cat => cat.Activity.IsWorking ? CommandError.None : CommandError.NotWorking,
            (cat, events) => ActivitySystem.Stop(cat, IdleReason.Stopped, events),
            SaveUrgency.Deferred);

        public CommandResult FeedCat(CatId catId) => Execute(catId,
            cat => CommandErrors.From(Feeding.CanFeed(cat, _world.Wallet, Config)),
            (cat, events) => Feeding.Feed(cat, _world.Wallet, Config, events),
            SaveUrgency.Deferred);

        public CommandResult EquipSkill(CatId catId, int slot, SkillId? skillId) => Execute(catId,
            cat => CommandErrors.From(CatRules.CanEquip(cat, slot, skillId, Config)),
            (cat, events) => CatRules.Equip(cat, slot, skillId, Config, events),
            SaveUrgency.Deferred);

        public CommandResult BuyUpgrade(UpgradeId upgradeId) => Execute(
            () => CommandErrors.From(Hideout.CanBuy(_world.Upgrades, _world.Wallet, Config, upgradeId)),
            events => Hideout.Buy(_world.Upgrades, _world.Wallet, Config, upgradeId, events),
            SaveUrgency.Immediate);

        private CommandResult Execute(CatId catId, Func<Cat, CommandError> validate, Action<Cat, List<DomainEvent>> apply, SaveUrgency urgency)
        {
            Tick();
            if (!_world.TryGetCat(catId, out var cat))
                return CommandResult.Fail(CommandError.UnknownCat);
            return Execute(() => validate(cat), events => apply(cat, events), urgency, caughtUp: true);
        }

        private CommandResult Execute(Func<CommandError> validate, Action<List<DomainEvent>> apply, SaveUrgency urgency, bool caughtUp = false)
        {
            if (!caughtUp)
                Tick();

            var error = validate();
            if (error != CommandError.None)
                return CommandResult.Fail(error);

            var events = new List<DomainEvent>();
            apply(events);
            _bus.PublishAll(events);
            _save.MarkDirty(urgency);
            return CommandResult.Ok;
        }
    }
}
