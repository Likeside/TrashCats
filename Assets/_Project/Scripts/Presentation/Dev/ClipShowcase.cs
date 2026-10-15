using System.Collections;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.Characters;
using UnityEngine;

namespace TrashCats.Presentation.Dev
{
    /// <summary>
    /// Dev scene "AllAnimations": plays one clip of one character from the art registry, over and over (a clip that
    /// does not loop — attack, hit, knock-out — holds its last frame for a moment and starts again). A role "pose_N"
    /// shows the N-th static hideout pose.
    /// </summary>
    [RequireComponent(typeof(SpriteFlipbook))]
    public sealed class ClipShowcase : MonoBehaviour
    {
        public CharacterArtRegistry registry;
        public string viewKey;
        public string role;
        [SerializeField] private float holdAtEnd = 0.6f;

        private IEnumerator Start()
        {
            Application.runInBackground = true; // keeps playing while the window is not focused (review, screenshots)
            var art = registry != null ? registry.Find(viewKey) : null;
            var flipbook = GetComponent<SpriteFlipbook>();
            var renderer = GetComponent<SpriteRenderer>();
            if (art == null)
                yield break;
            renderer.color = art.tint;

            if (role.StartsWith("pose_") && int.TryParse(role.Substring(5), out var pose))
            {
                renderer.sprite = art.HideoutPose(pose);
                yield break;
            }

            var source = Clip(art, role);
            if (source == null || source.frames == null || source.frames.Length == 0)
                yield break;
            // a copy: the registry's clip asset is not changed
            var clip = JsonUtility.FromJson<FlipbookClip>(JsonUtility.ToJson(source));
            clip.frames = source.frames;
            if (clip.loop)
            {
                flipbook.Play(clip);
                yield break;
            }
            while (true)
            {
                flipbook.Play(clip);
                yield return new WaitForSeconds(clip.Duration + holdAtEnd);
            }
        }

        public static FlipbookClip Clip(CharacterArt art, string role) => role switch
        {
            "idle" => art.idle,
            "attack" => art.attack,
            "heavyAttack" => art.heavyAttack,
            "hit" => art.hit,
            "knockOut" => art.knockOut,
            "tail" => art.tail,
            "hideoutIdle" => art.hideoutIdle,
            "rope" => art.rope,
            "dig" => art.dig,
            "run" => art.run,
            "bench" => art.bench,
            "canPunch" => art.canPunch,
            _ => null,
        };
    }
}
