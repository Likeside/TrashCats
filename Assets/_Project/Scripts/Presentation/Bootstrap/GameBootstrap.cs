using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Composition Root. Lives in the Boot scene, survives scene loads and owns the game services.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string BootSceneName = "Boot";
        public const string GameSceneName = "Game";

        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
