using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;

namespace TrashCats.Tests
{
    /// <summary>The localization table (Resources/Localization/ru.json) covers every key used in code and parses.</summary>
    public sealed class LocalizationTableTests
    {
        private const string TablePath = "Assets/_Project/Resources/Localization/ru.json";

        private static Dictionary<string, string> Table() =>
            JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(TablePath));

        [Test]
        public void EveryKeyUsedInCode_IsInTheTable()
        {
            var table = Table();
            var used = Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Loc\.T\(""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value))
                .Concat(new[] { "defeat.bites", "defeat.misses", "defeat.health", "defeat.generic" })
                .Distinct().ToList();

            Assert.That(used, Is.Not.Empty);
            Assert.That(used.Where(k => !table.ContainsKey(k)), Is.Empty);
        }

        [Test]
        public void EveryTemplate_IsAValidFormatString()
        {
            var args = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
            foreach (var pair in Table())
                Assert.DoesNotThrow(() => string.Format(pair.Value, args), pair.Key);
        }
    }
}
