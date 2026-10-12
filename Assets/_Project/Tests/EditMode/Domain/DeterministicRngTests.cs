using System;
using System.Linq;
using NUnit.Framework;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class DeterministicRngTests
    {
        // Golden values computed by an independent reference implementation (Python) of the same algorithm.
        [Test]
        public void Sequence_MatchesGoldenValues()
        {
            var rng = new DeterministicRng(12345);
            var values = Enumerable.Range(0, 5).Select(_ => rng.NextUInt()).ToArray();

            Assert.That(values, Is.EqualTo(new uint[] { 3762511623, 4292648982, 269806297, 616432574, 2113899162 }));
        }

        [Test]
        public void NextInt_MatchesGoldenValues()
        {
            var rng = new DeterministicRng(12345);
            Assert.That(Enumerable.Range(0, 8).Select(_ => rng.NextInt(0, 1000)), Is.EqualTo(new[] { 876, 999, 62, 143, 492, 98, 785, 197 }));

            var negative = new DeterministicRng(-7);
            Assert.That(Enumerable.Range(0, 5).Select(_ => negative.NextInt(-50, 50)), Is.EqualTo(new[] { 49, 12, -30, 31, -32 }));
        }

        [Test]
        public void SameSeed_SameSequence_DifferentSeed_DifferentSequence()
        {
            var a = new DeterministicRng(42);
            var b = new DeterministicRng(42);
            var c = new DeterministicRng(43);

            var seqA = Enumerable.Range(0, 100).Select(_ => a.NextUInt()).ToArray();
            Assert.That(Enumerable.Range(0, 100).Select(_ => b.NextUInt()), Is.EqualTo(seqA));
            Assert.That(Enumerable.Range(0, 100).Select(_ => c.NextUInt()), Is.Not.EqualTo(seqA));
        }

        [Test]
        public void NextInt_StaysInRange_AndCoversIt()
        {
            var rng = new DeterministicRng(1);
            var seen = new bool[10];
            for (var i = 0; i < 10_000; i++)
            {
                var v = rng.NextInt(5, 15);
                Assert.That(v, Is.InRange(5, 14));
                seen[v - 5] = true;
            }

            Assert.That(seen, Has.All.True);
        }

        [Test]
        public void NextInt_InvalidRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRng(1).NextInt(3, 3));
        }

        [Test]
        public void RollPermille_ApproximatesChance()
        {
            var rng = new DeterministicRng(7);
            var hits = Enumerable.Range(0, 20_000).Count(_ => rng.RollPermille(750));

            Assert.That(hits / 20_000.0, Is.EqualTo(0.75).Within(0.01));
            Assert.That(rng.RollPermille(0), Is.False);
            Assert.That(rng.RollPermille(1000), Is.True);
        }

        [Test]
        public void CombineSeeds_AndStableHash_MatchGoldenValues()
        {
            Assert.That(DeterministicRng.CombineSeeds(1, 2, 3), Is.EqualTo(2034659765));
            Assert.That(DeterministicRng.CombineSeeds(3, 2, 1), Is.EqualTo(1261589429));
            Assert.That(DeterministicRng.StableHash("ter_backyard"), Is.EqualTo(-1944010060));
            Assert.That(DeterministicRng.StableHash("кот"), Is.EqualTo(-1935689737));
        }
    }
}
