using System;

namespace BciChess.Core
{
    /// <summary>Piece placement only. Turn, castling rights etc. live in <see cref="ChessPosition"/>.</summary>
    public sealed class ChessBoard
    {
        private readonly Piece[] _squares = new Piece[64];

        public Piece this[Square square]
        {
            get => _squares[square.Index];
            internal set => _squares[square.Index] = value;
        }

        public Piece this[int file, int rank] => _squares[rank * 8 + file];

        public ChessBoard Clone()
        {
            var copy = new ChessBoard();
            Array.Copy(_squares, copy._squares, _squares.Length);
            return copy;
        }

        public bool TryFindKing(PieceColor color, out Square square)
        {
            for (int i = 0; i < 64; i++)
            {
                if (_squares[i].Is(PieceType.King, color))
                {
                    square = new Square(i);
                    return true;
                }
            }
            square = default;
            return false;
        }
    }
}
