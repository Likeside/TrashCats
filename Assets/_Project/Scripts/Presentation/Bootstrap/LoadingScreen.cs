using System.Collections;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Loading screen of the Boot scene: the gang in the alley (Art/UI/loading_bg), the logo and a bar. It loads
    /// the Game scene in the background and stays over it until the scene has built its screens, then fades out.
    /// Shown for at least <see cref="MinSeconds"/>, so it does not flash by on a fast device.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        private const float MinSeconds = 1.2f;
        private const float FadeSeconds = 0.35f;

        private CanvasGroup _group;
        private UiBar _bar;
        private float _shown;

        public static LoadingScreen Show(Sprite background, Sprite logo, UiSkin skin)
        {
            Ui.Skin = skin;
            var canvas = Ui.CreateCanvas("LoadingScreen", 100);
            DontDestroyOnLoad(canvas.gameObject);
            var screen = canvas.gameObject.AddComponent<LoadingScreen>();
            screen._group = canvas.gameObject.AddComponent<CanvasGroup>();
            screen.Build(canvas.transform, background, logo);
            return screen;
        }

        private void Build(Transform root, Sprite background, Sprite logo)
        {
            Ui.Panel(root, "Back", new Color32(0x1B, 0x16, 0x2A, 0xFF), raycast: true).rectTransform.Fill();
            if (background != null)
            {
                // the picture covers the screen; on tall phones the sky and the ground are cut, the cats stay
                var frame = Ui.Rect(root, "Picture");
                var fit = frame.gameObject.AddComponent<CoverFit>();
                fit.aspect = background.rect.width / background.rect.height;
                fit.focus = new Vector2(0.5f, 0.5f);
                fit.Apply();
                Ui.Picture(frame, "Background", background).rectTransform.Fill();
            }

            // the logo (a tin plate with the title, Art/UI/logo) hangs in the sky, a little askew
            if (logo != null)
            {
                var plate = Ui.Picture(root, "Logo", logo);
                plate.preserveAspect = true;
                plate.rectTransform.Place(new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), new Vector2(0, -560), new Vector2(0, -90));
                plate.rectTransform.localEulerAngles = new Vector3(0, 0, -3f);
            }
            else
            {
                var title = Ui.Label(root, "TRASH CATS", 150, TextAnchor.MiddleCenter, Palette.Primary, heading: true);
                title.rectTransform.Place(new Vector2(0f, 0.8f), new Vector2(1f, 0.95f), new Vector2(40, 0), new Vector2(-40, 0));
            }

            _bar = Ui.Bar(root, "LoadingBar", Palette.Primary);
            _bar.Rect.Place(new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0, 150), new Vector2(0, 196));
            _bar.Set(0f);
            var label = Ui.Label(root, Loc.T("loading.label"), 40, TextAnchor.MiddleCenter, Palette.Paper);
            label.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 205), new Vector2(0, 265));
        }

        /// <summary>Loads <paramref name="scene"/> and hides once <paramref name="ready"/> says it is built.</summary>
        public void Load(string scene, System.Func<bool> ready) => StartCoroutine(Run(scene, ready));

        private IEnumerator Run(string scene, System.Func<bool> ready)
        {
            _shown = Time.unscaledTime;
            // a frame with the screen drawn before the heavy loading starts
            yield return null;
            // dev review of builds: "-captureLoading <png>" saves the loading screen
            var args = System.Environment.GetCommandLineArgs();
            var capture = System.Array.IndexOf(args, "-captureLoading");
            if (capture >= 0 && capture + 1 < args.Length)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(args[capture + 1]);
                yield return null;
            }
            var load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!load.isDone)
            {
                Progress(load.progress / 0.9f * 0.8f);
                yield return null;
            }
            while (!ready())
            {
                Progress(0.9f);
                yield return null;
            }
            while (Time.unscaledTime - _shown < MinSeconds)
            {
                Progress(Mathf.Lerp(0.9f, 1f, (Time.unscaledTime - _shown) / MinSeconds));
                yield return null;
            }
            Progress(1f);
            for (var t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            Destroy(gameObject);
        }

        private float _progress;

        /// <summary>The bar only moves forward, smoothly.</summary>
        private void Progress(float target)
        {
            _progress = Mathf.Max(_progress, Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime * 1.5f));
            _bar.Set(_progress);
        }
    }
}
