using System.Collections;
using System.IO;
using NUnit.Framework;
using TrashCats.Domain;
using TrashCats.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TrashCats.Tests
{
    public sealed class BootFlowTests
    {
        private const float TimeoutSeconds = 10f;
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "trashcats-playmode-" + System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _saveDir;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrap.Instance != null)
                Object.Destroy(GameBootstrap.Instance.gameObject);
            yield return null;

            // Leave an empty scene so the next test really reloads Boot and Game.
            var empty = SceneManager.CreateScene("Empty_" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameBootstrap.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir))
                Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Boot_LoadsGameScene_WithBootedRoot()
        {
            yield return BootGame();

            Assert.That(GameBootstrap.Instance, Is.Not.Null);
            var root = Object.FindFirstObjectByType<GameSceneRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.IsBooted, Is.True);
            Assert.That(File.Exists(Path.Combine(_saveDir, "save.json")), Is.True, "new game is saved immediately");
        }

        [UnityTest]
        public IEnumerator TappingCatAndActivity_StartsActivity()
        {
            yield return BootGame();

            Click("Cat_cat_ryzhy");
            yield return null;
            Click("Activity_act_scavenge");
            yield return null;

            var ryzhy = GameBootstrap.Instance.Services.Session.World.Cats[0];
            Assert.That(ryzhy.Activity.IsWorking, Is.True);
            Assert.That(ryzhy.Activity.ActivityId, Is.EqualTo(new ActivityId("act_scavenge")));
        }

        [UnityTest]
        public IEnumerator BattleLoop_MapPrepFightSkipResults_AppliesBattle()
        {
            yield return BootGame();
            var session = GameBootstrap.Instance.Services.Session;

            Click("Tab_Map");
            yield return null;
            Click("Node_ter_backyard");
            yield return null;
            Click("Prep_Fight");
            yield return null;
            Assert.That(session.World.PendingBattle, Is.Not.Null);
            Click("Combat_Skip");
            for (var i = 0; i < 5; i++) yield return null;
            Click("Results_Continue");
            yield return null;

            Assert.That(session.World.PendingBattle, Is.Null);
            Assert.That(session.World.Territories.Get(new TerritoryId("ter_backyard")).Attempts, Is.EqualTo(1));
            Assert.That(session.World.Cats[0].Activity.Status, Is.EqualTo(ActivityStatus.Idle));
        }

        private static IEnumerator BootGame()
        {
            SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
            yield return null;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!IsBooted())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Game scene was not loaded in time");
                yield return null;
            }

            yield return null;
        }

        private static bool IsBooted()
        {
            var root = Object.FindFirstObjectByType<GameSceneRoot>();
            return GameBootstrap.Instance != null && root != null && root.IsBooted
                && root.gameObject.scene == SceneManager.GetActiveScene();
        }

        private static void Click(string objectName)
        {
            var go = GameObject.Find(objectName);
            Assert.That(go, Is.Not.Null, objectName);
            var button = go.GetComponent<Button>();
            Assert.That(button.interactable, Is.True, objectName + " is not interactable");
            button.onClick.Invoke();
        }
    }
}
