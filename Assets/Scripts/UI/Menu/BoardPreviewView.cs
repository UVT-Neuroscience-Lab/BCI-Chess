using BciChess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Static board showing a sample position in the chosen colours and piece set (lobby preview).</summary>
    public sealed class BoardPreviewView : MonoBehaviour
    {
        // Ruy Lopez after 3.Bb5, last move f1-b5.
        private const string SampleFen = "r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3";
        private static readonly Square LastFrom = Square.Parse("f1");
        private static readonly Square LastTo = Square.Parse("b5");

        private readonly Image[] _squares = new Image[64];
        private readonly Image[] _tints = new Image[64];
        private readonly ChessPieceView[] _pieces = new ChessPieceView[64];
        private readonly Text[] _files = new Text[8];
        private readonly Text[] _ranks = new Text[8];
        private BoardTheme _theme;

        public void Build(BoardTheme theme, Font glyphFont, PieceSet set)
        {
            _theme = theme;
            var position = ChessPosition.FromFen(SampleFen);
            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                var image = UiFactory.CreateImage(square.ToString(), transform, Color.white);
                UiFactory.SetAnchors(image.rectTransform, new Vector2(square.File / 8f, square.Rank / 8f),
                    new Vector2((square.File + 1) / 8f, (square.Rank + 1) / 8f));
                _squares[i] = image;

                _tints[i] = UiFactory.CreateImage("Tint", image.transform, Color.clear);
                UiFactory.Stretch(_tints[i].rectTransform);

                if (square.Rank == 0)
                {
                    _files[square.File] = UiFactory.CreateText("File", image.transform, ((char)('a' + square.File)).ToString(),
                        14, Color.white, TextAnchor.LowerRight, UiFactory.Semibold);
                    UiFactory.Stretch(_files[square.File].rectTransform, 3f);
                }
                if (square.File == 0)
                {
                    _ranks[square.Rank] = UiFactory.CreateText("Rank", image.transform, (square.Rank + 1).ToString(),
                        14, Color.white, TextAnchor.UpperLeft, UiFactory.Semibold);
                    UiFactory.Stretch(_ranks[square.Rank].rectTransform, 3f);
                }

                var pieceRect = UiFactory.CreateRect("Piece", image.transform);
                UiFactory.Stretch(pieceRect, 2f);
                _pieces[i] = pieceRect.gameObject.AddComponent<ChessPieceView>();
                _pieces[i].Build(theme, glyphFont, set);
                _pieces[i].SetPiece(position[square]);
            }
        }

        public void Apply(GamePreferences preferences)
        {
            var scheme = preferences.BoardScheme;
            var set = preferences.PieceSet;
            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                _squares[i].color = square.IsLight ? scheme.Light : scheme.Dark;
                bool lastMove = preferences.highlightLastMove && (square == LastFrom || square == LastTo);
                _tints[i].color = lastMove ? _theme.lastMoveTint : Color.clear;
                _pieces[i].SetPieceSet(set);
            }
            for (int i = 0; i < 8; i++)
            {
                // File labels sit on rank 1 (a1 is dark), rank labels on the a-file.
                _files[i].color = new Square(i, 0).IsLight ? scheme.Dark : scheme.Light;
                _ranks[i].color = new Square(0, i).IsLight ? scheme.Dark : scheme.Light;
                _files[i].enabled = preferences.showCoordinates;
                _ranks[i].enabled = preferences.showCoordinates;
            }
        }
    }
}
