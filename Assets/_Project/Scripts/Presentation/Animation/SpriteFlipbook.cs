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
        private bool _finished;

        public event Action<FlipbookClip> Finished;

        public FlipbookClip Current => _clip;
        public int FrameIndex { get; private set; }

        private void Awake()
        {
            if (target == null)
                target = GetComponent<SpriteRenderer>();
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
            SetFrame(0);
        }

        private void Update()
        {
            if (_clip == null || _finished)
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

        private void SetFrame(int index)
        {
            FrameIndex = index;
            target.sprite = _clip.frames[index];
        }
    }
}
