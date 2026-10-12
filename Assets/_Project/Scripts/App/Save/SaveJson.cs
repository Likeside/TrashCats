using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrashCats.App
{
    public sealed class SaveFromFutureException : Exception
    {
        public int Version { get; }

        public SaveFromFutureException(int version)
            : base($"Save version {version} is newer than supported {SaveData.CurrentVersion}.") => Version = version;
    }

    /// <summary>JSON (de)serialization of saves with step-by-step version migrations.</summary>
    public static class SaveJson
    {
        /// <summary>Migrations from version N to N+1 as pure JSON transformations. Each one needs a sample-based test.</summary>
        private static readonly IReadOnlyDictionary<int, Func<JObject, JObject>> Migrations = new Dictionary<int, Func<JObject, JObject>>();

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
        };

        public static string Serialize(SaveData data, bool indented) =>
            JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None, Settings);

        /// <summary>Parses and migrates a save. Throws <see cref="JsonException"/> on malformed JSON and
        /// <see cref="SaveFromFutureException"/> when the file comes from a newer build.</summary>
        public static SaveData Deserialize(string json)
        {
            var root = JObject.Parse(json);
            var version = root.Value<int?>(nameof(SaveData.Version)) ?? 0;
            if (version > SaveData.CurrentVersion)
                throw new SaveFromFutureException(version);

            while (version < SaveData.CurrentVersion)
            {
                if (!Migrations.TryGetValue(version, out var migrate))
                    throw new JsonSerializationException($"No migration from save version {version}.");
                root = migrate(root);
                version++;
                root[nameof(SaveData.Version)] = version;
            }

            return root.ToObject<SaveData>(JsonSerializer.Create(Settings));
        }
    }
}
