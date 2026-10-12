namespace TrashCats.App
{
    /// <summary>Where save files live. Implementations must write atomically and keep the previous file as a backup.</summary>
    public interface ISaveStorage
    {
        /// <summary>Main save contents, or null if there is none.</summary>
        string ReadMain();

        /// <summary>Previous save contents, or null if there is none.</summary>
        string ReadBackup();

        /// <summary>Atomically replaces the main save, moving the old one to the backup.</summary>
        void Write(string json);

        /// <summary>Stores a file next to the saves without touching them (e.g. a save from a newer build).</summary>
        void WriteAside(string name, string json);

        /// <summary>Deletes main and backup saves.</summary>
        void DeleteAll();
    }
}
