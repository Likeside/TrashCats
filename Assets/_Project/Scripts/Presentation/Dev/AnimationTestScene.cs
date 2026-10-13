using System;
using System.Collections;
using System.IO;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;

namespace TrashCats.Presentation.Dev
{
    /// <summary>
    /// Development scene for reviewing character animations: the character plus one button per clip.
    /// With the command-line argument "-captureDir &lt;dir&gt;" it saves a screenshot of every frame of every clip and quits
    /// (used to review Linux builds without a person at the screen).
    /// </summary>
    public sealed class AnimationTestScene : MonoBehaviour
    {
        [SerializeField] private SpriteFlipbook character;
        [SerializeField] private FlipbookClip[] clips = Array.Empty<FlipbookClip>();
        [SerializeField] private string[] buttonLabels = Array.Empty<string>();

        [Header("Separately animated tail, glued to the body's per-frame attach point")]
        [SerializeField] private SpriteFlipbook tail;
        [SerializeField] private FlipbookClip tailClip;

        private TMP_Text _status;

        private void Start()
        {
            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas("UI", 0);
            var safe = Ui.Rect(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _status = Ui.Label(safe, "", 34, TextAnchor.UpperCenter, Palette.Paper);
            _status.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(20, -120), new Vector2(-20, -30));

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                var label = i < buttonLabels.Length ? buttonLabels[i] : clip.name;
                var button = Ui.Button(safe, "Play_" + clip.name, label, i == 0 ? Palette.Primary : Palette.Secondary, () => Play(clip), 44);
                var width = 1f / clips.Length;
                button.Rect.Place(new Vector2(i * width, 0), new Vector2((i + 1) * width, 0), new Vector2(30, 60), new Vector2(-30, 220));
            }

            if (tail != null)
                tail.Play(tailClip);
            if (clips.Length > 0)
                Play(clips[0]);

            var captureDir = ArgValue("-captureDir");
            if (captureDir != null)
                StartCoroutine(CaptureAll(captureDir));
        }

        private void Update()
        {
            if (character.Current != null)
                _status.text = $"{character.Current.name} · кадр {character.FrameIndex + 1}/{character.Current.frames.Length} · {character.Current.fps} fps";
        }

        private void Play(FlipbookClip clip) => character.Play(clip);

        private IEnumerator CaptureAll(string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(0.5f);
            enabled = false;

            // every body frame, with the tail frame that would be shown alongside it
            foreach (var clip in clips)
            {
                for (var f = 0; f < clip.frames.Length; f++)
                {
                    character.ShowFrame(clip, f);
                    if (tail != null)
                        tail.ShowFrame(tailClip, f % tailClip.frames.Length);
                    _status.text = $"{clip.name} · кадр {f + 1}/{clip.frames.Length}";
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{clip.name}_{f:00}.png"));
                    yield return null;
                }
            }

            // live recordings of each clip with the independent tail and procedural effects
            enabled = true;
            if (tail != null)
                tail.Play(tailClip);
            foreach (var clip in clips)
            {
                Play(clip);
                for (var t = 0; t < 24; t++)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{clip.name}_live_{t:00}.png"));
                    yield return new WaitForSeconds(0.1f);
                }
            }

            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        private static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
