namespace TrashCats.Domain
{
    public sealed class ActivityStarted : DomainEvent
    {
        public CatId CatId { get; }
        public ActivityId ActivityId { get; }
        public long CycleStartMs { get; }
        public long CycleDurationMs { get; }

        /// <summary>True when the cycle was started by auto-repeat rather than by the player.</summary>
        public bool IsRepeat { get; }

        public ActivityStarted(CatId catId, ActivityId activityId, long cycleStartMs, long cycleDurationMs, bool isRepeat)
        {
            CatId = catId;
            ActivityId = activityId;
            CycleStartMs = cycleStartMs;
            CycleDurationMs = cycleDurationMs;
            IsRepeat = isRepeat;
        }
    }

    public sealed class ActivityCycleCompleted : DomainEvent
    {
        public CatId CatId { get; }
        public ActivityId ActivityId { get; }
        public ActivityKind Kind { get; }

        /// <summary>Food, Junk, XP (as granted, before the level cap) or restored Energy depending on <see cref="Kind"/>.</summary>
        public int Output { get; }

        public AttributeType TrainedAttribute { get; }

        public ActivityCycleCompleted(CatId catId, ActivityId activityId, ActivityKind kind, int output, AttributeType trainedAttribute)
        {
            CatId = catId;
            ActivityId = activityId;
            Kind = kind;
            Output = output;
            TrainedAttribute = trainedAttribute;
        }
    }

    public sealed class ActivityStopped : DomainEvent
    {
        public CatId CatId { get; }
        public ActivityId ActivityId { get; }
        public IdleReason Reason { get; }

        public ActivityStopped(CatId catId, ActivityId activityId, IdleReason reason)
        {
            CatId = catId;
            ActivityId = activityId;
            Reason = reason;
        }
    }
}
