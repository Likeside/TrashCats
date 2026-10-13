using System.Collections.Generic;

namespace TrashCats.App
{
    // Plain serializable DTOs, deliberately separate from domain classes so refactoring the domain does not break saves.
    // Fields missing in older files get their default values; see Docs/Architecture/SAVE_SYSTEM.md.

    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long SavedAtUtcMs;
        public long GameTimeMs;
        public int SaveSeed;
        public int Food;
        public int Junk;
        public List<CatSave> Cats = new List<CatSave>();
        public Dictionary<string, int> Upgrades = new Dictionary<string, int>();
        public Dictionary<string, TerritorySave> Territories = new Dictionary<string, TerritorySave>();
        public PendingBattleSave PendingBattle;
        public bool FinalReached;
        public TutorialSave Tutorial = new TutorialSave();
        public SettingsSave Settings = new SettingsSave();
    }

    public sealed class TutorialSave
    {
        public List<string> Completed = new List<string>();
        public bool Skipped;
    }

    public sealed class CatSave
    {
        public string Id;
        public int[] Levels;
        public int[] Xp;
        public int Energy;
        public int Satiety;
        public string[] Skills;
        public ActivitySave Activity = new ActivitySave();
    }

    public sealed class ActivitySave
    {
        public string State = "Idle";
        public string IdleReason;
        public string ActivityId;
        public long CycleStartMs;
        public long CycleDurationMs;
        public bool HungryOverride;
    }

    public sealed class SettingsSave
    {
        public int BattleSpeed = 1;
        public float SfxVolume = 1f;
        public float MusicVolume = 0.7f;
    }
}

namespace TrashCats.App
{
    public sealed class TerritorySave
    {
        public string Status = "Locked";
        public int Attempts;
    }

    /// <summary>The battle is stored as its inputs; the deterministic simulator recreates the same result on load.</summary>
    public sealed class PendingBattleSave
    {
        public string Territory;
        public string[] Order;
        public int Seed;
        public List<FighterSave> Players = new List<FighterSave>();
        public List<FighterSave> Enemies = new List<FighterSave>();
    }

    public sealed class FighterSave
    {
        public string Id;
        public int Strength;
        public int Agility;
        public int Toughness;
        public string[] Skills;
        public int HpMultiplierPermille = 1000;
        public string ViewKey;
    }
}
