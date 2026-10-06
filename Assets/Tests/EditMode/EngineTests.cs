using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Engine;
using BciChess.Interaction;
using NUnit.Framework;
using static BciChess.Tests.TestUtil;

namespace BciChess.Tests
{
    /// <summary>Engine whose answers are scripted by the test.</summary>
    internal sealed class ScriptedEngine : IChessEngine
    {
        public Func<EnginePosition, Task<string>> Answer = _ => Task.FromResult<string>(null);
        public int Calls;
        public EnginePosition LastPosition;

        public string Name => "Scripted";

        public Task<string> GetBestMoveAsync(EnginePosition position, CancellationToken cancellationToken)
        {
            Calls++;
            LastPosition = position;
            return Answer(position);
        }

        public void Dispose()
        {
        }
    }

    public class UciProtocolTests
    {
        [Test]
        public void PositionCommand_IncludesStartFenAndMoves()
        {
            var game = new ChessGame();
            Assert.AreEqual("position fen " + ChessPosition.StartFen, UciProtocol.PositionCommand(EnginePosition.From(game)));

            Play(game, "e2e4", "e7e5");
            Assert.AreEqual("position fen " + ChessPosition.StartFen + " moves e2e4 e7e5",
                UciProtocol.PositionCommand(EnginePosition.From(game)));
        }

        [Test]
        public void GoCommand_UsesDepthOrMoveTime()
        {
            Assert.AreEqual("go movetime 750", UciProtocol.GoCommand(750, 0));
            Assert.AreEqual("go depth 8", UciProtocol.GoCommand(750, 8));
        }

        [TestCase("bestmove e2e4", "e2e4")]
        [TestCase("bestmove e7e8q ponder a2a3", "e7e8q")]
        [TestCase("bestmove (none)", null)]
        [TestCase("bestmove 0000", null)]
        public void ParsesBestMove(string line, string expected)
        {
            Assert.IsTrue(UciProtocol.TryParseBestMove(line, out string move));
            Assert.AreEqual(expected, move);
        }

        [TestCase("info depth 10 score cp 20")]
        [TestCase("bestmoves e2e4")]
        [TestCase("bestmove")]
        [TestCase("bestmove e2")]
        public void RejectsNonBestMoveLines(string line)
        {
            Assert.IsFalse(UciProtocol.TryParseBestMove(line, out _));
        }

        [Test]
        public void UciMove_IsValidatedAgainstLegalMoves()
        {
            var game = new ChessGame();
            Assert.IsTrue(game.TryGetUciMove("g1f3", out var move));
            Assert.AreEqual(Sq("g1"), move.From);
            Assert.IsFalse(game.TryGetUciMove("e2e5", out _), "Illegal");
            Assert.IsFalse(game.TryGetUciMove("e7e5", out _), "Wrong side");
            Assert.IsFalse(game.TryGetUciMove("zz99", out _), "Malformed");

            game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            Assert.IsTrue(game.TryGetUciMove("a7a8n", out var promotion));
            Assert.AreEqual(PieceType.Knight, promotion.Promotion);
            Assert.IsFalse(game.TryGetUciMove("a7a8", out _), "Promotion piece required");
        }
    }

    public class ComputerPlayerTests
    {
        private SynchronizationContext _previousContext;

        // Without a synchronization context, completing an awaited task resumes the computer player inline,
        // which keeps these tests synchronous (Unity's test runner would otherwise defer to the next frame).
        [SetUp]
        public void ClearSynchronizationContext()
        {
            _previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void RestoreSynchronizationContext()
        {
            SynchronizationContext.SetSynchronizationContext(_previousContext);
        }

        [Test]
        public void RepliesOnItsTurn_AndNotOnTheHumansTurn()
        {
            var game = new ChessGame();
            var engine = new ScriptedEngine { Answer = _ => Task.FromResult("e7e5") };
            var computer = new ComputerPlayer(game, engine, PieceColor.Black) { Enabled = true };

            Assert.AreEqual(0, engine.Calls, "White (human) to move");
            Play(game, "e2e4");

            Assert.AreEqual(1, engine.Calls);
            Assert.AreEqual(1, engine.LastPosition.Moves.Count);
            Assert.AreEqual("e7e5", game.LastMove.Value.ToUci());
            Assert.IsFalse(computer.IsThinking);
        }

        [Test]
        public void PlaysFirst_WhenItHasWhite()
        {
            var game = new ChessGame();
            var engine = new ScriptedEngine { Answer = _ => Task.FromResult("d2d4") };
            new ComputerPlayer(game, engine, PieceColor.White) { Enabled = true };
            Assert.AreEqual("d2d4", game.LastMove.Value.ToUci());
        }

        [Test]
        public void ThinksUntilTheEngineAnswers()
        {
            var game = new ChessGame();
            var answer = new TaskCompletionSource<string>();
            var engine = new ScriptedEngine { Answer = _ => answer.Task };
            var computer = new ComputerPlayer(game, engine, PieceColor.Black) { Enabled = true };

            Play(game, "e2e4");
            Assert.IsTrue(computer.IsThinking);
            Assert.AreEqual(1, game.MoveHistory.Count);

            answer.SetResult("c7c5");
            Assert.IsFalse(computer.IsThinking);
            Assert.AreEqual("c7c5", game.LastMove.Value.ToUci());
        }

        [Test]
        public void StaleAnswer_AfterUndo_IsDiscarded()
        {
            var game = new ChessGame();
            var answer = new TaskCompletionSource<string>();
            var engine = new ScriptedEngine { Answer = _ => answer.Task };
            var computer = new ComputerPlayer(game, engine, PieceColor.Black) { Enabled = true };

            Play(game, "e2e4");
            game.Undo();
            Assert.IsFalse(computer.IsThinking);

            answer.SetResult("e7e5");
            Assert.AreEqual(0, game.MoveHistory.Count);
        }

        [Test]
        public void EngineFailure_FallsBackToSecondEngine()
        {
            var game = new ChessGame();
            var broken = new ScriptedEngine
            {
                Answer = _ => Task.FromException<string>(new EngineException("not found"))
            };
            var fallback = new ScriptedEngine { Answer = _ => Task.FromResult("g8f6") };
            var computer = new ComputerPlayer(game, broken, PieceColor.Black, fallback) { Enabled = true };

            Play(game, "e2e4");
            Assert.AreEqual("g8f6", game.LastMove.Value.ToUci());
            Assert.AreEqual("Scripted", computer.EngineName);
            StringAssert.Contains("not found", computer.Message);

            Play(game, "d2d4");
            Assert.AreEqual(1, broken.Calls, "Failed engine is not retried");
            Assert.AreEqual(2, fallback.Calls);
        }

        [Test]
        public void EngineFailure_WithoutFallback_ReportsAndStops()
        {
            var game = new ChessGame();
            var broken = new ScriptedEngine
            {
                Answer = _ => Task.FromException<string>(new EngineException("crashed"))
            };
            var computer = new ComputerPlayer(game, broken, PieceColor.Black) { Enabled = true };

            Play(game, "e2e4");
            Assert.IsFalse(computer.IsThinking);
            StringAssert.Contains("crashed", computer.Message);
            Assert.AreEqual(1, game.MoveHistory.Count);
        }

        [Test]
        public void IllegalEngineMove_IsRejected()
        {
            var game = new ChessGame();
            var engine = new ScriptedEngine { Answer = _ => Task.FromResult("e7e4") };
            var computer = new ComputerPlayer(game, engine, PieceColor.Black) { Enabled = true };

            Play(game, "e2e4");
            Assert.AreEqual(1, game.MoveHistory.Count);
            StringAssert.Contains("e7e4", computer.Message);
        }

        [Test]
        public void SelectionController_BlocksInputOnComputerTurn()
        {
            var game = new ChessGame();
            var answer = new TaskCompletionSource<string>();
            var selection = new SelectionController(game) { ComputerSide = PieceColor.Black };
            new ComputerPlayer(game, new ScriptedEngine { Answer = _ => answer.Task }, PieceColor.Black) { Enabled = true };

            Play(game, "e2e4");
            Assert.AreEqual(InteractionState.ComputerThinking, selection.State);
            Assert.IsFalse(selection.SelectSquare(Sq("e7")));
            Assert.AreEqual(0, selection.SelectablePieces.Count);

            answer.SetResult("e7e5");
            Assert.AreEqual(InteractionState.SelectingPiece, selection.State);
        }

        [Test]
        public void BciIsIdleDuringComputerTurn()
        {
            var game = new ChessGame();
            var selection = new SelectionController(game) { ComputerSide = PieceColor.Black };
            var fake = new FakeBciSelector();
            var bci = new BciSelectionController(selection, fake, new StimulusManager(new[] { 1, 2, 3, 4, 5, 6 }, 6))
            {
                Enabled = true
            };

            Play(game, "e2e4");
            Assert.AreEqual(BciSessionStatus.Idle, bci.Status);
            Assert.IsFalse(fake.IsSelecting);
        }
    }

    public class SimpleEngineTests
    {
        [Test]
        public void PrefersTheMostValuableCapture()
        {
            var game = new ChessGame("4k3/8/8/3r4/8/3Q1p2/8/4K3 w - - 0 1");
            string move = new SimpleEngine(seed: 1).GetBestMoveAsync(EnginePosition.From(game), CancellationToken.None).Result;
            Assert.AreEqual("d3d5", move);
        }

        [Test]
        public void ReturnsLegalMoves()
        {
            var game = new ChessGame();
            var engine = new SimpleEngine(seed: 5);
            for (int i = 0; i < 40 && !game.IsGameOver; i++)
            {
                string move = engine.GetBestMoveAsync(EnginePosition.From(game), CancellationToken.None).Result;
                Assert.IsTrue(game.TryGetUciMove(move, out var legal), $"Illegal move {move}");
                game.TryMakeMove(legal);
            }
        }

        [Test]
        public void ReturnsNull_WhenThereIsNoMove()
        {
            var game = new ChessGame("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1"); // stalemate
            Assert.IsNull(new SimpleEngine().GetBestMoveAsync(EnginePosition.From(game), CancellationToken.None).Result);
        }
    }
}
