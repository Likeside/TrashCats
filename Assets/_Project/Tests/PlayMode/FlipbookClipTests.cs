using System.Linq;
using NUnit.Framework;
using TrashCats.Presentation.Animation;
using UnityEngine;

namespace TrashCats.Tests
{
    public sealed class FlipbookClipTests
    {
        [Test]
        public void PingPong_PlaysForwardThenBackwardWithoutRepeatingEnds()
        {
            var clip = new FlipbookClip { frames = new Sprite[4], fps = 10f, pingPong = true };

            Assert.That(clip.SequenceLength, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, clip.SequenceLength).Select(clip.FrameAt), Is.EqualTo(new[] { 0, 1, 2, 3, 2, 1 }));
            Assert.That(clip.Duration, Is.EqualTo(0.6f).Within(1e-5));
        }

        [Test]
        public void Loop_PlaysFramesInOrder()
        {
            var clip = new FlipbookClip { frames = new Sprite[3], fps = 6f };

            Assert.That(Enumerable.Range(0, clip.SequenceLength).Select(clip.FrameAt), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(clip.Duration, Is.EqualTo(0.5f).Within(1e-5));
        }
    }
}
