using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TrashCats.Presentation
{
    /// <summary>
    /// Player-facing strings from the localization table (Resources/Localization/&lt;language&gt;.json, CONTENT_SPEC §11):
    /// key → text with {0}, {1}... placeholders. Strings were moved there by Tools/extract_strings.py.
    /// A missing key shows the key itself, so gaps are visible.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "ru";

        private static Dictionary<string, string> _table;

        public static string Language { get; private set; } = DefaultLanguage;

        public static void Load(string language)
        {
            var asset = Resources.Load<TextAsset>("Localization/" + language);
            _table = asset != null ? ParseFlat(asset.text) : new Dictionary<string, string>();
            Language = language;
        }

        public static string T(string key)
        {
            if (_table == null)
                Load(Language);
            return _table.TryGetValue(key, out var text) ? text : key;
        }

        /// <summary>Parses a flat JSON object of strings ({"key": "text", ...}).</summary>
        public static Dictionary<string, string> ParseFlat(string json)
        {
            var result = new Dictionary<string, string>();
            var i = 0;
            string ReadString()
            {
                while (json[i] != '"') i++;
                i++;
                var sb = new StringBuilder();
                while (json[i] != '"')
                {
                    var c = json[i++];
                    if (c != '\\') { sb.Append(c); continue; }
                    var e = json[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': sb.Append((char)int.Parse(json.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                i++;
                return sb.ToString();
            }

            while (true)
            {
                var next = json.IndexOf('"', i);
                if (next < 0) break;
                i = next;
                var key = ReadString();
                i = json.IndexOf(':', i) + 1;
                result[key] = ReadString();
            }

            return result;
        }

        public static string T(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);
    }
}
