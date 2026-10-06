using BciChess.Core;
using BciChess.Interaction;
using NUnit.Framework;
using static BciChess.Tests.TestUtil;

namespace BciChess.Tests
{
    public class SelectionControllerTests
    {
        [Test]
        public void StartsBySelectingPiece_WithOnlyMovablePieces()
        {
            var controller = new SelectionController(new ChessGame());
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
            AssertSquares(controller.SelectablePieces,
                "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2", "b1", "g1");
        }

        [Test]
        public void PieceThenDestination_PlaysMove()
        {
            var game = new ChessGame();
            var controller = new SelectionController(game);

            Assert.IsTrue(controller.SelectSquare(Sq("g1")));
            Assert.AreEqual(InteractionState.SelectingDestination, controller.State);
            AssertSquares(controller.SelectableDestinations, "f3", "h3");

            Assert.IsTrue(controller.SelectSquare(Sq("f3")));
            Assert.AreEqual(1, game.MoveHistory.Count);
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
            Assert.IsNull(controller.SelectedPiece);
            Assert.AreEqual(PieceColor.Black, game.SideToMove);
        }

        [Test]
        public void InvalidSelections_AreRejected()
        {
            var game = new ChessGame();
            var controller = new SelectionController(game);

            Assert.IsFalse(controller.SelectSquare(Sq("e4")), "Empty square");
            Assert.IsFalse(controller.SelectSquare(Sq("e7")), "Opponent piece");
            Assert.IsFalse(controller.SelectSquare(Sq("a1")), "Piece without legal moves");

            controller.SelectSquare(Sq("e2"));
            Assert.IsFalse(controller.SelectSquare(Sq("e5")), "Illegal destination");
            Assert.AreEqual(Sq("e2"), controller.SelectedPiece);
            Assert.AreEqual(0, game.MoveHistory.Count);
        }

        [Test]
        public void SelectingAnotherOwnPiece_SwitchesSelection()
        {
            var controller = new SelectionController(new ChessGame());
            controller.SelectSquare(Sq("e2"));
            Assert.IsTrue(controller.SelectSquare(Sq("g1")));
            Assert.AreEqual(Sq("g1"), controller.SelectedPiece);
        }

        [Test]
        public void SelectingSamePieceAgain_Deselects()
        {
            var controller = new SelectionController(new ChessGame());
            controller.SelectSquare(Sq("e2"));
            Assert.IsTrue(controller.SelectSquare(Sq("e2")));
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
            Assert.IsNull(controller.SelectedPiece);
        }

        [Test]
        public void Promotion_WaitsForChoice_ThenPlays()
        {
            var game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            var controller = new SelectionController(game);

            controller.SelectSquare(Sq("a7"));
            Assert.IsTrue(controller.SelectSquare(Sq("a8")));
            Assert.AreEqual(InteractionState.SelectingPromotion, controller.State);
            Assert.AreEqual(Sq("a8"), controller.PromotionTarget);
            Assert.IsFalse(controller.SelectSquare(Sq("a1")), "Squares are ignored while choosing promotion");

            Assert.IsTrue(controller.SelectPromotion(PieceType.Rook));
            Assert.AreEqual(new Piece(PieceType.Rook, PieceColor.White), game.Position[Sq("a8")]);
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
        }

        [Test]
        public void Cancel_StepsBackOneLevel()
        {
            var controller = new SelectionController(new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1"));
            controller.SelectSquare(Sq("a7"));
            controller.SelectSquare(Sq("a8"));

            controller.Cancel();
            Assert.AreEqual(InteractionState.SelectingDestination, controller.State);
            Assert.AreEqual(Sq("a7"), controller.SelectedPiece);

            controller.Cancel();
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
            Assert.IsNull(controller.SelectedPiece);
        }

        [Test]
        public void GameOver_BlocksSelection_AndUndoResumes()
        {
            var game = new ChessGame();
            var controller = new SelectionController(game);
            Play(game, "f2f3", "e7e5", "g2g4", "d8h4");

            Assert.AreEqual(InteractionState.GameOver, controller.State);
            Assert.IsFalse(controller.SelectSquare(Sq("e1")));

            game.Undo();
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
        }

        [Test]
        public void ExternalPositionChange_ClearsSelection()
        {
            var game = new ChessGame();
            var controller = new SelectionController(game);
            controller.SelectSquare(Sq("e2"));
            game.Reset();
            Assert.IsNull(controller.SelectedPiece);
            Assert.AreEqual(InteractionState.SelectingPiece, controller.State);
        }
    }
}
