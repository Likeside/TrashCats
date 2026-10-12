using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public enum FeedError
    {
        None = 0,
        InBattle,
        NoFood,
        AlreadyFull,
    }

    /// <summary>Instant feeding from the shared stockpile: 1 Food restores <see cref="NeedsConfig.SatietyPerFood"/> Satiety.</summary>
    public static class Feeding
    {
        public static FeedError CanFeed(IReadOnlyCat cat, Wallet wallet, GameConfig config)
        {
            if (cat.Activity.Status == ActivityStatus.InBattle) return FeedError.InBattle;
            if (cat.Satiety >= config.Needs.Max) return FeedError.AlreadyFull;
            if (wallet.Food <= 0) return FeedError.NoFood;
            return FeedError.None;
        }

        /// <summary>Food needed to fill the cat completely (rounded up).</summary>
        public static int FoodToFill(IReadOnlyCat cat, GameConfig config)
        {
            var missing = config.Needs.Max - cat.Satiety;
            var perFood = config.Needs.SatietyPerFood;
            return (missing + perFood - 1) / perFood;
        }

        /// <summary>Feeds until full or out of food. Returns the amount of Food spent. Throws if <see cref="CanFeed"/> fails.</summary>
        public static int Feed(Cat cat, Wallet wallet, GameConfig config, List<DomainEvent> events)
        {
            var error = CanFeed(cat, wallet, config);
            if (error != FeedError.None)
                throw new InvalidOperationException($"Cannot feed {cat.Id}: {error}.");

            var food = Math.Min(FoodToFill(cat, config), wallet.Food);
            wallet.TrySpend(ResourceType.Food, food, events);
            CatRules.ChangeNeeds(cat, 0, food * config.Needs.SatietyPerFood, config, events);
            return food;
        }
    }
}
