using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BciChess.Core;

namespace BciChess.Engine
{
    /// <summary>A computer opponent that suggests a move for a position.</summary>
    public interface IChessEngine : IDisposable
    {
        string Name { get; }

        /// <summary>
        /// Returns the engine's move in UCI notation (e.g. "e2e4"), or null if the side to move has no move.
        /// Throws <see cref="EngineException"/> if the engine fails; honours <paramref name="cancellationToken"/>.
        /// </summary>
        Task<string> GetBestMoveAsync(EnginePosition position, CancellationToken cancellationToken);
    }

    public sealed class EngineException : Exception
    {
        public EngineException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// A position as sent to an engine: the start position plus the moves played, so the engine also
    /// knows the history (repetitions), and the resulting current position.
    /// </summary>
    public sealed class EnginePosition
    {
        public EnginePosition(ChessPosition start, IReadOnlyList<ChessMove> moves, ChessPosition current)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            Moves = moves ?? throw new ArgumentNullException(nameof(moves));
            Current = current ?? throw new ArgumentNullException(nameof(current));
        }

        public ChessPosition Start { get; }
        public IReadOnlyList<ChessMove> Moves { get; }
        public ChessPosition Current { get; }

        /// <summary>Snapshot of the game's current state (copies the move list).</summary>
        public static EnginePosition From(ChessGame game)
        {
            var moves = new List<ChessMove>(game.MoveHistory);
            return new EnginePosition(game.StartPosition, moves, game.Position);
        }
    }
}
