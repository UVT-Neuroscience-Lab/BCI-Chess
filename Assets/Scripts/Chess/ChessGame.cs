using System;
using System.Collections.Generic;

namespace BciChess.Core
{
    public enum GameStatus
    {
        InProgress,
        Checkmate,
        Stalemate,
        DrawFiftyMoveRule,
        DrawInsufficientMaterial,
        DrawThreefoldRepetition
    }

    /// <summary>
    /// A chess game: current position, move history, legal moves and game-over detection.
    /// This is the only entry point other systems (input, BCI, engine) should use to change the game.
    /// </summary>
    public sealed class ChessGame
    {
        private static readonly IReadOnlyList<ChessMove> NoMoves = Array.Empty<ChessMove>();
        private static readonly IReadOnlyList<Square> NoSquares = Array.Empty<Square>();

        // _positions[0] is the starting position; the last entry is the current position.
        private readonly List<ChessPosition> _positions = new List<ChessPosition>();
        private readonly List<ChessMove> _moves = new List<ChessMove>();
        private List<ChessMove> _legalMoves = new List<ChessMove>();
        private List<Square> _selectablePieces = new List<Square>();

        public ChessGame() : this(ChessPosition.StartFen)
        {
        }

        public ChessGame(string fen)
        {
            Reset(fen);
        }

        /// <summary>Raised after a move has been played.</summary>
        public event Action<ChessMove> MoveMade;

        /// <summary>Raised whenever the current position changes (move, undo or reset).</summary>
        public event Action PositionChanged;

        public ChessPosition Position => _positions[_positions.Count - 1];

        /// <summary>The position the game started from (before <see cref="MoveHistory"/>).</summary>
        public ChessPosition StartPosition => _positions[0];
        public PieceColor SideToMove => Position.SideToMove;
        public IReadOnlyList<ChessMove> MoveHistory => _moves;
        public ChessMove? LastMove => _moves.Count > 0 ? _moves[_moves.Count - 1] : (ChessMove?)null;

        public GameStatus Status { get; private set; }
        public bool IsGameOver => Status != GameStatus.InProgress;

        /// <summary>True if the side to move is in check.</summary>
        public bool IsInCheck { get; private set; }

        /// <summary>The winner after checkmate; null otherwise.</summary>
        public PieceColor? Winner => Status == GameStatus.Checkmate ? SideToMove.Opponent() : (PieceColor?)null;

        /// <summary>All legal moves for the side to move. Empty once the game is over.</summary>
        public IReadOnlyList<ChessMove> GetLegalMoves() => IsGameOver ? NoMoves : _legalMoves;

        /// <summary>Legal moves of the piece on <paramref name="from"/>.</summary>
        public IReadOnlyList<ChessMove> GetLegalMoves(Square from)
        {
            if (IsGameOver)
                return NoMoves;
            return _legalMoves.FindAll(m => m.From == from);
        }

        /// <summary>Squares holding a piece of the side to move that has at least one legal move.</summary>
        public IReadOnlyList<Square> GetSelectablePieces() => IsGameOver ? NoSquares : _selectablePieces;

        /// <summary>
        /// Distinct legal destinations of the piece on <paramref name="from"/>.
        /// The four promotion moves to one square count as one destination.
        /// </summary>
        public IReadOnlyList<Square> GetSelectableDestinations(Square from)
        {
            var result = new List<Square>();
            foreach (var move in GetLegalMoves())
            {
                if (move.From == from && !result.Contains(move.To))
                    result.Add(move.To);
            }
            return result;
        }

        /// <summary>True if moving from -> to is legal and requires choosing a promotion piece.</summary>
        public bool RequiresPromotion(Square from, Square to)
        {
            foreach (var move in GetLegalMoves())
            {
                if (move.From == from && move.To == to)
                    return move.IsPromotion;
            }
            return false;
        }

        public bool TryGetMove(Square from, Square to, PieceType promotion, out ChessMove move)
        {
            foreach (var candidate in GetLegalMoves())
            {
                if (candidate.From == from && candidate.To == to && candidate.Promotion == promotion)
                {
                    move = candidate;
                    return true;
                }
            }
            move = default;
            return false;
        }

        /// <summary>Finds the legal move written in UCI notation ("e2e4", "e7e8q"). False if malformed or illegal.</summary>
        public bool TryGetUciMove(string uci, out ChessMove move)
        {
            move = default;
            if (uci == null || (uci.Length != 4 && uci.Length != 5))
                return false;
            if (!Square.TryParse(uci.Substring(0, 2), out var from) || !Square.TryParse(uci.Substring(2, 2), out var to))
                return false;

            var promotion = PieceType.None;
            if (uci.Length == 5)
            {
                if (!Piece.TryFromFenChar(uci[4], out var piece))
                    return false;
                promotion = piece.Type;
            }
            return TryGetMove(from, to, promotion, out move);
        }

        public bool TryMakeMove(ChessMove move) => TryMakeMove(move.From, move.To, move.Promotion);

        /// <summary>Plays the move if it is legal. Promotion moves must name the promotion piece.</summary>
        public bool TryMakeMove(Square from, Square to, PieceType promotion = PieceType.None)
        {
            if (!TryGetMove(from, to, promotion, out var move))
                return false;

            _positions.Add(Position.Apply(move));
            _moves.Add(move);
            Recalculate();

            MoveMade?.Invoke(move);
            PositionChanged?.Invoke();
            return true;
        }

        /// <summary>Takes back the last move. Returns false if there is nothing to undo.</summary>
        public bool Undo()
        {
            if (_moves.Count == 0)
                return false;

            _moves.RemoveAt(_moves.Count - 1);
            _positions.RemoveAt(_positions.Count - 1);
            Recalculate();
            PositionChanged?.Invoke();
            return true;
        }

        public void Reset(string fen = ChessPosition.StartFen)
        {
            var start = ChessPosition.FromFen(fen);
            _positions.Clear();
            _moves.Clear();
            _positions.Add(start);
            Recalculate();
            PositionChanged?.Invoke();
        }

        private void Recalculate()
        {
            var position = Position;
            _legalMoves = MoveGenerator.GenerateLegalMoves(position);
            IsInCheck = MoveGenerator.IsInCheck(position);

            _selectablePieces = new List<Square>();
            foreach (var move in _legalMoves)
            {
                if (!_selectablePieces.Contains(move.From))
                    _selectablePieces.Add(move.From);
            }

            if (_legalMoves.Count == 0)
                Status = IsInCheck ? GameStatus.Checkmate : GameStatus.Stalemate;
            else if (position.HalfmoveClock >= 100)
                Status = GameStatus.DrawFiftyMoveRule;
            else if (HasInsufficientMaterial(position))
                Status = GameStatus.DrawInsufficientMaterial;
            else if (CountRepetitions(position.RepetitionKey) >= 3)
                Status = GameStatus.DrawThreefoldRepetition;
            else
                Status = GameStatus.InProgress;
        }

        private int CountRepetitions(string key)
        {
            int count = 0;
            foreach (var position in _positions)
            {
                if (position.RepetitionKey == key)
                    count++;
            }
            return count;
        }

        /// <summary>K vs K, K+minor vs K, or only bishops that all stand on one square colour.</summary>
        private static bool HasInsufficientMaterial(ChessPosition position)
        {
            int minors = 0;
            int knights = 0;
            bool? bishopSquaresLight = null;
            bool mixedBishopColours = false;

            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                var piece = position[square];
                switch (piece.Type)
                {
                    case PieceType.Pawn:
                    case PieceType.Rook:
                    case PieceType.Queen:
                        return false;
                    case PieceType.Knight:
                        minors++;
                        knights++;
                        break;
                    case PieceType.Bishop:
                        minors++;
                        if (bishopSquaresLight == null)
                            bishopSquaresLight = square.IsLight;
                        else if (bishopSquaresLight != square.IsLight)
                            mixedBishopColours = true;
                        break;
                }
            }

            if (minors <= 1)
                return true;
            return knights == 0 && !mixedBishopColours;
        }
    }
}
