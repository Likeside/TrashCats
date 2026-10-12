using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public interface IReadOnlyGameWorld
    {
        GameConfig Config { get; }
        long GameTimeMs { get; }
        int SaveSeed { get; }
        IReadOnlyList<IReadOnlyCat> Cats { get; }
        int Food { get; }
        int Junk { get; }
        int GetUpgradeLevel(UpgradeId id);
        bool TryGetCat(CatId id, out IReadOnlyCat cat);
    }

    /// <summary>
    /// Root aggregate of the game state. Mutated only by domain systems (via Application commands and
    /// <see cref="Simulation"/>); Presentation sees it through <see cref="IReadOnlyGameWorld"/>.
    /// </summary>
    public sealed class GameWorld : IReadOnlyGameWorld
    {
        private readonly List<Cat> _cats;

        public GameConfig Config { get; }
        public long GameTimeMs { get; internal set; }
        public int SaveSeed { get; }
        public IReadOnlyList<Cat> Cats => _cats;
        public Wallet Wallet { get; }
        public UpgradeLevels Upgrades { get; }

        IReadOnlyList<IReadOnlyCat> IReadOnlyGameWorld.Cats => _cats;
        public int Food => Wallet.Food;
        public int Junk => Wallet.Junk;

        public GameWorld(GameConfig config, long gameTimeMs, int saveSeed, IEnumerable<Cat> cats, Wallet wallet, UpgradeLevels upgrades)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (gameTimeMs < 0) throw new ArgumentOutOfRangeException(nameof(gameTimeMs));
            GameTimeMs = gameTimeMs;
            SaveSeed = saveSeed;
            _cats = new List<Cat>(cats ?? throw new ArgumentNullException(nameof(cats)));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        }

        /// <summary>New game: starting resources and starting cats from the config, time 0.</summary>
        public static GameWorld CreateNew(GameConfig config, int saveSeed)
        {
            var cats = new List<Cat>();
            foreach (var catId in config.Start.Cats)
                cats.Add(CatFactory.Create(config.GetCat(catId), config));

            return new GameWorld(config, 0, saveSeed, cats, new Wallet(config.Start.Food, config.Start.Junk), new UpgradeLevels());
        }

        public int GetUpgradeLevel(UpgradeId id) => Upgrades.GetLevel(id);

        public Cat GetCat(CatId id)
        {
            foreach (var cat in _cats)
                if (cat.Id == id)
                    return cat;
            throw new KeyNotFoundException($"Cat '{id}' is not in the gang.");
        }

        public bool TryGetCat(CatId id, out Cat cat)
        {
            foreach (var c in _cats)
            {
                if (c.Id != id) continue;
                cat = c;
                return true;
            }

            cat = null;
            return false;
        }

        bool IReadOnlyGameWorld.TryGetCat(CatId id, out IReadOnlyCat cat)
        {
            var found = TryGetCat(id, out var c);
            cat = c;
            return found;
        }

        /// <summary>All bonuses currently affecting activities.</summary>
        public ModifierSet CollectModifiers()
        {
            var modifiers = new ModifierSet();
            Upgrades.Contribute(Config, modifiers);
            return modifiers;
        }
    }
}
