using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>Development-only shortcuts used by the debug menu and automated build reviews.</summary>
    public static class DebugCheats
    {
        public static void UnlockAllTerritories(GameWorld world)
        {
            foreach (var territory in world.Config.Territories)
            {
                var state = world.Territories.Get(territory.Id);
                if (state.Status == TerritoryStatus.Locked)
                    state.Status = TerritoryStatus.Available;
            }
        }

        public static void RecruitAll(GameWorld world, List<DomainEvent> events)
        {
            foreach (var definition in world.Config.Cats)
            {
                if (world.TryGetCat(definition.Id, out _)) continue;
                world.AddCat(CatFactory.Create(definition, world.Config));
                events.Add(new CatRecruited(definition.Id));
            }
        }

        public static void RestAll(GameWorld world, List<DomainEvent> events)
        {
            foreach (var cat in world.Cats)
                CatRules.ChangeNeeds(cat, world.Config.Needs.Max, world.Config.Needs.Max, world.Config, events);
        }
    }
}
