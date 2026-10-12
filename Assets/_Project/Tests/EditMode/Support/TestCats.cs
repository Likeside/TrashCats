using TrashCats.Domain;

namespace TrashCats.Tests
{
    public static class TestCats
    {
        public static Cat Make(CatId id, int str = 1, int agi = 1, int tou = 1, int energy = 100, int satiety = 100,
            AttributeType aptitude = AttributeType.Strength, ActivityState activity = null)
        {
            return new Cat(id, aptitude, new[] { str, agi, tou }, new int[3], energy, satiety,
                new SkillId?[Cat.SkillSlotCount], activity ?? ActivityState.Idle(IdleReason.None));
        }
    }
}
