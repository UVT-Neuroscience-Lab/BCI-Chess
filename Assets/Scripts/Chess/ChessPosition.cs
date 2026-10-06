using System;
using System.Globalization;
using System.Text;

namespace BciChess.Core
{
    [Flags]
    public enum CastlingRights : byte
    {
        None = 0,
        WhiteKingSide = 1,
        WhiteQueenSide = 2,
        BlackKingSide = 4,
        BlackQueenSide = 8,
        All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
    }

    /// <summary>
    /// Complete, immutable chess position: placement, side to move, castling rights,
    /// en passant square and move clocks. Applying a move returns a new position.
    /// </summary>
    public sealed class ChessPosition
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        private readonly ChessBoard _board;

        private ChessPosition(ChessBoard board, PieceColor sideToMove, CastlingRights castlingRights,
            Square? enPassantSquare, int halfmoveClock, int fullmoveNumber)
        {
            _board = board;
            SideToMove = sideToMove;
            CastlingRights = castlingRights;
            EnPassantSquare = enPassantSquare;
            HalfmoveClock = halfmoveClock;
            FullmoveNumber = fullmoveNumber;
        }

        public PieceColor SideToMove { get; }
        public CastlingRights CastlingRights { get; }

        /// <summary>The square a pawn may capture onto en passant, if any.</summary>
        public Square? EnPassantSquare { get; }

        /// <summary>Half-moves since the last capture or pawn move (fifty-move rule).</summary>
        public int HalfmoveClock { get; }

        public int FullmoveNumber { get; }

        public Piece this[Square square] => _board[square];

        /// <summary>Read access to the placement. Do not mutate; positions are treated as immutable.</summary>
        internal ChessBoard Board => _board;

        public bool TryFindKing(PieceColor color, out Square square) => _board.TryFindKing(color, out square);

        public static ChessPosition Start() => FromFen(StartFen);

        /// <summary>
        /// Key identifying the position for repetition detection (placement, side, castling, en passant).
        /// </summary>
        public string RepetitionKey
        {
            get
            {
                string fen = ToFen();
                int cut = NthIndexOf(fen, ' ', 4);
                return cut < 0 ? fen : fen.Substring(0, cut);
            }
        }

        /// <summary>
        /// Plays a move without checking legality. Callers must pass moves produced by <see cref="MoveGenerator"/>.
        /// </summary>
        internal ChessPosition Apply(ChessMove move)
        {
            var board = _board.Clone();
            var mover = SideToMove;
            int homeRank = mover == PieceColor.White ? 0 : 7;

            board[move.From] = Piece.None;

            if (move.IsEnPassant)
                board[new Square(move.To.File, move.From.Rank)] = Piece.None;

            if ((move.Flags & MoveFlags.CastleKingSide) != 0)
            {
                board[new Square(7, homeRank)] = Piece.None;
                board[new Square(5, homeRank)] = new Piece(PieceType.Rook, mover);
            }
            else if ((move.Flags & MoveFlags.CastleQueenSide) != 0)
            {
                board[new Square(0, homeRank)] = Piece.None;
                board[new Square(3, homeRank)] = new Piece(PieceType.Rook, mover);
            }

            board[move.To] = move.IsPromotion ? new Piece(move.Promotion, mover) : move.Piece;

            var rights = CastlingRights;
            if (move.Piece.Type == PieceType.King)
            {
                rights &= mover == PieceColor.White
                    ? ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide)
                    : ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
            }
            // A rook leaving or being captured on its home corner loses that right.
            rights &= ~RightsLostAt(move.From);
            rights &= ~RightsLostAt(move.To);

            Square? enPassant = null;
            if (move.IsDoublePawnPush)
            {
                var passed = new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2);
                // Only record the square when an enemy pawn could actually capture there,
                // so identical positions compare equal for repetition detection.
                if (HasAdjacentPawn(board, move.To, mover.Opponent()))
                    enPassant = passed;
            }

            bool resetsClock = move.Piece.Type == PieceType.Pawn || move.IsCapture;
            int halfmove = resetsClock ? 0 : HalfmoveClock + 1;
            int fullmove = mover == PieceColor.Black ? FullmoveNumber + 1 : FullmoveNumber;

            return new ChessPosition(board, mover.Opponent(), rights, enPassant, halfmove, fullmove);
        }

        private static CastlingRights RightsLostAt(Square square)
        {
            switch (square.Index)
            {
                case 0: return CastlingRights.WhiteQueenSide;  // a1
                case 7: return CastlingRights.WhiteKingSide;   // h1
                case 56: return CastlingRights.BlackQueenSide; // a8
                case 63: return CastlingRights.BlackKingSide;  // h8
                default: return CastlingRights.None;
            }
        }

        private static bool HasAdjacentPawn(ChessBoard board, Square square, PieceColor color)
        {
            return (square.TryOffset(-1, 0, out var left) && board[left].Is(PieceType.Pawn, color))
                || (square.TryOffset(1, 0, out var right) && board[right].Is(PieceType.Pawn, color));
        }

        public static ChessPosition FromFen(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new FormatException("FEN is empty.");

            string[] parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                throw new FormatException($"FEN needs at least 4 fields: '{fen}'.");

            var board = new ChessBoard();
            string[] ranks = parts[0].Split('/');
            if (ranks.Length != 8)
                throw new FormatException($"FEN placement needs 8 ranks: '{parts[0]}'.");

            for (int i = 0; i < 8; i++)
            {
                int rank = 7 - i;
                int file = 0;
                foreach (char c in ranks[i])
                {
                    if (c >= '1' && c <= '8')
                    {
                        file += c - '0';
                    }
                    else if (Piece.TryFromFenChar(c, out var piece) && file < 8)
                    {
                        board[new Square(file, rank)] = piece;
                        file++;
                    }
                    else
                    {
                        throw new FormatException($"Invalid FEN rank '{ranks[i]}'.");
                    }
                }
                if (file != 8)
                    throw new FormatException($"FEN rank '{ranks[i]}' does not cover 8 files.");
            }

            PieceColor side;
            if (parts[1] == "w") side = PieceColor.White;
            else if (parts[1] == "b") side = PieceColor.Black;
            else throw new FormatException($"Invalid side to move '{parts[1]}'.");

            var rights = CastlingRights.None;
            if (parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    switch (c)
                    {
                        case 'K': rights |= CastlingRights.WhiteKingSide; break;
                        case 'Q': rights |= CastlingRights.WhiteQueenSide; break;
                        case 'k': rights |= CastlingRights.BlackKingSide; break;
                        case 'q': rights |= CastlingRights.BlackQueenSide; break;
                        default: throw new FormatException($"Invalid castling field '{parts[2]}'.");
                    }
                }
            }

            Square? enPassant = null;
            if (parts[3] != "-")
            {
                if (!Square.TryParse(parts[3], out var ep))
                    throw new FormatException($"Invalid en passant square '{parts[3]}'.");
                enPassant = ep;
            }

            int halfmove = parts.Length > 4 ? ParseInt(parts[4]) : 0;
            int fullmove = parts.Length > 5 ? ParseInt(parts[5]) : 1;

            return new ChessPosition(board, side, rights, enPassant, halfmove, Math.Max(1, fullmove));
        }

        public string ToFen()
        {
            var sb = new StringBuilder(90);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    var piece = _board[file, rank];
                    if (piece.IsNone)
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }
                    sb.Append(piece.ToFenChar());
                }
                if (empty > 0)
                    sb.Append(empty);
                if (rank > 0)
                    sb.Append('/');
            }

            sb.Append(SideToMove == PieceColor.White ? " w " : " b ");

            if (CastlingRights == CastlingRights.None)
            {
                sb.Append('-');
            }
            else
            {
                if ((CastlingRights & CastlingRights.WhiteKingSide) != 0) sb.Append('K');
                if ((CastlingRights & CastlingRights.WhiteQueenSide) != 0) sb.Append('Q');
                if ((CastlingRights & CastlingRights.BlackKingSide) != 0) sb.Append('k');
                if ((CastlingRights & CastlingRights.BlackQueenSide) != 0) sb.Append('q');
            }

            sb.Append(' ').Append(EnPassantSquare.HasValue ? EnPassantSquare.Value.ToString() : "-");
            sb.Append(' ').Append(HalfmoveClock.ToString(CultureInfo.InvariantCulture));
            sb.Append(' ').Append(FullmoveNumber.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public override string ToString() => ToFen();

        private static int ParseInt(string text)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                throw new FormatException($"Invalid FEN number '{text}'.");
            return value;
        }

        private static int NthIndexOf(string text, char c, int n)
        {
            int index = -1;
            for (int i = 0; i < n; i++)
            {
                index = text.IndexOf(c, index + 1);
                if (index < 0)
                    return -1;
            }
            return index;
        }
    }
}
