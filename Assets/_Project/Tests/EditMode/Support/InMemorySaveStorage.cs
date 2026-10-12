using System.Collections.Generic;
using TrashCats.App;

namespace TrashCats.Tests
{
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public string Main;
        public string Backup;
        public readonly Dictionary<string, string> Aside = new Dictionary<string, string>();
        public int Writes;

        public string ReadMain() => Main;
        public string ReadBackup() => Backup;

        public void Write(string json)
        {
            if (Main != null) Backup = Main;
            Main = json;
            Writes++;
        }

        public void WriteAside(string name, string json) => Aside[name] = json;

        public void DeleteAll()
        {
            Main = null;
            Backup = null;
        }
    }
}
