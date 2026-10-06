using System.Collections.Generic;

namespace BciChess.Core
{
    /// <summary>Generates legal moves and answers attack/check queries. Stateless.</summary>
    public static class MoveGenerator
    {
        private static readonly int[,] KnightOffsets =
            { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };

        private static readonly int[,] KingOffsets =
            { { 1, 0 }, { 1, 1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, -1 } };

        private static readonly int[,] RookDirections = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] BishopDirections = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        private static readonly PieceType[] PromotionTypes =
            { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        public static List<ChessMove> GenerateLegalMoves(ChessPosition position)
        {
            var pseudo = GeneratePseudoLegalMoves(position);
            var legal = new List<ChessMove>(pseudo.Count);
            var mover = position.SideToMove;
            foreach (var move in pseudo)
            {
                if (!IsInCheck(position.Apply(move).Board, mover))
                    legal.Add(move);
            }
            return legal;
        }

        /// <summary>Counts leaf nodes of the legal move tree. Used to validate move generation.</summary>
        public static long Perft(ChessPosition position, int depth)
        {
            if (depth <= 0)
                return 1;
            var moves = GenerateLegalMoves(position);
            if (depth == 1)
                return moves.Count;
            long nodes = 0;
            foreach (var move in moves)
                nodes += Perft(position.Apply(move), depth - 1);
            return nodes;
        }

        public static bool IsInCheck(ChessPosition position) => IsInCheck(position.Board, position.SideToMove);

        public static bool IsInCheck(ChessPosition position, PieceColor color) => IsInCheck(position.Board, color);

        internal static bool IsInCheck(ChessBoard board, PieceColor color) =>
            board.TryFindKing(color, out var king) && IsSquareAttacked(board, king, color.Opponent());

        public static bool IsSquareAttacked(ChessPosition position, Square target, PieceColor attacker) =>
            IsSquareAttacked(position.Board, target, attacker);

        internal static bool IsSquareAttacked(ChessBoard board, Square target, PieceColor attacker)
        {
            int f = target.File;
            int r = target.Rank;

            int pawnRank = attacker == PieceColor.White ? r - 1 : r + 1;
            if (IsPieceAt(board, f - 1, pawnRank, PieceType.Pawn, attacker) ||
                IsPieceAt(board, f + 1, pawnRank, PieceType.Pawn, attacker))
                return true;

            for (int i = 0; i < 8; i++)
            {
                if (IsPieceAt(board, f + KnightOffsets[i, 0], r + KnightOffsets[i, 1], PieceType.Knight, attacker))
                    return true;
                if (IsPieceAt(board, f + KingOffsets[i, 0], r + KingOffsets[i, 1], PieceType.King, attacker))
                    return true;
            }

            return IsAttackedAlongRays(board, f, r, RookDirections, PieceType.Rook, attacker)
                || IsAttackedAlongRays(board, f, r, BishopDirections, PieceType.Bishop, attacker);
        }

        private static bool IsAttackedAlongRays(ChessBoard board, int f, int r, int[,] directions,
            PieceType slider, PieceColor attacker)
        {
            for (int d = 0; d < directions.GetLength(0); d++)
            {
                int df = directions[d, 0], dr = directions[d, 1];
                int cf = f + df, cr = r + dr;
                while (Square.IsOnBoard(cf, cr))
                {
                    var piece = board[cf, cr];
                    if (!piece.IsNone)
                    {
                        if (piece.Color == attacker && (piece.Type == slider || piece.Type == PieceType.Queen))
                            return true;
                        break;
                    }
                    cf += df;
                    cr += dr;
                }
            }
            return false;
        }

        private static bool IsPieceAt(ChessBoard board, int file, int rank, PieceType type, PieceColor color) =>
            Square.IsOnBoard(file, rank) && board[file, rank].Is(type, color);

        /// <summary>Moves that obey piece movement rules but may leave the own king in check.</summary>
        private static List<ChessMove> GeneratePseudoLegalMoves(ChessPosition position)
        {
            var moves = new List<ChessMove>(48);
            var board = position.Board;
            var side = position.SideToMove;

            for (int i = 0; i < 64; i++)
            {
                var piece = board[new Square(i)];
                if (piece.IsNone || piece.Color != side)
                    continue;

                var from = new Square(i);
                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        AddPawnMoves(position, from, piece, moves);
                        break;
                    case PieceType.Knight:
                        AddStepMoves(board, from, piece, KnightOffsets, moves);
                        break;
                    case PieceType.Bishop:
                        AddSlidingMoves(board, from, piece, BishopDirections, moves);
                        break;
                    case PieceType.Rook:
                        AddSlidingMoves(board, from, piece, RookDirections, moves);
                        break;
                    case PieceType.Queen:
                        AddSlidingMoves(board, from, piece, RookDirections, moves);
                        AddSlidingMoves(board, from, piece, BishopDirections, moves);
                        break;
                    case PieceType.King:
                        AddStepMoves(board, from, piece, KingOffsets, moves);
                        AddCastlingMoves(position, from, piece, moves);
                        break;
                }
            }
            return moves;
        }

        private static void AddPawnMoves(ChessPosition position, Square from, Piece pawn, List<ChessMove> moves)
        {
            var board = position.Board;
            int dir = pawn.Color == PieceColor.White ? 1 : -1;
            int startRank = pawn.Color == PieceColor.White ? 1 : 6;
            int promotionRank = pawn.Color == PieceColor.White ? 7 : 0;

            if (from.TryOffset(0, dir, out var one) && board[one].IsNone)
            {
                AddPawnMove(from, one, pawn, Piece.None, MoveFlags.None, promotionRank, moves);

                if (from.Rank == startRank && from.TryOffset(0, 2 * dir, out var two) && board[two].IsNone)
                    moves.Add(new ChessMove(from, two, pawn, flags: MoveFlags.DoublePawnPush));
            }

            for (int df = -1; df <= 1; df += 2)
            {
                if (!from.TryOffset(df, dir, out var target))
                    continue;

                var victim = board[target];
                if (!victim.IsNone && victim.Color != pawn.Color)
                {
                    AddPawnMove(from, target, pawn, victim, MoveFlags.Capture, promotionRank, moves);
                }
                else if (victim.IsNone && position.EnPassantSquare == target)
                {
                    var captured = new Piece(PieceType.Pawn, pawn.Color.Opponent());
                    moves.Add(new ChessMove(from, target, pawn, captured, PieceType.None,
                        MoveFlags.Capture | MoveFlags.EnPassant));
                }
            }
        }

        private static void AddPawnMove(Square from, Square to, Piece pawn, Piece captured, MoveFlags flags,
            int promotionRank, List<ChessMove> moves)
        {
            if (to.Rank == promotionRank)
            {
                foreach (var type in PromotionTypes)
                    moves.Add(new ChessMove(from, to, pawn, captured, type, flags));
            }
            else
            {
                moves.Add(new ChessMove(from, to, pawn, captured, PieceType.None, flags));
            }
        }

        private static void AddStepMoves(ChessBoard board, Square from, Piece piece, int[,] offsets,
            List<ChessMove> moves)
        {
            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                if (!from.TryOffset(offsets[i, 0], offsets[i, 1], out var to))
                    continue;
                var target = board[to];
                if (target.IsNone)
                    moves.Add(new ChessMove(from, to, piece));
                else if (target.Color != piece.Color)
                    moves.Add(new ChessMove(from, to, piece, target, PieceType.None, MoveFlags.Capture));
            }
        }

        private static void AddSlidingMoves(ChessBoard board, Square from, Piece piece, int[,] directions,
            List<ChessMove> moves)
        {
            for (int d = 0; d < directions.GetLength(0); d++)
            {
                var current = from;
                while (current.TryOffset(directions[d, 0], directions[d, 1], out var to))
                {
                    var target = board[to];
                    if (target.IsNone)
                    {
                        moves.Add(new ChessMove(from, to, piece));
                    }
                    else
                    {
                        if (target.Color != piece.Color)
                            moves.Add(new ChessMove(from, to, piece, target, PieceType.None, MoveFlags.Capture));
                        break;
                    }
                    current = to;
                }
            }
        }

        private static void AddCastlingMoves(ChessPosition position, Square from, Piece king, List<ChessMove> moves)
        {
            var board = position.Board;
            var color = king.Color;
            int rank = color == PieceColor.White ? 0 : 7;
            if (from != new Square(4, rank))
                return;

            var kingSide = color == PieceColor.White ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
            var queenSide = color == PieceColor.White ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;
            if ((position.CastlingRights & (kingSide | queenSide)) == 0)
                return;

            var enemy = color.Opponent();
            if (IsSquareAttacked(board, from, enemy))
                return;

            var rook = new Piece(PieceType.Rook, color);

            if ((position.CastlingRights & kingSide) != 0
                && board[7, rank] == rook
                && board[5, rank].IsNone && board[6, rank].IsNone
                && !IsSquareAttacked(board, new Square(5, rank), enemy)
                && !IsSquareAttacked(board, new Square(6, rank), enemy))
            {
                moves.Add(new ChessMove(from, new Square(6, rank), king, flags: MoveFlags.CastleKingSide));
            }

            if ((position.CastlingRights & queenSide) != 0
                && board[0, rank] == rook
                && board[1, rank].IsNone && board[2, rank].IsNone && board[3, rank].IsNone
                && !IsSquareAttacked(board, new Square(3, rank), enemy)
                && !IsSquareAttacked(board, new Square(2, rank), enemy))
            {
                moves.Add(new ChessMove(from, new Square(2, rank), king, flags: MoveFlags.CastleQueenSide));
            }
        }
    }
}
