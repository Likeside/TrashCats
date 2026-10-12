namespace TrashCats.Domain
{
    public enum ActivityStatus
    {
        Idle = 0,
        Working = 1,
        InBattle = 2,
    }

    /// <summary>Why a cat is idle; shown to the player as a bubble.</summary>
    public enum IdleReason
    {
        None = 0,
        Tired = 1,
        Hungry = 2,
        SleepDone = 3,
        AttributeMax = 4,
        Stopped = 5,
        AfterBattle = 6,
    }

    /// <summary>Immutable snapshot of what a cat is doing.</summary>
    public sealed class ActivityState
    {
        public static readonly ActivityState InBattle = new ActivityState(ActivityStatus.InBattle, IdleReason.None, default, 0, 0, false);

        public ActivityStatus Status { get; }
        public IdleReason IdleReason { get; }
        public ActivityId ActivityId { get; }
        public long CycleStartMs { get; }
        public long CycleDurationMs { get; }

        /// <summary>The player started this activity while the cat was hungry, so hunger does not stop auto-repeat.</summary>
        public bool HungryOverride { get; }

        public long CycleEndMs => CycleStartMs + CycleDurationMs;
        public bool IsWorking => Status == ActivityStatus.Working;

        private ActivityState(ActivityStatus status, IdleReason idleReason, ActivityId activityId, long cycleStartMs, long cycleDurationMs, bool hungryOverride)
        {
            Status = status;
            IdleReason = idleReason;
            ActivityId = activityId;
            CycleStartMs = cycleStartMs;
            CycleDurationMs = cycleDurationMs;
            HungryOverride = hungryOverride;
        }

        public static ActivityState Idle(IdleReason reason) =>
            new ActivityState(ActivityStatus.Idle, reason, default, 0, 0, false);

        public static ActivityState Working(ActivityId activityId, long cycleStartMs, long cycleDurationMs, bool hungryOverride) =>
            new ActivityState(ActivityStatus.Working, IdleReason.None, activityId, cycleStartMs, cycleDurationMs, hungryOverride);

        public override string ToString() => Status switch
        {
            ActivityStatus.Working => $"Working({ActivityId}, {CycleStartMs}+{CycleDurationMs}{(HungryOverride ? ", hungry" : "")})",
            ActivityStatus.Idle => $"Idle({IdleReason})",
            _ => "InBattle",
        };
    }
}
