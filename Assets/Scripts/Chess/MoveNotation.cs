namespace BciChess.Core
{
    /// <summary>Human-readable move formatting, e.g. "Ng1-f3", "e5xd6", "e7-e8=Q", "O-O".</summary>
    public static class MoveNotation
    {
        public static string ToLongAlgebraic(ChessMove move)
        {
            if ((move.Flags & MoveFlags.CastleKingSide) != 0)
                return "O-O";
            if ((move.Flags & MoveFlags.CastleQueenSide) != 0)
                return "O-O-O";

            string text = PieceLetter(move.Piece.Type) + move.From + (move.IsCapture ? "x" : "-") + move.To;
            if (move.IsPromotion)
                text += "=" + PieceLetter(move.Promotion);
            return text;
        }

        public static string PieceLetter(PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight: return "N";
                case PieceType.Bishop: return "B";
                case PieceType.Rook: return "R";
                case PieceType.Queen: return "Q";
                case PieceType.King: return "K";
                default: return "";
            }
        }
    }
}
