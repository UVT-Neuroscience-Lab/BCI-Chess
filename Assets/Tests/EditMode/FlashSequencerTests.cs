using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using NUnit.Framework;

namespace BciChess.Tests
{
    public class FlashSequencerTests
    {
        private const float Step = 0.001f; // 1 ms

        private static List<int> RecordFlashes(FlashSequencer sequencer, IReadOnlyList<int> slots, int rounds)
        {
            var flashes = new List<int>();
            sequencer.FlashStarted += flashes.Add;
            sequencer.Start(slots);
            while (sequencer.RoundsCompleted < rounds)
                sequencer.Tick(Step);
            return flashes.Take(rounds * slots.Count).ToList();
        }

        [Test]
        public void EverySlotFlashesExactlyOncePerRound()
        {
            var slots = new[] { 0, 1, 2, 3, 4, 5 };
            var flashes = RecordFlashes(new FlashSequencer(100, 75, seed: 42), slots, rounds: 20);

            for (int round = 0; round < 20; round++)
                CollectionAssert.AreEquivalent(slots, flashes.Skip(round * slots.Length).Take(slots.Length));
        }

        [Test]
        public void SameSlotNeverFlashesTwiceInARow()
        {
            var flashes = RecordFlashes(new FlashSequencer(100, 75, seed: 7), new[] { 0, 1, 2 }, rounds: 200);
            for (int i = 1; i < flashes.Count; i++)
                Assert.AreNotEqual(flashes[i - 1], flashes[i], $"Repeat at flash {i}");
        }

        [Test]
        public void OnAndOffTimesAreRespected()
        {
            var sequencer = new FlashSequencer(100, 50, seed: 1);
            sequencer.Start(new[] { 3, 8 });
            int first = sequencer.LitSlot.Value;

            sequencer.Tick(0.099f);
            Assert.AreEqual(first, sequencer.LitSlot, "Still lit just before on-time ends");
            sequencer.Tick(0.002f);
            Assert.IsNull(sequencer.LitSlot, "Dark during off-time");
            sequencer.Tick(0.048f);
            Assert.IsNull(sequencer.LitSlot);
            sequencer.Tick(0.002f);
            Assert.IsNotNull(sequencer.LitSlot, "Next flash after on + off");
            Assert.AreNotEqual(first, sequencer.LitSlot.Value);
        }

        [Test]
        public void LargeTimeStep_AdvancesSeveralFlashes()
        {
            var sequencer = new FlashSequencer(100, 100, seed: 3);
            int flashes = 0;
            sequencer.FlashStarted += _ => flashes++;
            sequencer.Start(new[] { 0, 1, 2, 3 });
            sequencer.Tick(0.95f); // flashes start at 0, 0.2, 0.4, 0.6 and 0.8 s
            Assert.AreEqual(5, flashes);
        }

        [Test]
        public void SingleSlot_FlashesRepeatedly()
        {
            var sequencer = new FlashSequencer(100, 100, seed: 3);
            int flashes = 0;
            sequencer.FlashStarted += _ => flashes++;
            sequencer.Start(new[] { 5 });
            sequencer.Tick(0.65f);
            Assert.AreEqual(4, flashes);
        }

        [Test]
        public void Stop_TurnsEverythingOff()
        {
            var sequencer = new FlashSequencer(100, 75, seed: 3);
            sequencer.Start(new[] { 0, 1 });
            Assert.IsTrue(sequencer.IsRunning);
            sequencer.Stop();
            Assert.IsFalse(sequencer.IsRunning);
            Assert.IsNull(sequencer.LitSlot);
            sequencer.Tick(1f);
            Assert.IsNull(sequencer.LitSlot);
        }

        [Test]
        public void EmptySlotList_DoesNotRun()
        {
            var sequencer = new FlashSequencer(100, 75);
            sequencer.Start(new int[0]);
            Assert.IsFalse(sequencer.IsRunning);
            Assert.IsNull(sequencer.LitSlot);
        }
    }
}
