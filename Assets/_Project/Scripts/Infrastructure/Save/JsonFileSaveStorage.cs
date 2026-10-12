using System;
using System.IO;
using System.Text;
using TrashCats.App;

namespace TrashCats.Infrastructure
{
    /// <summary>
    /// Save files in a directory (normally Application.persistentDataPath): save.json and save.bak.json.
    /// Writes go to a temp file first and then replace the main file, so a crash never leaves a half-written save.
    /// </summary>
    public sealed class JsonFileSaveStorage : ISaveStorage
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private readonly string _directory;

        public string MainPath => Path.Combine(_directory, "save.json");
        public string BackupPath => Path.Combine(_directory, "save.bak.json");
        private string TempPath => Path.Combine(_directory, "save.tmp.json");

        public JsonFileSaveStorage(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public string ReadMain() => ReadIfExists(MainPath);

        public string ReadBackup() => ReadIfExists(BackupPath);

        public void Write(string json)
        {
            Directory.CreateDirectory(_directory);
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Utf8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (!File.Exists(MainPath))
            {
                File.Move(TempPath, MainPath);
                return;
            }

            try
            {
                File.Replace(TempPath, MainPath, BackupPath);
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
            {
                // Some file systems do not support File.Replace; keep the backup first so a valid file always exists.
                File.Copy(MainPath, BackupPath, true);
                File.Copy(TempPath, MainPath, true);
                File.Delete(TempPath);
            }
        }

        public void WriteAside(string name, string json)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, Path.GetFileName(name)), json, Utf8);
        }

        public void DeleteAll()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
                if (File.Exists(path))
                    File.Delete(path);
        }

        private static string ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path, Utf8) : null;
    }
}
