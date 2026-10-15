using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Animation
{
    /// <summary>
    /// Plays a <see cref="FlipbookClip"/> on a UI Image (hideout cats and props); reports frame changes. A clip with
    /// <see cref="FlipbookClip.breathe"/> also breathes: a slow stretch up from the bottom pivot (a static idle picture).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class UiFlipbook : MonoBehaviour
    {
        private Image _image;
        private FlipbookClip _clip;
        private float _time;
        private int _frame = -1;
        private float _breathTime;
        private bool _breathing;

        public FlipbookClip Clip => _clip;
        /// <summary>Frame shown now.</summary>
        public int Frame => _frame;
        /// <summary>Position in the loop, 0..1 (continuous, between frames too).</summary>
        public float Phase => _clip == null ? 0f : Mathf.Repeat(_time * _clip.fps, _clip.SequenceLength) / _clip.SequenceLength;
        public event Action<int> FrameChanged;

        /// <summary>Plays from the first frame; null or empty clips stop on the current picture.</summary>
        public void Play(FlipbookClip clip)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (clip == _clip) return;
            _clip = clip != null && clip.frames != null && clip.frames.Length > 0 ? clip : null;
            _time = 0f;
            _frame = -1;
            ResetBreath();
            Apply(0);
        }

        public void Stop()
        {
            _clip = null;
            ResetBreath();
        }

        private void ResetBreath()
        {
            if (!_breathing) return;
            _breathing = false;
            var scale = transform.localScale;
            transform.localScale = new Vector3(Mathf.Sign(scale.x), 1f, scale.z);
        }

        private void Update()
        {
            if (_clip == null) return;
            if (_clip.breathe > 0f)
            {
                // the side the picture faces (the x sign) is kept; the bottom pivot stays planted
                _breathing = true;
                _breathTime += Time.deltaTime;
                var wave = Mathf.Sin(_breathTime * 2f * Mathf.PI / _clip.breathePeriod) * _clip.breathe;
                var scale = transform.localScale;
                transform.localScale = new Vector3(Mathf.Sign(scale.x) * (1f - wave * 0.5f), 1f + wave, scale.z);
            }
            _time += Time.deltaTime;
            var step = (int)(_time * _clip.fps);
            var length = _clip.SequenceLength;
            step = _clip.loop ? step % length : Mathf.Min(step, length - 1);
            Apply(step);
        }

        private void Apply(int step)
        {
            if (_clip == null) return;
            var frame = _clip.FrameAt(step);
            if (frame == _frame) return;
            _frame = frame;
            _image.sprite = _clip.frames[frame];
            FrameChanged?.Invoke(frame);
        }
    }
}
