using System.Collections.Generic;
using NUnit.Framework;
using TrashCats.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace TrashCats.Tests
{
    public sealed class ConfigAssetTests
    {
        private const string AssetPath = "Assets/_Project/Configs/GameConfig.asset";

        [Test]
        public void GameConfigAsset_LoadsAndValidates()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, AssetPath);

            Assert.DoesNotThrow(() => ConfigLoader.Load(asset));
        }

        [Test]
        public void GameConfigAsset_MatchesTestConfig()
        {
            // Unit tests use TestConfigs.Default() (the same but for the activity durations); this keeps it identical to
            // what the game ships with.
            var asset = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(AssetPath);

            Assert.That(ConfigDescription.Of(ConfigLoader.Load(asset)), Is.EqualTo(ConfigDescription.Of(TestConfigs.Shipped())));
        }

        [Test]
        public void InvalidAsset_ThrowsWithValidatorMessage()
        {
            var asset = ScriptableObject.CreateInstance<GameConfigAsset>();
            asset.startCats = new List<string> { "cat_missing" };
            try
            {
                var e = Assert.Throws<InvalidConfigException>(() => ConfigLoader.Load(asset));
                Assert.That(e.Message, Does.Contain("unknown cat 'cat_missing'"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void EmptyId_IsReportedAsInvalidConfig()
        {
            var asset = ScriptableObject.CreateInstance<GameConfigAsset>();
            asset.startCats = new List<string> { "" };
            try
            {
                Assert.Throws<InvalidConfigException>(() => ConfigLoader.Load(asset));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
