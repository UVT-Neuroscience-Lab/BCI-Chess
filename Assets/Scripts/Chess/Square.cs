using System;

namespace BciChess.Core
{
    /// <summary>
    /// A board square. Index 0 = a1, 7 = h1, 56 = a8, 63 = h8.
    /// </summary>
    public readonly struct Square : IEquatable<Square>
    {
        public readonly int Index;

        public Square(int index)
        {
            if (index < 0 || index > 63)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Square index must be 0..63.");
            Index = index;
        }

        public Square(int file, int rank)
        {
            if (!IsOnBoard(file, rank))
                throw new ArgumentOutOfRangeException(nameof(file), $"({file},{rank}) is off the board.");
            Index = rank * 8 + file;
        }

        /// <summary>0 = a-file, 7 = h-file.</summary>
        public int File => Index & 7;

        /// <summary>0 = rank 1, 7 = rank 8.</summary>
        public int Rank => Index >> 3;

        /// <summary>True for light squares (a1 is dark, h1 is light).</summary>
        public bool IsLight => ((File + Rank) & 1) == 1;

        public static bool IsOnBoard(int file, int rank) => file >= 0 && file < 8 && rank >= 0 && rank < 8;

        public bool TryOffset(int fileDelta, int rankDelta, out Square result)
        {
            int file = File + fileDelta;
            int rank = Rank + rankDelta;
            if (IsOnBoard(file, rank))
            {
                result = new Square(file, rank);
                return true;
            }
            result = default;
            return false;
        }

        public static bool TryParse(string text, out Square square)
        {
            square = default;
            if (text == null || text.Length != 2)
                return false;
            int file = char.ToLowerInvariant(text[0]) - 'a';
            int rank = text[1] - '1';
            if (!IsOnBoard(file, rank))
                return false;
            square = new Square(file, rank);
            return true;
        }

        public static Square Parse(string text)
        {
            if (!TryParse(text, out var square))
                throw new FormatException($"'{text}' is not a valid square.");
            return square;
        }

        public override string ToString() => $"{(char)('a' + File)}{Rank + 1}";

        public bool Equals(Square other) => Index == other.Index;
        public override bool Equals(object obj) => obj is Square other && Equals(other);
        public override int GetHashCode() => Index;
        public static bool operator ==(Square a, Square b) => a.Index == b.Index;
        public static bool operator !=(Square a, Square b) => a.Index != b.Index;
    }
}
