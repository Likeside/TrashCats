namespace TrashCats.Domain
{
    public enum ActivityKind
    {
        Scavenge = 0,
        Rummage = 1,
        Train = 2,
        Sleep = 3,
    }

    public sealed class ActivityDefinition
    {
        public ActivityId Id { get; init; }
        public string NameKey { get; init; }
        public ActivityKind Kind { get; init; }
        public long DurationMs { get; init; }
        public int EnergyCost { get; init; }
        public int SatietyCost { get; init; }

        /// <summary>Food, Junk, XP or restored Energy per cycle depending on <see cref="Kind"/>, before bonuses.</summary>
        public int BaseOutput { get; init; }

        /// <summary>Trained attribute; only meaningful for <see cref="ActivityKind.Train"/>.</summary>
        public AttributeType TrainedAttribute { get; init; }

        /// <summary>How many cats can do this activity at once; 0 means unlimited.</summary>
        public int Capacity { get; init; }

        /// <summary>Whether hunger doubles the cycle duration and stops auto-repeat.</summary>
        public bool AffectedByHunger => Kind != ActivityKind.Sleep;
    }
}
