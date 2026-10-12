namespace TrashCats.Domain
{
    /// <summary>Integer bonus math shared by all activity results.</summary>
    public static class Bonuses
    {
        /// <summary>value × (100 + pct) / 100, rounded half up. Used for Food and Junk output.</summary>
        public static int ApplyPctRounded(int value, int pct) => (value * (100 + pct) + 50) / 100;

        /// <summary>value × (100 + pct) / 100, rounded down. Used for training XP.</summary>
        public static int ApplyPctFloor(int value, int pct) => value * (100 + pct) / 100;
    }
}
