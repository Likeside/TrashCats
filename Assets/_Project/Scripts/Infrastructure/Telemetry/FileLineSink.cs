using System;
using System.IO;
using TrashCats.App;

namespace TrashCats.Infrastructure
{
    /// <summary>Appends telemetry lines to a local file (playtest_log.jsonl next to the save); write errors are ignored.</summary>
    public sealed class FileLineSink : ILineSink
    {
        private readonly string _path;

        public FileLineSink(string directory, string fileName = "playtest_log.jsonl")
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, fileName);
        }

        public string FilePath => _path;

        public void Append(string line)
        {
            try
            {
                File.AppendAllText(_path, line + "\n");
            }
            catch (Exception)
            {
                // telemetry must never break the game
            }
        }
    }
}
