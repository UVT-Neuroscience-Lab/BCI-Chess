using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BciChess.Core;

namespace BciChess.Engine
{
    /// <summary>
    /// Built-in fallback opponent used when Stockfish is unavailable, so a demo never stalls.
    /// It is deliberately weak: it prefers the most valuable capture (and promotions), otherwise plays a random move.
    /// </summary>
    public sealed class SimpleEngine : IChessEngine
    {
        private readonly Random _random;

        public SimpleEngine(int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public string Name => "Built-in (simple)";

        public Task<string> GetBestMoveAsync(EnginePosition position, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var moves = MoveGenerator.GenerateLegalMoves(position.Current);
            if (moves.Count == 0)
                return Task.FromResult<string>(null);

            int bestScore = int.MinValue;
            var best = new List<ChessMove>();
            foreach (var move in moves)
            {
                int score = Score(move);
                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                }
                if (score == bestScore)
                    best.Add(move);
            }
            return Task.FromResult(best[_random.Next(best.Count)].ToUci());
        }

        public void Dispose()
        {
        }

        // Most valuable victim, least valuable attacker; promotions count as winning the new piece.
        private static int Score(ChessMove move)
        {
            int score = 0;
            if (move.IsCapture)
                score += 10 * Value(move.Captured.Type) - Value(move.Piece.Type);
            if (move.IsPromotion)
                score += 10 * Value(move.Promotion);
            return score;
        }

        private static int Value(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return 1;
                case PieceType.Knight:
                case PieceType.Bishop: return 3;
                case PieceType.Rook: return 5;
                case PieceType.Queen: return 9;
                case PieceType.King: return 100;
                default: return 0;
            }
        }
    }
}
