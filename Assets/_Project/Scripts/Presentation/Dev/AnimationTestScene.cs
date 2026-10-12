using System;
using System.Collections;
using System.IO;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.UI;
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

        [Header("Separate tail layer (shown only with the clip at tailClipIndex)")]
        [SerializeField] private SpriteFlipbook tail;
        [SerializeField] private FlipbookClip tailClip;
        [SerializeField] private int tailClipIndex;

        private UnityEngine.UI.Text _status;

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

        private void Play(FlipbookClip clip)
        {
            character.Play(clip);
            var withTail = tail != null && Array.IndexOf(clips, clip) == tailClipIndex;
            if (tail == null)
                return;
            tail.gameObject.SetActive(withTail);
            if (withTail)
                tail.Play(tailClip);
        }

        private IEnumerator CaptureAll(string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(0.5f);
            foreach (var clip in clips)
            {
                Play(clip);
                enabled = false;
                character.enabled = false;
                var tailFrames = tail != null && tail.gameObject.activeSelf ? tailClip.frames.Length : 0;
                if (tail != null) tail.enabled = false;
                for (var f = 0; f < Mathf.Max(clip.frames.Length, tailFrames); f++)
                {
                    character.GetComponent<SpriteRenderer>().sprite = clip.frames[Mathf.Min(f, clip.frames.Length - 1)];
                    if (tailFrames > 0)
                        tail.GetComponent<SpriteRenderer>().sprite = tailClip.frames[f % tailFrames];
                    _status.text = $"{clip.name} · кадр {f + 1}";
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{clip.name}_{f:00}.png"));
                    yield return null;
                }

                character.enabled = true;
                if (tail != null) tail.enabled = true;
                enabled = true;
            }

            // live recording of the first clip (with procedural effects) for smoothness review
            Play(clips[0]);
            for (var t = 0; t < 24; t++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{clips[0].name}_live_{t:00}.png"));
                yield return new WaitForSeconds(0.1f);
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
