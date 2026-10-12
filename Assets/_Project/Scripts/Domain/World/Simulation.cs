using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>The only way game time moves forward. Online ticks and offline catch-up both use it.</summary>
    public static class Simulation
    {
        /// <summary>
        /// Completes every cycle that ends at or before <paramref name="targetMs"/>, in chronological order across
        /// all cats (ties broken by gang order), then sets the world time to the target. Auto-repeated cycles start
        /// exactly when the previous one ended, so one large step equals many small ones.
        /// </summary>
        public static void AdvanceTo(GameWorld world, long targetMs, List<DomainEvent> events)
        {
            if (targetMs < world.GameTimeMs)
                throw new ArgumentOutOfRangeException(nameof(targetMs), $"Cannot move time backwards ({world.GameTimeMs} -> {targetMs}).");

            while (true)
            {
                Cat next = null;
                var nextEnd = long.MaxValue;
                foreach (var cat in world.Cats)
                {
                    if (!cat.Activity.IsWorking || cat.Activity.CycleEndMs >= nextEnd)
                        continue;
                    next = cat;
                    nextEnd = cat.Activity.CycleEndMs;
                }

                if (next == null || nextEnd > targetMs)
                    break;

                world.GameTimeMs = nextEnd;
                ActivitySystem.CompleteCycle(world, next, events);
            }

            world.GameTimeMs = targetMs;
        }

        /// <summary>Moves time forward by <paramref name="deltaMs"/> (must not be negative).</summary>
        public static void AdvanceBy(GameWorld world, long deltaMs, List<DomainEvent> events)
        {
            if (deltaMs < 0) throw new ArgumentOutOfRangeException(nameof(deltaMs));
            AdvanceTo(world, world.GameTimeMs + deltaMs, events);
        }
    }
}
