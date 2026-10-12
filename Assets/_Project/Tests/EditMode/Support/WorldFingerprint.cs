using System.Linq;
using System.Text;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    /// <summary>Complete textual description of a world state; equal strings mean equal states.</summary>
    public static class WorldFingerprint
    {
        public static string Of(GameWorld world)
        {
            var sb = new StringBuilder();
            sb.Append($"t={world.GameTimeMs} food={world.Food} junk={world.Junk};");
            foreach (var upgrade in world.Upgrades.All.OrderBy(p => p.Key.Value))
                sb.Append($" {upgrade.Key}={upgrade.Value}");
            foreach (var cat in world.Cats)
            {
                sb.Append($"\n{cat.Id}: E{cat.Energy} S{cat.Satiety}");
                foreach (var attribute in AttributeTypes.All)
                    sb.Append($" {attribute}={cat.GetLevel(attribute)}/{cat.GetXp(attribute)}");
                sb.Append(" skills=").Append(string.Join(",", Enumerable.Range(0, Cat.SkillSlotCount).Select(i => cat.GetSkillInSlot(i)?.Value ?? "-")));
                sb.Append(' ').Append(cat.Activity);
            }

            return sb.ToString();
        }
    }
}
