using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using NUnit.Framework;

namespace BciChess.Tests
{
    public class NeighbourSeparationTests
    {
        private static BciTarget At(string id, float x, float y) => new BciTarget(id, id, position: new TargetPosition(x, y));

        private static double Ring(int a, int b, int size)
        {
            int d = System.Math.Abs(a - b);
            return System.Math.Min(d, size - d);
        }

        [Test]
        public void Graph_TouchingSquaresAreNeighbours()
        {
            var targets = new[] { At("a1", 0, 0), At("b2", 1, 1), At("c1", 2, 0), At("h8", 7, 7), new BciTarget("cancel", "Cancel") };
            var graph = NeighbourGraph.Build(targets, 1.5f);

            Assert.IsTrue(graph.AreNeighbours(0, 1), "Diagonal");
            Assert.IsTrue(graph.AreNeighbours(1, 2), "Diagonal");
            Assert.IsFalse(graph.AreNeighbours(0, 2), "Two files apart");
            Assert.IsEmpty(graph.NeighboursOf(3));
            Assert.IsEmpty(graph.NeighboursOf(4), "No position: never a neighbour");
        }

        [Test]
        public void Graph_GroupsAreNeighboursWhenAnyMembersTouch()
        {
            var left = new BciTarget("g0", "left", new CandidateGroup(new[] { At("a2", 0, 1), At("b2", 1, 1) }));
            var middle = new BciTarget("g1", "middle", new CandidateGroup(new[] { At("c3", 2, 2), At("c7", 2, 6) }));
            var right = new BciTarget("g2", "right", new CandidateGroup(new[] { At("g2", 6, 1), At("h2", 7, 1) }));
            var graph = NeighbourGraph.Build(new[] { left, middle, right }, 1.5f);

            Assert.IsTrue(graph.AreNeighbours(0, 1), "b2 touches c3");
            Assert.IsFalse(graph.AreNeighbours(1, 2));
            Assert.IsFalse(graph.AreNeighbours(0, 2));
        }

        [Test]
        public void Assignment_GivesNeighboursFarApartSlots()
        {
            var manager = new StimulusManager(Enumerable.Range(1, 6), 6);
            var line = new[] { At("t0", 0, 0), At("t1", 1, 0), At("t2", 2, 0), At("t3", 3, 0) };
            var graph = NeighbourGraph.Build(line, 1.5f);

            Assert.IsTrue(manager.TryAssign(line, graph, out var assigned));
            CollectionAssert.AreEqual(line.Select(t => t.Id), assigned.Select(t => t.Id), "Order kept");
            Assert.AreEqual(4, assigned.Select(t => t.Stimulus.Value.Index).Distinct().Count(), "Unique slots");

            foreach (var (a, b) in graph.Pairs())
            {
                double separation = Ring(assigned[a].Stimulus.Value.Index, assigned[b].Stimulus.Value.Index, 6);
                Assert.GreaterOrEqual(separation, 2, $"{line[a].Id}-{line[b].Id}");
            }
        }

        [Test]
        public void Assignment_BeatsSequentialOrderForNeighbours()
        {
            var manager = new StimulusManager(Enumerable.Range(1, 8), 8);
            var line = Enumerable.Range(0, 5).Select(i => At("t" + i, i, 0)).ToList();
            var graph = NeighbourGraph.Build(line, 1.5f);

            manager.TryAssign(line, out var sequential);
            manager.TryAssign(line, graph, out var separated);

            double MinNeighbourGap(IReadOnlyList<BciTarget> a) =>
                graph.Pairs().Min(p => Ring(a[p.Item1].Stimulus.Value.Index, a[p.Item2].Stimulus.Value.Index, 8));

            Assert.AreEqual(1, MinNeighbourGap(sequential));
            Assert.Greater(MinNeighbourGap(separated), MinNeighbourGap(sequential));
        }

        [Test]
        public void Assignment_UsesCustomSlotDistance()
        {
            // Slots 0 and 1 look identical, all others very different: neighbours must not get 0 and 1.
            var manager = new StimulusManager(new[] { 1, 2, 3 }, 3);
            var pair = new[] { At("a", 0, 0), At("b", 1, 0) };
            double Distance(int x, int y) => (x <= 1 && y <= 1) ? 0 : 10;

            manager.TryAssign(pair, NeighbourGraph.Build(pair, 1.5f), out var assigned, Distance);
            var slots = assigned.Select(t => t.Stimulus.Value.Index).OrderBy(s => s).ToList();
            CollectionAssert.AreNotEqual(new[] { 0, 1 }, slots);
        }

        [Test]
        public void Sequencer_KeepsNeighboursApartInTime()
        {
            var sequencer = new FlashSequencer(100, 75, seed: 11);
            var flashes = new List<int>();
            sequencer.FlashStarted += flashes.Add;
            var chain = new[] { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5) };
            sequencer.Start(new[] { 0, 1, 2, 3, 4, 5 }, chain, neighbourSpacing: 2);
            while (sequencer.RoundsCompleted < 100)
                sequencer.Tick(0.001f);

            Assert.AreEqual(0, sequencer.RoundsWithSpacingViolations);
            for (int round = 0; round < 100; round++)
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, flashes.Skip(round * 6).Take(6));
            for (int i = 1; i < 600; i++)
            {
                Assert.AreNotEqual(flashes[i - 1], flashes[i]);
                Assert.IsFalse(chain.Contains((flashes[i - 1], flashes[i])) || chain.Contains((flashes[i], flashes[i - 1])),
                    $"Neighbours {flashes[i - 1]} and {flashes[i]} flashed back to back at {i}");
            }
        }

        [Test]
        public void Sequencer_FallsBackGracefullyWhenSpacingIsImpossible()
        {
            // Only two slots and they are neighbours: they must alternate, so spacing 2 cannot hold.
            var sequencer = new FlashSequencer(100, 75, seed: 3);
            var flashes = new List<int>();
            sequencer.FlashStarted += flashes.Add;
            sequencer.Start(new[] { 0, 1 }, new[] { (0, 1) }, neighbourSpacing: 2);
            while (sequencer.RoundsCompleted < 10)
                sequencer.Tick(0.001f);

            Assert.Greater(sequencer.RoundsWithSpacingViolations, 0);
            for (int i = 1; i < 20; i++)
                Assert.AreNotEqual(flashes[i - 1], flashes[i], "Still no immediate repeats");
        }

        [Test]
        public void Controller_ReportsNeighbourPairsOfCurrentTargets()
        {
            var game = new ChessGame();
            var selection = new SelectionController(game);
            var bci = new BciSelectionController(selection, new FakeBciSelector(), new StimulusManager(Enumerable.Range(1, 12), 12))
            {
                Enabled = true
            };

            var squareBySlot = bci.Targets.ToDictionary(t => t.Stimulus.Value.Index,
                t => ((ChessTargetPayload)t.Payload).Square);
            Assert.IsNotEmpty(bci.NeighbourSlotPairs);
            foreach (var (a, b) in bci.NeighbourSlotPairs)
            {
                var sa = squareBySlot[a];
                var sb = squareBySlot[b];
                Assert.LessOrEqual(System.Math.Max(System.Math.Abs(sa.File - sb.File), System.Math.Abs(sa.Rank - sb.Rank)), 1,
                    $"{sa} and {sb} are not adjacent");
            }
        }
    }

    public class DestinationFlashTests
    {
        // White rook a1 with ten legal moves: a2..a8 in one line plus b1, c1, d1.
        private const string RookFen = "4k3/8/8/8/8/8/8/R3K3 w - - 0 1";

        private static (BciSelectionController bci, Dictionary<int, Square> squareBySlot) ChooseRookDestinations()
        {
            var game = new ChessGame(RookFen);
            var selection = new SelectionController(game);
            var bci = new BciSelectionController(selection, new FakeBciSelector(), new StimulusManager(Enumerable.Range(1, 12), 12))
            {
                Enabled = true
            };
            selection.SelectSquare(Square.Parse("a1"));
            Assert.IsTrue(bci.IsChoosingDestination);

            var squareBySlot = bci.Targets
                .Where(t => t.Payload is ChessTargetPayload p && p.Kind == ChessTargetKind.Destination)
                .ToDictionary(t => t.Stimulus.Value.Index, t => ((ChessTargetPayload)t.Payload).Square);
            Assert.AreEqual(10, squareBySlot.Count);
            return (bci, squareBySlot);
        }

        private static double Distance(Square a, Square b) =>
            System.Math.Sqrt((a.File - b.File) * (a.File - b.File) + (a.Rank - b.Rank) * (a.Rank - b.Rank));

        [Test]
        public void Destinations_WithinTwoSquares_CountAsNeighbours()
        {
            var (bci, squareBySlot) = ChooseRookDestinations();
            var pairs = new HashSet<(int, int)>(bci.NeighbourSlotPairs.Select(p => (System.Math.Min(p.Item1, p.Item2), System.Math.Max(p.Item1, p.Item2))));

            foreach (var a in squareBySlot)
            {
                foreach (var b in squareBySlot.Where(b => b.Key > a.Key))
                {
                    bool close = Distance(a.Value, b.Value) <= 2.9;
                    Assert.AreEqual(close, pairs.Contains((System.Math.Min(a.Key, b.Key), System.Math.Max(a.Key, b.Key))),
                        $"{a.Value}-{b.Value}");
                }
            }
        }

        [Test]
        public void ConsecutiveDestinationFlashes_AreNeverWithinTwoSquares()
        {
            var (bci, squareBySlot) = ChooseRookDestinations();
            var sequencer = new FlashSequencer(100, 300, seed: 21);
            var flashes = new List<int>();
            sequencer.FlashStarted += flashes.Add;
            sequencer.Start(bci.Targets.Select(t => t.Stimulus.Value.Index).ToList(), bci.NeighbourSlotPairs, 2);
            while (sequencer.RoundsCompleted < 100)
                sequencer.Tick(0.005f);

            Assert.AreEqual(0, sequencer.RoundsWithSpacingViolations);
            for (int i = 1; i < flashes.Count; i++)
            {
                if (!squareBySlot.TryGetValue(flashes[i - 1], out var previous) || !squareBySlot.TryGetValue(flashes[i], out var current))
                    continue; // the Cancel tile is off the board
                Assert.Greater(Distance(previous, current), 2.9, $"{previous} then {current} at flash {i}");
            }
        }

        [Test]
        public void SetTiming_ChangesTheDarkPause()
        {
            var sequencer = new FlashSequencer(100, 75, seed: 1);
            sequencer.SetTiming(100, 300);
            sequencer.Start(new[] { 0, 1 });
            sequencer.Tick(0.101f);
            Assert.IsNull(sequencer.LitSlot, "Dark after the flash");
            sequencer.Tick(0.250f);
            Assert.IsNull(sequencer.LitSlot, "Still dark: 300 ms pause");
            sequencer.Tick(0.060f);
            Assert.IsNotNull(sequencer.LitSlot, "Next flash after the pause");
        }
    }

    public class MinimumFlashTests
    {
        private static IReadOnlyList<BciTarget> Assigned(int count)
        {
            new StimulusManager(Enumerable.Range(1, count), count)
                .TryAssign(Enumerable.Range(0, count).Select(i => new BciTarget("t" + i, "t" + i)).ToList(), out var assigned);
            return assigned;
        }

        [Test]
        public void FakeSelector_RejectsSelectionsUntilTargetFlashedEnough()
        {
            var sequencer = new FlashSequencer(100, 100, seed: 2);
            var fake = new FakeBciSelector(minimumFlashes: 3);
            fake.AttachStimulusSource(sequencer);
            var results = new List<BciSelectionResult>();
            fake.SelectionFinished += results.Add;

            int slot2Flashes = 0;
            sequencer.FlashStarted += slot => { if (slot == 2) slot2Flashes++; };

            var targets = Assigned(4);
            fake.StartSelection(targets);
            sequencer.Start(new[] { 0, 1, 2, 3 });
            Assert.AreEqual(3, fake.FlashesRequired);

            while (slot2Flashes < 2)
                sequencer.Tick(0.01f);
            Assert.IsFalse(fake.TrySelectSlot(2), "Slot 2 has flashed only twice");
            Assert.IsTrue(fake.IsSelecting, "Rejected selection keeps the selection running");

            while (slot2Flashes < 3)
                sequencer.Tick(0.01f);
            Assert.IsTrue(fake.TrySelectSlot(2));
            Assert.AreEqual("t2", results.Single().Target.Id);
        }

        [Test]
        public void FlashCountsRestartWithEachSelection()
        {
            var sequencer = new FlashSequencer(100, 100, seed: 2);
            var fake = new FakeBciSelector(minimumFlashes: 2);
            fake.AttachStimulusSource(sequencer);

            fake.StartSelection(Assigned(3));
            sequencer.Start(new[] { 0, 1, 2 });
            while (sequencer.RoundsCompleted < 3)
                sequencer.Tick(0.01f);
            Assert.GreaterOrEqual(fake.FlashesCollected, 2);

            fake.StartSelection(Assigned(3));
            Assert.AreEqual(0, fake.FlashesCollected);
            Assert.IsFalse(fake.TrySelectSlot(0));
        }

        [Test]
        public void ControllerWaitsForMinimumFlashesBeforeActing()
        {
            var game = new ChessGame();
            var selection = new SelectionController(game);
            var sequencer = new FlashSequencer(100, 100, seed: 4);
            var fake = new FakeBciSelector(minimumFlashes: 10);
            fake.AttachStimulusSource(sequencer);
            var bci = new BciSelectionController(selection, fake, new StimulusManager(Enumerable.Range(1, 12), 12))
            {
                Enabled = true
            };
            int g1 = bci.Targets.Single(t => t.Id == "piece:g1").Stimulus.Value.Index;
            int g1Flashes = 0;
            sequencer.FlashStarted += slot => { if (slot == g1) g1Flashes++; };
            sequencer.Start(bci.Targets.Select(t => t.Stimulus.Value.Index).ToList());

            while (g1Flashes < 9)
                sequencer.Tick(0.01f);
            Assert.IsFalse(fake.TrySelectSlot(g1), "Nine flashes are not enough");
            Assert.AreEqual(InteractionState.SelectingPiece, selection.State);

            while (g1Flashes < 10)
                sequencer.Tick(0.01f);
            Assert.IsTrue(fake.TrySelectSlot(g1));
            Assert.AreEqual(Square.Parse("g1"), selection.SelectedPiece);
        }
    }
}
