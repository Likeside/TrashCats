namespace TrashCats.Domain
{
    public sealed class SkillDefinition
    {
        public SkillId Id { get; init; }
        public string NameKey { get; init; }

        /// <summary>Innate skills (Scratch) are always available and never occupy a slot.</summary>
        public bool IsInnate { get; init; }

        public AttributeType UnlockAttribute { get; init; }
        public int UnlockLevel { get; init; }
    }
}
