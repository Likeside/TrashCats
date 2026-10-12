namespace TrashCats.Domain
{
    /// <summary>
    /// Aggregated bonuses from hideout upgrades (and later territories) that affect activity results.
    /// Rebuilt from the world state when needed; never persisted.
    /// </summary>
    public sealed class ModifierSet
    {
        private readonly int[] _trainingXpPct = new int[AttributeTypes.Count];

        public int SleepEnergyFlat { get; set; }
        public int FoodOutputFlat { get; set; }
        public int JunkOutputFlat { get; set; }
        public int FoodOutputPct { get; set; }
        public int JunkOutputPct { get; set; }
        public int TrainingXpPctAll { get; set; }

        public void AddTrainingXpPct(AttributeType attribute, int pct) => _trainingXpPct[(int)attribute] += pct;

        public int GetTrainingXpPct(AttributeType attribute) => _trainingXpPct[(int)attribute] + TrainingXpPctAll;
    }
}
