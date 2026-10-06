using BciChess.Core;
using NUnit.Framework;
using static BciChess.Tests.TestUtil;

namespace BciChess.Tests
{
    public class MoveGenerationTests
    {
        // Reference node counts from https://www.chessprogramming.org/Perft_Results
        [TestCase(ChessPosition.StartFen, 1, 20)]
        [TestCase(ChessPosition.StartFen, 2, 400)]
        [TestCase(ChessPosition.StartFen, 3, 8902)]
        [TestCase(ChessPosition.StartFen, 4, 197281)]
        [TestCase("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48)]
        [TestCase("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039)]
        [TestCase("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 3, 97862)]
        [TestCase("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 4, 43238)]
        [TestCase("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 3, 9467)]
        [TestCase("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 3, 62379)]
        public void Perft_MatchesReferenceCounts(string fen, int depth, long expected)
        {
            Assert.AreEqual(expected, MoveGenerator.Perft(ChessPosition.FromFen(fen), depth));
        }

        [Test]
        public void Pawn_FromStart_CanMoveOneOrTwo()
        {
            var game = new ChessGame();
            AssertSquares(game.GetSelectableDestinations(Sq("e2")), "e3", "e4");
        }

        [Test]
        public void Pawn_Blocked_CannotMove()
        {
            var game = new ChessGame("4k3/8/8/8/8/4p3/4P3/4K3 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("e2")));
        }

        [Test]
        public void Pawn_CapturesDiagonallyOnly()
        {
            var game = new ChessGame("4k3/8/8/8/3p1p2/4p3/4P3/4K3 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("e2")));
            game = new ChessGame("4k3/8/8/8/8/3p1p2/4P3/4K3 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("e2")), "e3", "e4", "d3", "f3");
        }

        [Test]
        public void Knight_FromStart()
        {
            var game = new ChessGame();
            AssertSquares(game.GetSelectableDestinations(Sq("g1")), "f3", "h3");
        }

        // Lone-piece positions below include a spare pawn so they are not an insufficient-material draw.
        [Test]
        public void Knight_InCentre_HasEightMoves()
        {
            var game = new ChessGame("7k/8/8/8/3N4/8/P7/K7 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("d4")),
                "c2", "e2", "b3", "f3", "b5", "f5", "c6", "e6");
        }

        [Test]
        public void Bishop_SlidesDiagonallyUntilBlocked()
        {
            var game = new ChessGame("8/8/8/8/3B4/8/P7/K6k w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("d4")),
                "c3", "b2", "e5", "f6", "g7", "h8", "c5", "b6", "a7", "e3", "f2", "g1");
        }

        [Test]
        public void Rook_SlidesOrthogonally()
        {
            var game = new ChessGame("8/8/8/8/3R4/8/8/K6k w - - 0 1");
            Assert.AreEqual(14, game.GetSelectableDestinations(Sq("d4")).Count);
        }

        [Test]
        public void Queen_CombinesRookAndBishop()
        {
            var game = new ChessGame("8/8/8/8/3Q4/8/8/K6k w - - 0 1");
            Assert.AreEqual(26, game.GetSelectableDestinations(Sq("d4")).Count);
        }

        [Test]
        public void King_MovesOneSquare_AndAvoidsAttackedSquares()
        {
            var game = new ChessGame("k7/8/8/8/3K4/7P/8/8 w - - 0 1");
            Assert.AreEqual(8, game.GetSelectableDestinations(Sq("d4")).Count);

            // Black rook on e8 controls the e-file.
            game = new ChessGame("k3r3/8/8/8/3K4/8/8/8 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("d4")), "c3", "c4", "c5", "d3", "d5");
        }

        [Test]
        public void Rook_CanCaptureButNotJumpOver()
        {
            var game = new ChessGame("4k3/8/8/3p4/8/8/8/3RK3 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("d1")), "d2", "d3", "d4", "d5", "c1", "b1", "a1");

            Assert.IsTrue(game.TryGetMove(Sq("d1"), Sq("d5"), PieceType.None, out var capture));
            Assert.IsTrue(capture.IsCapture);
            Assert.AreEqual(new Piece(PieceType.Pawn, PieceColor.Black), capture.Captured);

            Assert.IsTrue(game.TryMakeMove(Sq("d1"), Sq("d5")));
            Assert.AreEqual(new Piece(PieceType.Rook, PieceColor.White), game.Position[Sq("d5")]);
        }

        [Test]
        public void CannotCaptureOwnPiece()
        {
            var game = new ChessGame();
            Assert.IsFalse(game.TryMakeMove(Sq("a1"), Sq("a2")));
        }

        [Test]
        public void PinnedPiece_CannotExposeKing()
        {
            // White knight on e2 is pinned by the rook on e8.
            var game = new ChessGame("4r1k1/8/8/8/8/8/4N3/4K3 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("e2")));
        }
    }
}
