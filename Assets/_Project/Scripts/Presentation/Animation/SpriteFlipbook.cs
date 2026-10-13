using System;
using UnityEngine;

namespace TrashCats.Presentation.Animation
{
    [Serializable]
    public sealed class FlipbookClip
    {
        public string name;
        public Sprite[] frames = Array.Empty<Sprite>();
        [Min(1)] public float fps = 10f;
        public bool loop = true;

        [Tooltip("Index in frames of each originally drawn frame (clips doubled by Tools/interpolate_adaptive.py); empty = every frame is a key.")]
        public int[] keys = Array.Empty<int>();

        /// <summary>Frame index of the originally drawn frame <paramref name="key"/>.</summary>
        public int KeyFrame(int key) => keys != null && key < keys.Length ? keys[key] : key;

        /// <summary>Seconds from the clip start to the originally drawn frame <paramref name="key"/>.</summary>
        public float KeyTime(int key) => KeyFrame(key) / fps;

        [Tooltip("Loop forward then backward (0..n-1..1) - for frames ordered by pose, e.g. a tail sway.")]
        public bool pingPong;

        [Tooltip("Optional per-frame attachment point (local units from the sprite pivot), e.g. where the tail joins the body.")]
        public Vector2[] attachPoints = Array.Empty<Vector2>();

        [Tooltip("Procedural breathing: relative vertical stretch (0 = off). Scales around the sprite pivot (the planted foot).")]
        [Range(0f, 0.05f)] public float breathe;
        [Min(0.1f)] public float breathePeriod = 2.4f;

        /// <summary>Frames in one loop: n, or 2n-2 when ping-ponging.</summary>
        public int SequenceLength => pingPong && frames.Length > 1 ? frames.Length * 2 - 2 : frames.Length;

        public float Duration => SequenceLength / fps;

        public int FrameAt(int step) => step < frames.Length ? step : frames.Length * 2 - 2 - step;

        public bool TryGetAttachPoint(int frame, out Vector2 point)
        {
            var has = attachPoints != null && attachPoints.Length > 0;
            point = has ? attachPoints[Mathf.Clamp(frame, 0, attachPoints.Length - 1)] : default;
            return has;
        }
    }

    /// <summary>
    /// Frame-by-frame sprite animation (Art Bible: generated comic spritesheets with hard cuts between frames).
    /// Plays one clip at a time; non-looping clips hold their last frame and raise <see cref="Finished"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;

        private FlipbookClip _clip;
        private float _time;
        private float _breathTime;
        private bool _finished;
        private Vector3 _baseScale;

        public event Action<FlipbookClip> Finished;

        public FlipbookClip Current => _clip;
        public int FrameIndex { get; private set; }

        private bool _initialized;

        private void Awake() => Init();

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;
            if (target == null)
                target = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        public void Play(FlipbookClip clip, bool restart = true)
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0)
                return;
            if (!restart && clip == _clip)
                return;

            Init();
            _clip = clip;
            _time = 0f;
            _finished = false;
            transform.localScale = _baseScale;
            SetFrame(0);
        }

        private void Update()
        {
            if (_clip == null)
                return;

            Breathe(Time.deltaTime);
            if (_finished)
                return;

            _time += Time.deltaTime;
            var step = (int)(_time * _clip.fps);
            if (step >= _clip.SequenceLength)
            {
                if (_clip.loop)
                {
                    _time %= _clip.Duration;
                    step = (int)(_time * _clip.fps) % _clip.SequenceLength;
                }
                else
                {
                    step = _clip.SequenceLength - 1;
                    _finished = true;
                }
            }

            var index = _clip.FrameAt(step);
            if (index != FrameIndex)
                SetFrame(index);
            if (_finished)
                Finished?.Invoke(_clip);
        }

        /// <summary>Shows one frame of a clip and stops advancing (inspection, screenshots).</summary>
        public void ShowFrame(FlipbookClip clip, int index)
        {
            Init();
            _clip = clip;
            _finished = true;
            SetFrame(Mathf.Clamp(index, 0, clip.frames.Length - 1));
        }

        private void Breathe(float deltaTime)
        {
            if (_clip.breathe <= 0f)
                return;

            _breathTime += deltaTime;
            var wave = Mathf.Sin(_breathTime * 2f * Mathf.PI / _clip.breathePeriod) * _clip.breathe;
            // stretch up while slightly narrowing keeps the volume; the pivot (planted foot) stays in place
            transform.localScale = new Vector3(_baseScale.x * (1f - wave * 0.5f), _baseScale.y * (1f + wave), _baseScale.z);
        }

        private void SetFrame(int index)
        {
            FrameIndex = index;
            target.sprite = _clip.frames[index];
        }
    }
}
