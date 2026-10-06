using BciChess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Renders one chess piece as an image from the current <see cref="PieceSet"/>, or as a Unicode glyph for the
    /// symbol set. Knows nothing about selection or BCI stimulation.
    /// </summary>
    public sealed class ChessPieceView : MonoBehaviour
    {
        private BoardTheme _theme;
        private PieceSet _set;
        private Image _image;
        private Text _text;
        private Outline _outline;

        public Piece Piece { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Build(BoardTheme theme, Font glyphFont, PieceSet set)
        {
            _theme = theme;
            _set = set;

            _image = UiFactory.CreateImage("Image", transform, Color.white);
            _image.preserveAspect = true;
            UiFactory.Stretch(_image.rectTransform);

            _text = UiFactory.CreateText("Glyph", transform, "", 40, Color.white, TextAnchor.MiddleCenter, glyphFont);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(_text.rectTransform);
            _outline = _text.gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(1.5f, -1.5f);

            UpdateFontSize();
            SetPiece(Piece.None);
        }

        public void SetPieceSet(PieceSet set)
        {
            _set = set;
            SetPiece(Piece);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_text != null)
                UpdateFontSize();
        }

        /// <summary>Scale the glyph with the square so the board works at any resolution.</summary>
        private void UpdateFontSize()
        {
            float height = ((RectTransform)transform).rect.height;
            float scale = _theme.glyphStyle == PieceGlyphStyle.Letters ? 0.6f : 0.86f;
            _text.fontSize = Mathf.Max(8, Mathf.RoundToInt(height * scale));
        }

        public void SetPiece(Piece piece)
        {
            Piece = piece;
            var sprite = _set?.GetSprite(piece);
            _image.sprite = sprite;
            _image.enabled = sprite != null;

            bool glyph = sprite == null && !piece.IsNone;
            _text.enabled = glyph;
            if (!glyph)
                return;
            _text.text = PieceGlyphs.Get(piece, _theme.glyphStyle);
            bool white = piece.Color == PieceColor.White;
            _text.color = white ? _theme.whitePiece : _theme.blackPiece;
            _outline.effectColor = white ? _theme.whitePieceOutline : _theme.blackPieceOutline;
        }
    }

    public static class PieceGlyphs
    {
        public static string Get(Piece piece, PieceGlyphStyle style)
        {
            if (piece.IsNone)
                return string.Empty;
            if (style == PieceGlyphStyle.Letters)
                return piece.Type == PieceType.Pawn ? "P" : MoveNotation.PieceLetter(piece.Type);

            // Filled symbols for both colours; colour comes from the text tint.
            switch (piece.Type)
            {
                case PieceType.King: return "♚";
                case PieceType.Queen: return "♛";
                case PieceType.Rook: return "♜";
                case PieceType.Bishop: return "♝";
                case PieceType.Knight: return "♞";
                default: return "♟";
            }
        }
    }
}
