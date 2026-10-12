using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public static class TestWorlds
    {
        public static GameWorld Make(GameConfig config, int food = 0, int junk = 0, long timeMs = 0,
            IEnumerable<KeyValuePair<UpgradeId, int>> upgrades = null, params Cat[] cats)
        {
            return new GameWorld(config, timeMs, 1, cats, new Wallet(food, junk), new UpgradeLevels(upgrades));
        }

        public static KeyValuePair<UpgradeId, int> Level(UpgradeId id, int level) => new KeyValuePair<UpgradeId, int>(id, level);

        public static T Single<T>(this List<DomainEvent> events) where T : DomainEvent => events.OfType<T>().Single();
    }
}
