using System;
using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    public class RngStreamsTests {
        static int[] Draw(Random random, int count) {
            int[] values = new int[count];
            for (int i = 0; i < count; i++) {
                values[i] = random.Next();
            }
            return values;
        }

        [Test]
        public void SameSeedAndNameGiveTheSameSequence() {
            CollectionAssert.AreEqual(Draw(new RngStreams(42).Get(RngStreams.Manifest), 20),
                Draw(new RngStreams(42).Get(RngStreams.Manifest), 20));
        }

        [Test]
        public void DifferentNamesDiffer() {
            RngStreams streams = new RngStreams(42);
            CollectionAssert.AreNotEqual(Draw(streams.Get(RngStreams.Manifest), 20), Draw(streams.Get(RngStreams.Seating), 20));
        }

        [Test]
        public void DifferentSeedsDiffer() {
            CollectionAssert.AreNotEqual(Draw(new RngStreams(1).Get(RngStreams.Scare), 20),
                Draw(new RngStreams(2).Get(RngStreams.Scare), 20));
        }

        [Test]
        public void ExtraDrawsOnOneStreamDontChangeAnother() {
            RngStreams quiet = new RngStreams(7);
            RngStreams busy = new RngStreams(7);
            Draw(busy.Get(RngStreams.Hallucination), 500);
            CollectionAssert.AreEqual(Draw(quiet.Get(RngStreams.Manifest), 20), Draw(busy.Get(RngStreams.Manifest), 20));
        }

        [Test]
        public void GetReturnsTheSameContinuingStream() {
            RngStreams streams = new RngStreams(9);
            Assert.AreSame(streams.Get(RngStreams.Decoy), streams.Get(RngStreams.Decoy));
        }

        [Test]
        public void StreamSeedIsNonNegativeForExtremeSeeds() {
            foreach (int seed in new[] { int.MinValue, -1, 0, int.MaxValue }) {
                Assert.GreaterOrEqual(RngStreams.StreamSeed(seed, RngStreams.Menu), 0);
            }
        }
    }
}
