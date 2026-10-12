namespace TrashCats.Domain
{
    /// <summary>
    /// Something that happened in the domain. Domain operations append events to a list they receive;
    /// the Application layer publishes them after the state change is complete.
    /// </summary>
    public abstract class DomainEvent
    {
    }

    public sealed class NeedsChanged : DomainEvent
    {
        public CatId CatId { get; }
        public int Energy { get; }
        public int Satiety { get; }

        public NeedsChanged(CatId catId, int energy, int satiety)
        {
            CatId = catId;
            Energy = energy;
            Satiety = satiety;
        }
    }

    public sealed class ResourcesChanged : DomainEvent
    {
        public int Food { get; }
        public int Junk { get; }
        public int FoodDelta { get; }
        public int JunkDelta { get; }

        public ResourcesChanged(int food, int junk, int foodDelta, int junkDelta)
        {
            Food = food;
            Junk = junk;
            FoodDelta = foodDelta;
            JunkDelta = junkDelta;
        }
    }

    public sealed class AttributeLevelUp : DomainEvent
    {
        public CatId CatId { get; }
        public AttributeType Attribute { get; }
        public int NewLevel { get; }

        public AttributeLevelUp(CatId catId, AttributeType attribute, int newLevel)
        {
            CatId = catId;
            Attribute = attribute;
            NewLevel = newLevel;
        }
    }

    public sealed class SkillUnlocked : DomainEvent
    {
        public CatId CatId { get; }
        public SkillId SkillId { get; }

        public SkillUnlocked(CatId catId, SkillId skillId)
        {
            CatId = catId;
            SkillId = skillId;
        }
    }

    public sealed class SkillEquipped : DomainEvent
    {
        public CatId CatId { get; }
        public int Slot { get; }
        public SkillId? SkillId { get; }

        public SkillEquipped(CatId catId, int slot, SkillId? skillId)
        {
            CatId = catId;
            Slot = slot;
            SkillId = skillId;
        }
    }
}
