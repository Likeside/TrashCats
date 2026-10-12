using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public enum ActivityError
    {
        None = 0,
        UnknownActivity,
        InBattle,
        Tired,
        EnergyFull,
        StationBusy,
        AttributeMaxed,
    }

    /// <summary>
    /// Rules of timed activities (Docs/GDD/CORE_SYSTEMS.md §3): starting, cycle duration, atomic completion,
    /// auto-repeat and cancellation. Time only moves through <see cref="Simulation.AdvanceTo"/>.
    /// </summary>
    public static class ActivitySystem
    {
        public static ActivityError CanStart(GameWorld world, IReadOnlyCat cat, ActivityId activityId)
        {
            if (!world.Config.TryGetActivity(activityId, out var definition)) return ActivityError.UnknownActivity;
            if (cat.Activity.Status == ActivityStatus.InBattle) return ActivityError.InBattle;
            return CheckCycleStart(world, cat, definition);
        }

        /// <summary>
        /// Starts an activity chosen by the player, cancelling the current cycle (its progress is lost, nothing is paid
        /// or granted). A hungry cat may still start: the cycle is slower and hunger will not stop auto-repeat.
        /// </summary>
        public static void Start(GameWorld world, Cat cat, ActivityId activityId, List<DomainEvent> events)
        {
            var error = CanStart(world, cat, activityId);
            if (error != ActivityError.None)
                throw new InvalidOperationException($"Cannot start {activityId} for {cat.Id}: {error}.");

            var definition = world.Config.GetActivity(activityId);
            var hungryOverride = definition.AffectedByHunger && CatRules.IsHungry(cat, world.Config);
            StartCycle(world, cat, definition, hungryOverride, false, events);
        }

        /// <summary>Stops the current cycle without paying or granting anything.</summary>
        public static void Stop(Cat cat, IdleReason reason, List<DomainEvent> events)
        {
            if (!cat.Activity.IsWorking)
                return;

            var activityId = cat.Activity.ActivityId;
            cat.Activity = ActivityState.Idle(reason);
            events.Add(new ActivityStopped(cat.Id, activityId, reason));
        }

        /// <summary>Cycle duration for this cat right now (hunger doubles affected activities).</summary>
        public static long CycleDuration(GameWorld world, IReadOnlyCat cat, ActivityDefinition definition)
        {
            var hungry = definition.AffectedByHunger && CatRules.IsHungry(cat, world.Config);
            return hungry ? definition.DurationMs * world.Config.Needs.HungryDurationMultiplier : definition.DurationMs;
        }

        /// <summary>Progress of the current cycle in [0, 1]; 0 when the cat is not working.</summary>
        public static float GetCycleProgress(IReadOnlyCat cat, long nowMs)
        {
            var state = cat.Activity;
            if (!state.IsWorking) return 0f;
            var elapsed = Math.Clamp(nowMs - state.CycleStartMs, 0, state.CycleDurationMs);
            return (float)elapsed / state.CycleDurationMs;
        }

        /// <summary>Completes the current cycle at <see cref="GameWorld.GameTimeMs"/>: costs, output, then auto-repeat or stop.</summary>
        internal static void CompleteCycle(GameWorld world, Cat cat, List<DomainEvent> events)
        {
            var state = cat.Activity;
            if (!state.IsWorking)
                throw new InvalidOperationException($"{cat.Id} has no cycle to complete.");

            var config = world.Config;
            var definition = config.GetActivity(state.ActivityId);
            var modifiers = world.CollectModifiers();

            CatRules.ChangeNeeds(cat, -definition.EnergyCost, -definition.SatietyCost, config, events);
            var output = ApplyOutput(world, cat, definition, modifiers, events);
            events.Add(new ActivityCycleCompleted(cat.Id, definition.Id, definition.Kind, output, definition.TrainedAttribute));

            var reason = StopReason(world, cat, definition, state.HungryOverride);
            if (reason == IdleReason.None)
                StartCycle(world, cat, definition, state.HungryOverride, true, events);
            else
                Stop(cat, reason, events);
        }

        internal static int ExpectedOutput(GameWorld world, IReadOnlyCat cat, ActivityDefinition definition, ModifierSet modifiers)
        {
            switch (definition.Kind)
            {
                case ActivityKind.Scavenge:
                    return Bonuses.ApplyPctRounded(definition.BaseOutput + modifiers.FoodOutputFlat, modifiers.FoodOutputPct);
                case ActivityKind.Rummage:
                    return Bonuses.ApplyPctRounded(definition.BaseOutput + modifiers.JunkOutputFlat, modifiers.JunkOutputPct);
                case ActivityKind.Train:
                    var aptitude = cat.Aptitude == definition.TrainedAttribute ? world.Config.Progression.AptitudeBonusPct : 0;
                    return Bonuses.ApplyPctFloor(definition.BaseOutput, aptitude + modifiers.GetTrainingXpPct(definition.TrainedAttribute));
                case ActivityKind.Sleep:
                    return definition.BaseOutput + modifiers.SleepEnergyFlat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, null);
            }
        }

        private static int ApplyOutput(GameWorld world, Cat cat, ActivityDefinition definition, ModifierSet modifiers, List<DomainEvent> events)
        {
            var output = ExpectedOutput(world, cat, definition, modifiers);
            switch (definition.Kind)
            {
                case ActivityKind.Scavenge:
                    world.Wallet.Add(ResourceType.Food, output, events);
                    break;
                case ActivityKind.Rummage:
                    world.Wallet.Add(ResourceType.Junk, output, events);
                    break;
                case ActivityKind.Train:
                    CatRules.AddXp(cat, definition.TrainedAttribute, output, world.Config, events);
                    break;
                case ActivityKind.Sleep:
                    CatRules.ChangeNeeds(cat, output, 0, world.Config, events);
                    break;
            }

            return output;
        }

        private static ActivityError CheckCycleStart(GameWorld world, IReadOnlyCat cat, ActivityDefinition definition)
        {
            var config = world.Config;
            if (definition.Kind == ActivityKind.Sleep)
                return cat.Energy >= config.Needs.Max ? ActivityError.EnergyFull : ActivityError.None;

            if (CatRules.IsTired(cat, config)) return ActivityError.Tired;
            if (definition.Kind == ActivityKind.Train && cat.GetLevel(definition.TrainedAttribute) >= config.Progression.MaxLevel)
                return ActivityError.AttributeMaxed;
            if (definition.Capacity > 0 && CountOthersOn(world, cat, definition.Id) >= definition.Capacity)
                return ActivityError.StationBusy;
            return ActivityError.None;
        }

        private static IdleReason StopReason(GameWorld world, Cat cat, ActivityDefinition definition, bool hungryOverride)
        {
            switch (CheckCycleStart(world, cat, definition))
            {
                case ActivityError.EnergyFull: return IdleReason.SleepDone;
                case ActivityError.Tired: return IdleReason.Tired;
                case ActivityError.AttributeMaxed: return IdleReason.AttributeMax;
            }

            if (definition.AffectedByHunger && !hungryOverride && CatRules.IsHungry(cat, world.Config))
                return IdleReason.Hungry;
            return IdleReason.None;
        }

        private static void StartCycle(GameWorld world, Cat cat, ActivityDefinition definition, bool hungryOverride, bool isRepeat, List<DomainEvent> events)
        {
            var duration = CycleDuration(world, cat, definition);
            cat.Activity = ActivityState.Working(definition.Id, world.GameTimeMs, duration, hungryOverride);
            events.Add(new ActivityStarted(cat.Id, definition.Id, world.GameTimeMs, duration, isRepeat));
        }

        private static int CountOthersOn(GameWorld world, IReadOnlyCat cat, ActivityId activityId)
        {
            var count = 0;
            foreach (var other in world.Cats)
                if (other.Id != cat.Id && other.Activity.IsWorking && other.Activity.ActivityId == activityId)
                    count++;
            return count;
        }
    }
}
