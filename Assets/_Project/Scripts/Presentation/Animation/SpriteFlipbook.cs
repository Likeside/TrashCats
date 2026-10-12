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

        [Tooltip("Procedural breathing: relative vertical stretch (0 = off). Scales around the sprite pivot (the planted foot).")]
        [Range(0f, 0.05f)] public float breathe;
        [Min(0.1f)] public float breathePeriod = 2.4f;

        public float Duration => frames.Length / fps;
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

        private void Awake()
        {
            if (target == null)
                target = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        public void Play(FlipbookClip clip, bool restart = true)
        {
            if (clip == null || clip.frames.Length == 0)
                return;
            if (!restart && clip == _clip)
                return;

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
            var index = (int)(_time * _clip.fps);
            if (index >= _clip.frames.Length)
            {
                if (_clip.loop)
                {
                    _time %= _clip.Duration;
                    index = (int)(_time * _clip.fps) % _clip.frames.Length;
                }
                else
                {
                    index = _clip.frames.Length - 1;
                    _finished = true;
                }
            }

            if (index != FrameIndex)
                SetFrame(index);
            if (_finished)
                Finished?.Invoke(_clip);
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
