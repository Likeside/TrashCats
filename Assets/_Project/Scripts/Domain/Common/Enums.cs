namespace TrashCats.Domain
{
    public enum AttributeType
    {
        Strength = 0,
        Agility = 1,
        Toughness = 2,
    }

    public enum ResourceType
    {
        Food = 0,
        Junk = 1,
    }

    public static class AttributeTypes
    {
        public const int Count = 3;

        public static readonly AttributeType[] All =
        {
            AttributeType.Strength,
            AttributeType.Agility,
            AttributeType.Toughness,
        };
    }
}
