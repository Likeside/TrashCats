namespace TrashCats.Domain
{
    public sealed class CatDefinition
    {
        public CatId Id { get; init; }
        public string NameKey { get; init; }
        public AttributeType Aptitude { get; init; }
        public int StartStrength { get; init; } = 1;
        public int StartAgility { get; init; } = 1;
        public int StartToughness { get; init; } = 1;
        public int StartEnergy { get; init; } = 100;
        public int StartSatiety { get; init; } = 60;

        /// <summary>Key of the visual representation; only Presentation resolves it.</summary>
        public string ViewKey { get; init; }

        public int GetStartLevel(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => StartStrength,
            AttributeType.Agility => StartAgility,
            _ => StartToughness,
        };
    }
}
