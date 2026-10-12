using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>What happened while the player was away ("Пока тебя не было").</summary>
    public sealed class OfflineSummary
    {
        public long RealElapsedMs { get; }
        public long SimulatedMs { get; }
        public bool WasCapped => SimulatedMs < RealElapsedMs;
        public int FoodGained { get; }
        public int JunkGained { get; }
        public IReadOnlyList<AttributeLevelUp> LevelUps { get; }
        public IReadOnlyList<SkillUnlocked> SkillsUnlocked { get; }

        /// <summary>Cats that ended up idle during the catch-up, with the last reason.</summary>
        public IReadOnlyList<ActivityStopped> Stopped { get; }

        public OfflineSummary(long realElapsedMs, long simulatedMs, IReadOnlyList<DomainEvent> events)
        {
            RealElapsedMs = realElapsedMs;
            SimulatedMs = simulatedMs;
            var resources = events.OfType<ResourcesChanged>().ToList();
            FoodGained = resources.Sum(e => e.FoodDelta);
            JunkGained = resources.Sum(e => e.JunkDelta);
            LevelUps = events.OfType<AttributeLevelUp>().ToList();
            SkillsUnlocked = events.OfType<SkillUnlocked>().ToList();
            Stopped = events.OfType<ActivityStopped>().GroupBy(e => e.CatId).Select(g => g.Last()).ToList();
        }
    }

    /// <summary>Published after a catch-up longer than <see cref="TimeConfig.OfflineSummaryMinMs"/>.</summary>
    public sealed class OfflineProgressApplied
    {
        public OfflineSummary Summary { get; }

        public OfflineProgressApplied(OfflineSummary summary) => Summary = summary;
    }
}
