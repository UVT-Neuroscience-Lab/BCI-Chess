using System;

namespace BciChess.Core
{
    public enum PieceType : byte
    {
        None = 0,
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    public enum PieceColor : byte
    {
        White = 0,
        Black = 1
    }

    public static class PieceColorExtensions
    {
        public static PieceColor Opponent(this PieceColor color) =>
            color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public readonly struct Piece : IEquatable<Piece>
    {
        public static readonly Piece None = default;

        public readonly PieceType Type;
        public readonly PieceColor Color;

        public Piece(PieceType type, PieceColor color)
        {
            Type = type;
            // Normalize so every empty piece compares equal to Piece.None.
            Color = type == PieceType.None ? PieceColor.White : color;
        }

        public bool IsNone => Type == PieceType.None;

        public bool Is(PieceType type, PieceColor color) => Type == type && Color == color && type != PieceType.None;

        /// <summary>FEN letter: uppercase for White, lowercase for Black.</summary>
        public char ToFenChar()
        {
            char c;
            switch (Type)
            {
                case PieceType.Pawn: c = 'p'; break;
                case PieceType.Knight: c = 'n'; break;
                case PieceType.Bishop: c = 'b'; break;
                case PieceType.Rook: c = 'r'; break;
                case PieceType.Queen: c = 'q'; break;
                case PieceType.King: c = 'k'; break;
                default: return '.';
            }
            return Color == PieceColor.White ? char.ToUpperInvariant(c) : c;
        }

        public static bool TryFromFenChar(char c, out Piece piece)
        {
            var color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
            PieceType type;
            switch (char.ToLowerInvariant(c))
            {
                case 'p': type = PieceType.Pawn; break;
                case 'n': type = PieceType.Knight; break;
                case 'b': type = PieceType.Bishop; break;
                case 'r': type = PieceType.Rook; break;
                case 'q': type = PieceType.Queen; break;
                case 'k': type = PieceType.King; break;
                default:
                    piece = None;
                    return false;
            }
            piece = new Piece(type, color);
            return true;
        }

        public override string ToString() => IsNone ? "None" : $"{Color} {Type}";

        public bool Equals(Piece other) => Type == other.Type && Color == other.Color;
        public override bool Equals(object obj) => obj is Piece other && Equals(other);
        public override int GetHashCode() => ((int)Type << 1) | (int)Color;
        public static bool operator ==(Piece a, Piece b) => a.Equals(b);
        public static bool operator !=(Piece a, Piece b) => !a.Equals(b);
    }
}
