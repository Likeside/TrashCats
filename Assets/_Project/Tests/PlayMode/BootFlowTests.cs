using System.Collections;
using NUnit.Framework;
using TrashCats.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TrashCats.Tests
{
    public sealed class BootFlowTests
    {
        private const float TimeoutSeconds = 10f;

        [UnityTest]
        public IEnumerator Boot_LoadsGameScene_WithBootedRoot()
        {
            SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (SceneManager.GetActiveScene().name != GameBootstrap.GameSceneName)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Game scene was not loaded in time");
                yield return null;
            }

            Assert.That(GameBootstrap.Instance, Is.Not.Null);
            var root = Object.FindFirstObjectByType<GameSceneRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.IsBooted, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrap.Instance != null)
                Object.Destroy(GameBootstrap.Instance.gameObject);
            yield return null;
        }
    }
}
