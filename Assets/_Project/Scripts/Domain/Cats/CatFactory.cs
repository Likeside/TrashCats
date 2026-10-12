namespace TrashCats.Domain
{
    public static class CatFactory
    {
        /// <summary>
        /// Creates a freshly recruited cat from its definition. Skills unlocked by the starting levels are
        /// auto-equipped in config order.
        /// </summary>
        public static Cat Create(CatDefinition definition, GameConfig config)
        {
            var levels = new int[AttributeTypes.Count];
            foreach (var attribute in AttributeTypes.All)
                levels[(int)attribute] = definition.GetStartLevel(attribute);

            var cat = new Cat(definition.Id, definition.Aptitude, levels, new int[AttributeTypes.Count],
                definition.StartEnergy, definition.StartSatiety, new SkillId?[Cat.SkillSlotCount],
                ActivityState.Idle(IdleReason.None));

            foreach (var skill in config.Skills)
                if (!skill.IsInnate && CatRules.IsSkillUnlocked(cat, skill))
                    CatRules.TryAutoEquip(cat, skill.Id, null);

            return cat;
        }
    }
}
