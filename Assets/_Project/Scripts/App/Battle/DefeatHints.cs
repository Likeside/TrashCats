using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>Short, rule-based advice after a lost battle, read from the battle log (GDD UI_UX §9, COMBAT_DESIGN §M3-08).</summary>
    public static class DefeatHints
    {
        public static IReadOnlyList<string> For(PendingBattle battle, GameConfig config)
        {
            var hints = new List<string>();
            var log = battle.Result.Log;
            var playerHits = log.OfType<HitResolved>().Where(h => h.AttackerSide == Side.Player).ToList();
            var enemyHeavy = log.OfType<ActionPerformed>().Count(a => a.Side == Side.Enemy && config.GetSkill(a.SkillId).IsHeavyAttack);
            var team = battle.Setup.PlayerQueue;
            var hasDodge = team.Any(f => f.Skills.Any(s => config.GetSkill(s).Effect == SkillEffectKind.GuaranteedEvade));
            var hasFur = team.Any(f => f.Skills.Any(s => config.GetSkill(s).Effect == SkillEffectKind.DamageReduction));

            if (enemyHeavy >= 3 && !hasDodge)
                hints.Add("Враг часто кусал. Возьми Уворот (Ловкость 7) или Густую шерсть (Крепость 4).");
            if (playerHits.Count >= 6 && playerHits.Count(h => !h.Hit) * 100 / playerHits.Count >= 40)
                hints.Add("Много промахов. Подтяни Ловкость или возьми Грязный трюк (Сила 7).");
            if (hints.Count < 2 && !hasFur && team.Average(f => f.Toughness) < battle.Setup.EnemyQueue.Average(f => f.Toughness))
                hints.Add("Не хватает здоровья. Тренируй Крепость в колесе.");
            if (hints.Count == 0)
                hints.Add("Потренируйся и попробуй снова. Порядок бойцов тоже важен: победитель остаётся на ринге.");
            return hints.Take(2).ToList();
        }
    }
}
