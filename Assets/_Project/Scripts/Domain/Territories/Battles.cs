using System;
using System.Collections.Generic;
using System.Linq;

namespace TrashCats.Domain
{
    public enum BattleError
    {
        None = 0,
        UnknownTerritory,
        TerritoryLocked,
        InvalidTeam,
        CatTooTired,
        BattlePending,
    }

    /// <summary>Starting and resolving battles for territories (Docs/GDD/COMBAT_DESIGN.md §8-9).</summary>
    public static class Battles
    {
        public const int MaxTeamSize = 3;

        public static BattleError CanStart(GameWorld world, TerritoryId territory, IReadOnlyList<CatId> order)
        {
            if (!world.Config.TryGetTerritory(territory, out _)) return BattleError.UnknownTerritory;
            if (world.PendingBattle != null) return BattleError.BattlePending;
            if (!world.Territories.IsPlayable(territory)) return BattleError.TerritoryLocked;
            if (order == null || order.Count == 0 || order.Count > MaxTeamSize || order.Distinct().Count() != order.Count)
                return BattleError.InvalidTeam;
            foreach (var id in order)
            {
                if (!world.TryGetCat(id, out var cat)) return BattleError.InvalidTeam;
                if (cat.Energy < world.Config.Needs.MinEnergyForBattle) return BattleError.CatTooTired;
            }

            return BattleError.None;
        }

        /// <summary>Cancels the team's activities, simulates the battle and stores it as pending. The result is final.</summary>
        public static PendingBattle Start(GameWorld world, TerritoryId territory, IReadOnlyList<CatId> order, List<DomainEvent> events)
        {
            var error = CanStart(world, territory, order);
            if (error != BattleError.None)
                throw new InvalidOperationException($"Cannot start battle at {territory}: {error}.");

            var config = world.Config;
            foreach (var id in order)
            {
                var cat = world.GetCat(id);
                ActivitySystem.Stop(cat, IdleReason.None, events);
                cat.Activity = ActivityState.InBattle;
            }

            var attempt = world.Territories.RegisterAttempt(territory);
            var seed = DeterministicRng.CombineSeeds(world.SaveSeed, DeterministicRng.StableHash(territory.Value), attempt);
            var setup = new BattleSetup(
                order.Select(id => FighterSnapshot.FromCat(world.GetCat(id), config)),
                config.GetTerritory(territory).EnemyQueue.Select(e => FighterSnapshot.FromEnemy(config.GetEnemy(e))));
            var battle = new PendingBattle(territory, order, seed, setup, BattleSimulator.Simulate(setup, seed, config));
            world.PendingBattle = battle;
            events.Add(new BattleStarted(battle));
            return battle;
        }

        /// <summary>Applies the pending battle: costs and XP for cats that fought, rewards, capture, recruitment.</summary>
        public static void Complete(GameWorld world, List<DomainEvent> events)
        {
            var battle = world.PendingBattle ?? throw new InvalidOperationException("No pending battle.");
            var config = world.Config;
            var territory = config.GetTerritory(battle.Territory);
            var victory = battle.Result.Outcome == BattleOutcome.Victory;
            var fought = battle.Result.FoughtPlayerIds.Select(id => new CatId(id)).ToList();

            foreach (var id in fought)
            {
                var cat = world.GetCat(id);
                var energyDelta = victory ? -config.Needs.BattleWinEnergyCost : -cat.Energy;
                CatRules.ChangeNeeds(cat, energyDelta, -config.Needs.BattleSatietyCost, config, events);
                foreach (var attribute in AttributeTypes.All)
                    CatRules.AddXp(cat, attribute, victory ? config.Progression.BattleXpWin : config.Progression.BattleXpLoss, config, events);
            }

            foreach (var id in battle.Order)
                world.GetCat(id).Activity = ActivityState.Idle(IdleReason.AfterBattle);

            var firstVictory = victory && world.Territories.Get(territory.Id).Status != TerritoryStatus.Captured;
            var (food, junk) = victory ? TerritoryProgress.Reward(territory, firstVictory) : (0, 0);
            world.Wallet.Add(ResourceType.Food, food, events);
            world.Wallet.Add(ResourceType.Junk, junk, events);
            world.PendingBattle = null;
            events.Add(new BattleCompleted(territory.Id, battle.Result.Outcome, firstVictory, food, junk, fought));

            if (!firstVictory)
                return;

            world.Territories.Capture(territory.Id, config);
            var index = config.Territories.ToList().FindIndex(t => t.Id == territory.Id);
            TerritoryId? next = index + 1 < config.Territories.Count ? config.Territories[index + 1].Id : (TerritoryId?)null;
            events.Add(new TerritoryCaptured(territory.Id, next));

            if (territory.RecruitCat is CatId recruit && !world.TryGetCat(recruit, out _))
            {
                world.AddCat(CatFactory.Create(config.GetCat(recruit), config));
                events.Add(new CatRecruited(recruit));
            }

            if (territory.IsFinal && !world.FinalReached)
            {
                world.FinalReached = true;
                events.Add(new FinalReached());
            }
        }
    }
}

namespace TrashCats.Domain
{
    /// <summary>Restoring a pending battle from a save: the battle owns its cats again.</summary>
    public static class BattleRestore
    {
        public static void MarkInBattle(Cat cat) => cat.Activity = ActivityState.InBattle;
    }
}
