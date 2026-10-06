using BciChess.Core;
using NUnit.Framework;
using static BciChess.Tests.TestUtil;

namespace BciChess.Tests
{
    public class GameRulesTests
    {
        [Test]
        public void Fen_RoundTrips()
        {
            const string fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 3 17";
            Assert.AreEqual(fen, ChessPosition.FromFen(fen).ToFen());
            Assert.AreEqual(ChessPosition.StartFen, new ChessGame().Position.ToFen());
        }

        [Test]
        public void Check_IsDetected_AndMustBeResolved()
        {
            var game = new ChessGame("4k3/8/8/8/8/8/8/4RK2 b - - 0 1");
            Assert.IsTrue(game.IsInCheck);
            Assert.AreEqual(GameStatus.InProgress, game.Status);
            AssertSquares(game.GetSelectableDestinations(Sq("e8")), "d8", "f8", "d7", "f7");
        }

        [Test]
        public void Checkmate_FoolsMate()
        {
            var game = new ChessGame();
            Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
            Assert.AreEqual(GameStatus.Checkmate, game.Status);
            Assert.IsTrue(game.IsGameOver);
            Assert.IsTrue(game.IsInCheck);
            Assert.AreEqual(PieceColor.Black, game.Winner);
            Assert.AreEqual(0, game.GetLegalMoves().Count);
            Assert.AreEqual(0, game.GetSelectablePieces().Count);
        }

        [Test]
        public void Stalemate_IsDetected()
        {
            var game = new ChessGame("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
            Assert.IsFalse(game.IsInCheck);
            Assert.AreEqual(GameStatus.Stalemate, game.Status);
            Assert.IsNull(game.Winner);
        }

        [Test]
        public void Castling_BothSides_MovesKingAndRook()
        {
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("e1")), "d1", "f1", "d2", "e2", "f2", "g1", "c1");

            Play(game, "e1g1");
            Assert.AreEqual(new Piece(PieceType.King, PieceColor.White), game.Position[Sq("g1")]);
            Assert.AreEqual(new Piece(PieceType.Rook, PieceColor.White), game.Position[Sq("f1")]);
            Assert.IsTrue(game.Position[Sq("h1")].IsNone);
            Assert.IsTrue(game.LastMove.Value.IsCastling);

            Play(game, "e8c8");
            Assert.AreEqual(new Piece(PieceType.King, PieceColor.Black), game.Position[Sq("c8")]);
            Assert.AreEqual(new Piece(PieceType.Rook, PieceColor.Black), game.Position[Sq("d8")]);
            Assert.AreEqual(CastlingRights.None, game.Position.CastlingRights);
        }

        [Test]
        public void Castling_NotThroughAttackedSquare()
        {
            // Black rook on f8 attacks f1: king side is illegal, queen side is fine.
            var game = new ChessGame("4kr2/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            var destinations = game.GetSelectableDestinations(Sq("e1"));
            CollectionAssert.DoesNotContain(destinations, Sq("g1"));
            CollectionAssert.Contains(destinations, Sq("c1"));
        }

        [Test]
        public void Castling_NotOutOfCheck()
        {
            var game = new ChessGame("4r1k1/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            var destinations = game.GetSelectableDestinations(Sq("e1"));
            CollectionAssert.DoesNotContain(destinations, Sq("g1"));
            CollectionAssert.DoesNotContain(destinations, Sq("c1"));
        }

        [Test]
        public void Castling_RightLostAfterRookMoves()
        {
            var game = new ChessGame("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Play(game, "h1h2", "e8d8", "h2h1", "d8e8");
            var destinations = game.GetSelectableDestinations(Sq("e1"));
            CollectionAssert.DoesNotContain(destinations, Sq("g1"));
            CollectionAssert.Contains(destinations, Sq("c1"));
        }

        [Test]
        public void EnPassant_CapturesPassedPawn()
        {
            var game = new ChessGame();
            Play(game, "e2e4", "a7a6", "e4e5", "d7d5");
            CollectionAssert.Contains(game.GetSelectableDestinations(Sq("e5")), Sq("d6"));

            Play(game, "e5d6");
            Assert.IsTrue(game.LastMove.Value.IsEnPassant);
            Assert.IsTrue(game.Position[Sq("d5")].IsNone, "Captured pawn must be removed");
            Assert.AreEqual(new Piece(PieceType.Pawn, PieceColor.White), game.Position[Sq("d6")]);
        }

        [Test]
        public void EnPassant_OnlyImmediatelyAfterDoublePush()
        {
            var game = new ChessGame();
            Play(game, "e2e4", "a7a6", "e4e5", "d7d5", "h2h3", "h7h6");
            CollectionAssert.DoesNotContain(game.GetSelectableDestinations(Sq("e5")), Sq("d6"));
        }

        [Test]
        public void Promotion_RequiresChoice_AndPlacesChosenPiece()
        {
            var game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("a7")), "a8");
            Assert.AreEqual(4, game.GetLegalMoves(Sq("a7")).Count);
            Assert.IsTrue(game.RequiresPromotion(Sq("a7"), Sq("a8")));

            Assert.IsFalse(game.TryMakeMove(Sq("a7"), Sq("a8")), "Promotion piece must be specified");
            Assert.IsTrue(game.TryMakeMove(Sq("a7"), Sq("a8"), PieceType.Knight));
            Assert.AreEqual(new Piece(PieceType.Knight, PieceColor.White), game.Position[Sq("a8")]);
        }

        [Test]
        public void Promotion_ByCapture()
        {
            var game = new ChessGame("1r5k/P7/8/8/8/8/8/K7 w - - 0 1");
            AssertSquares(game.GetSelectableDestinations(Sq("a7")), "a8", "b8");
            Play(game, "a7b8q");
            Assert.AreEqual(new Piece(PieceType.Queen, PieceColor.White), game.Position[Sq("b8")]);
        }

        [Test]
        public void TurnsAlternate()
        {
            var game = new ChessGame();
            Assert.AreEqual(PieceColor.White, game.SideToMove);
            Assert.IsFalse(game.TryMakeMove(Sq("e7"), Sq("e5")), "Black cannot move on White's turn");
            Play(game, "e2e4");
            Assert.AreEqual(PieceColor.Black, game.SideToMove);
        }

        [Test]
        public void Undo_RestoresPreviousPosition()
        {
            var game = new ChessGame();
            Play(game, "e2e4", "e7e5");
            Assert.IsTrue(game.Undo());
            Assert.IsTrue(game.Undo());
            Assert.IsFalse(game.Undo());
            Assert.AreEqual(ChessPosition.StartFen, game.Position.ToFen());
            Assert.AreEqual(0, game.MoveHistory.Count);
        }

        [Test]
        public void Undo_AfterCheckmate_ResumesGame()
        {
            var game = new ChessGame();
            Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
            game.Undo();
            Assert.AreEqual(GameStatus.InProgress, game.Status);
        }

        [Test]
        public void Draw_InsufficientMaterial()
        {
            Assert.AreEqual(GameStatus.DrawInsufficientMaterial, new ChessGame("8/8/8/4k3/8/8/8/4K3 w - - 0 1").Status);
            Assert.AreEqual(GameStatus.DrawInsufficientMaterial, new ChessGame("8/8/8/4k3/8/8/8/3BK3 w - - 0 1").Status);
            Assert.AreEqual(GameStatus.InProgress, new ChessGame("8/8/8/4k3/8/8/8/3RK3 w - - 0 1").Status);
            Assert.AreEqual(GameStatus.InProgress, new ChessGame("8/8/8/4k3/8/8/8/2NNK3 w - - 0 1").Status);
        }

        [Test]
        public void Draw_ThreefoldRepetition()
        {
            var game = new ChessGame();
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
            Assert.AreEqual(GameStatus.InProgress, game.Status);
            Play(game, "f6g8");
            Assert.AreEqual(GameStatus.DrawThreefoldRepetition, game.Status);
        }

        [Test]
        public void Draw_FiftyMoveRule()
        {
            var game = new ChessGame("4k3/8/8/8/8/8/8/R3K3 w - - 99 80");
            Assert.AreEqual(GameStatus.InProgress, game.Status);
            Play(game, "a1a2");
            Assert.AreEqual(GameStatus.DrawFiftyMoveRule, game.Status);
        }

        [Test]
        public void MoveMade_And_PositionChanged_AreRaised()
        {
            var game = new ChessGame();
            int moveEvents = 0, positionEvents = 0;
            game.MoveMade += _ => moveEvents++;
            game.PositionChanged += () => positionEvents++;

            Play(game, "e2e4");
            game.Undo();
            game.Reset();

            Assert.AreEqual(1, moveEvents);
            Assert.AreEqual(3, positionEvents);
        }

        [Test]
        public void Notation_LongAlgebraic()
        {
            var game = new ChessGame();
            Play(game, "g1f3");
            Assert.AreEqual("Ng1-f3", MoveNotation.ToLongAlgebraic(game.LastMove.Value));
            Assert.AreEqual("g1f3", game.LastMove.Value.ToUci());
        }
    }
}
