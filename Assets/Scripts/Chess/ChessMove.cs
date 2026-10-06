using System;

namespace BciChess.Core
{
    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Capture = 1,
        DoublePawnPush = 2,
        EnPassant = 4,
        CastleKingSide = 8,
        CastleQueenSide = 16
    }

    /// <summary>
    /// A move in a specific position. Two moves are equal when From, To and Promotion match,
    /// which uniquely identifies a move within one position.
    /// </summary>
    public readonly struct ChessMove : IEquatable<ChessMove>
    {
        public readonly Square From;
        public readonly Square To;
        public readonly Piece Piece;
        public readonly Piece Captured;
        public readonly PieceType Promotion;
        public readonly MoveFlags Flags;

        public ChessMove(Square from, Square to, Piece piece, Piece captured = default,
            PieceType promotion = PieceType.None, MoveFlags flags = MoveFlags.None)
        {
            From = from;
            To = to;
            Piece = piece;
            Captured = captured;
            Promotion = promotion;
            Flags = flags;
        }

        public bool IsCapture => (Flags & MoveFlags.Capture) != 0;
        public bool IsEnPassant => (Flags & MoveFlags.EnPassant) != 0;
        public bool IsDoublePawnPush => (Flags & MoveFlags.DoublePawnPush) != 0;
        public bool IsCastling => (Flags & (MoveFlags.CastleKingSide | MoveFlags.CastleQueenSide)) != 0;
        public bool IsPromotion => Promotion != PieceType.None;

        /// <summary>UCI long algebraic notation, e.g. "e2e4", "e7e8q".</summary>
        public string ToUci()
        {
            string uci = From.ToString() + To;
            if (IsPromotion)
                uci += char.ToLowerInvariant(new Piece(Promotion, PieceColor.Black).ToFenChar());
            return uci;
        }

        public override string ToString() => ToUci();

        public bool Equals(ChessMove other) => From == other.From && To == other.To && Promotion == other.Promotion;
        public override bool Equals(object obj) => obj is ChessMove other && Equals(other);
        public override int GetHashCode() => (From.Index << 9) | (To.Index << 3) | (int)Promotion;
        public static bool operator ==(ChessMove a, ChessMove b) => a.Equals(b);
        public static bool operator !=(ChessMove a, ChessMove b) => !a.Equals(b);
    }
}
