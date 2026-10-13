using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Animation
{
    /// <summary>Plays a <see cref="FlipbookClip"/> on a UI Image (hideout cats and props); reports frame changes.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class UiFlipbook : MonoBehaviour
    {
        private Image _image;
        private FlipbookClip _clip;
        private float _time;
        private int _frame = -1;

        public FlipbookClip Clip => _clip;
        public event Action<int> FrameChanged;

        /// <summary>Plays from the first frame; null or empty clips stop on the current picture.</summary>
        public void Play(FlipbookClip clip)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (clip == _clip) return;
            _clip = clip != null && clip.frames != null && clip.frames.Length > 0 ? clip : null;
            _time = 0f;
            _frame = -1;
            Apply(0);
        }

        public void Stop() => _clip = null;

        private void Update()
        {
            if (_clip == null) return;
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
