using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Entry point of the Game scene. Receives services from <see cref="GameBootstrap"/> and builds the views.
    /// When the Game scene is started directly in the editor, redirects to the Boot scene.
    /// </summary>
    public sealed class GameSceneRoot : MonoBehaviour
    {
        public bool IsBooted { get; private set; }

        private void Awake()
        {
            if (GameBootstrap.Instance == null)
            {
                SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
                return;
            }

            IsBooted = true;
        }
    }
}
