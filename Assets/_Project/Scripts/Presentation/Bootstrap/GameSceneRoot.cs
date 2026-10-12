using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Entry point of the Game scene. Builds the screens from the services created by <see cref="GameBootstrap"/>,
    /// then lets time flow. When the Game scene is started directly in the editor, redirects to the Boot scene.
    /// </summary>
    public sealed class GameSceneRoot : MonoBehaviour
    {
        private HideoutScreen _hideout;

        public bool IsBooted { get; private set; }

        private void Awake()
        {
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
                return;
            }

            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas("UI", 0);
            canvas.transform.SetParent(transform, false);
            Ui.Panel(canvas.transform, "Background", Palette.Background).rectTransform.Fill();
            var safe = Ui.Rect(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var debugTools = Debug.isDebugBuild || Application.isEditor;
            _hideout = new HideoutScreen(bootstrap.Services, safe, debugTools, bootstrap.ResetProgress);

            IsBooted = true;
            bootstrap.StartTicking();
        }

        private void Update()
        {
            if (_hideout == null)
                return;

            _hideout.Tick();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _hideout.Overlays.Back();
        }

        private void OnDestroy() => _hideout?.Dispose();
    }
}
