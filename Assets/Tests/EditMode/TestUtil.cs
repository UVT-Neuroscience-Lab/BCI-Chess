using System.Collections.Generic;
using System.Linq;
using BciChess.Core;
using NUnit.Framework;

namespace BciChess.Tests
{
    internal static class TestUtil
    {
        public static Square Sq(string name) => Square.Parse(name);

        public static void Play(ChessGame game, params string[] uciMoves)
        {
            foreach (string uci in uciMoves)
            {
                var promotion = PieceType.None;
                if (uci.Length == 5)
                {
                    Piece.TryFromFenChar(uci[4], out var piece);
                    promotion = piece.Type;
                }
                bool played = game.TryMakeMove(Sq(uci.Substring(0, 2)), Sq(uci.Substring(2, 2)), promotion);
                Assert.IsTrue(played, $"Move {uci} should be legal in {game.Position.ToFen()}");
            }
        }

        public static void AssertSquares(IEnumerable<Square> actual, params string[] expected)
        {
            CollectionAssert.AreEquivalent(expected, actual.Select(s => s.ToString()).ToArray());
        }
    }
}
