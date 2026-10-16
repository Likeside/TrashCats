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
            // "-focus <viewKey>_<role>": the camera looks at that object (builds have no Scene view to look around)
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-focus");
            if (at >= 0 && at + 1 < args.Length && args[at + 1] == name && Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y + 2.5f, -10f);
                Camera.main.orthographicSize = 4.5f;
            }
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
                _clip = clip;
                _flipbook = flipbook;
                if (clip.handles != null && clip.handles.Length > 0) BuildHeldProps(art);
                yield break;
            }
            while (true)
            {
                flipbook.Play(clip);
                yield return new WaitForSeconds(clip.Duration + holdAtEnd);
            }
        }

        // --- the rope and the barbell the game draws in the hands (as in the hideout, HideoutSceneView.HeldProps) ---

        private FlipbookClip _clip;
        private SpriteFlipbook _flipbook;
        private LineRenderer _ropeInk;
        private LineRenderer _rope;
        private SpriteRenderer _barbell;

        private void BuildHeldProps(CharacterArt art)
        {
            if (role == "rope")
            {
                var material = new Material(Shader.Find("Sprites/Default"));
                _ropeInk = Line("RopeInk", material, new Color(0.11f, 0.08f, 0.06f), 0.095f);
                _rope = Line("Rope", material, new Color(0.62f, 0.4f, 0.2f), 0.05f);
            }
            else if (role == "bench" && registry.GetSprite("prop_barbell") is Sprite sprite)
            {
                // the sprite's pivot is its bottom centre: a holder at the grip with the sprite half its height lower
                var holder = new GameObject("HeldBarbell").transform;
                holder.SetParent(transform, false);
                var go = new GameObject("Sprite");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(0f, -sprite.rect.height / sprite.pixelsPerUnit * 0.5f, 0f);
                _barbell = go.AddComponent<SpriteRenderer>();
                _barbell.sprite = sprite;
                _barbell.sortingOrder = 1;
            }
        }

        private LineRenderer Line(string name, Material material, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            return line;
        }

        private void LateUpdate()
        {
            if (_clip == null || _clip.handles == null || _clip.handles.Length == 0)
                return;
            var frame = _clip.frames[0].rect.size / 100f; // sprites: 100 px per unit, pivot at the bottom centre
            Vector3 Local(Vector2 n) => new Vector3((n.x - 0.5f) * frame.x, n.y * frame.y, 0f);
            var h = _clip.handles[Mathf.Clamp(_flipbook.FrameIndex, 0, _clip.handles.Length - 1)];
            var left = new Vector2(h.x, h.y);
            var right = new Vector2(h.z, h.w);
            if (_rope != null)
            {
                var points = Animation.RopeGraphic.Curve(left, right, _flipbook.Phase, 1.02f, 0.02f);
                var behind = Animation.RopeGraphic.IsBehind(_flipbook.Phase);
                foreach (var (line, order) in new[] { (_ropeInk, behind ? -2 : 1), (_rope, behind ? -1 : 2) })
                {
                    line.positionCount = points.Length;
                    for (var i = 0; i < points.Length; i++)
                        line.SetPosition(i, Local(points[i]));
                    line.sortingOrder = order;
                }
            }
            if (_barbell != null)
            {
                var a = Local(left);
                var b = Local(right);
                var d = b - a;
                var holder = _barbell.transform.parent;
                holder.localPosition = (a + b) * 0.5f;
                holder.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                // the same share of the frame's width as in the hideout
                var size = _barbell.sprite.rect.size / _barbell.sprite.pixelsPerUnit;
                holder.localScale = Vector3.one * (Hideout.HideoutSceneView.HeldBarbellWidth * frame.x / size.x);
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
