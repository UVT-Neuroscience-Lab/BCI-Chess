using System;
using BciChess.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Everything a square needs to draw itself for one frame.</summary>
    public struct SquareVisual
    {
        public Piece Piece;
        public bool IsLastMove;
        public bool IsSelected;
        public bool IsSelectable;
        public bool IsDestination;
        public bool IsCaptureDestination;
        public bool IsCheck;
        public bool HasCursor;
    }

    /// <summary>One board square: background, highlight tint, move marker, cursor frame and coordinate labels.</summary>
    public sealed class SquareView : MonoBehaviour, IPointerClickHandler
    {
        private BoardTheme _theme;
        private Image _background;
        private Image _tint;
        private Image _checkGlow;
        private Image _marker;
        private Image _cursor;
        private Text _fileLabel;
        private Text _rankLabel;

        public event Action<SquareView, PointerEventData.InputButton> Clicked;

        public Square Square { get; private set; }
        public ChessPieceView PieceView { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Build(Square square, BoardTheme theme, Font glyphFont, PieceSet pieceSet)
        {
            Square = square;
            _theme = theme;

            _background = gameObject.AddComponent<Image>();
            _background.raycastTarget = true;

            _tint = UiFactory.CreateImage("Tint", transform, Color.clear);
            UiFactory.Stretch(_tint.rectTransform);

            _checkGlow = UiFactory.CreateImage("CheckGlow", transform, theme.checkTint, UiFactory.Glow);
            UiFactory.Stretch(_checkGlow.rectTransform);
            _checkGlow.enabled = false;

            _fileLabel = UiFactory.CreateText("FileLabel", transform, ((char)('a' + square.File)).ToString(), 17,
                Color.white, TextAnchor.LowerRight, UiFactory.Semibold);
            UiFactory.Stretch(_fileLabel.rectTransform, 4f);
            _fileLabel.rectTransform.offsetMin = new Vector2(4f, 1f);
            _rankLabel = UiFactory.CreateText("RankLabel", transform, (square.Rank + 1).ToString(), 17,
                Color.white, TextAnchor.UpperLeft, UiFactory.Semibold);
            UiFactory.Stretch(_rankLabel.rectTransform, 4f);
            _rankLabel.rectTransform.offsetMax = new Vector2(-4f, -1f);

            var pieceRect = UiFactory.CreateRect("Piece", transform);
            UiFactory.Stretch(pieceRect, 3f);
            PieceView = pieceRect.gameObject.AddComponent<ChessPieceView>();
            PieceView.Build(theme, glyphFont, pieceSet);

            _marker = UiFactory.CreateImage("MoveMarker", transform, theme.destinationMarker, UiFactory.Circle);
            _marker.enabled = false;

            _cursor = UiFactory.CreateImage("KeyboardCursor", transform, theme.cursor, UiFactory.Frame);
            _cursor.type = Image.Type.Sliced;
            UiFactory.Stretch(_cursor.rectTransform);
            _cursor.enabled = false;
        }

        /// <summary>Applies square colours; coordinate labels use the opposite square colour.</summary>
        public void SetColors(BoardColorScheme scheme)
        {
            _background.color = Square.IsLight ? scheme.Light : scheme.Dark;
            var labelColor = Square.IsLight ? scheme.Dark : scheme.Light;
            _fileLabel.color = labelColor;
            _rankLabel.color = labelColor;
        }

        public void SetCoordinateLabels(bool showFile, bool showRank)
        {
            _fileLabel.enabled = showFile;
            _rankLabel.enabled = showRank;
        }

        public void Render(in SquareVisual visual)
        {
            PieceView.SetPiece(visual.Piece);

            if (visual.IsSelected)
                _tint.color = _theme.selectedTint;
            else if (visual.IsSelectable)
                _tint.color = _theme.selectableTint;
            else if (visual.IsLastMove)
                _tint.color = _theme.lastMoveTint;
            else
                _tint.color = Color.clear;
            _checkGlow.enabled = visual.IsCheck;

            _marker.enabled = visual.IsDestination;
            if (visual.IsDestination)
            {
                // Dot for quiet moves, ring around the victim for captures.
                _marker.sprite = visual.IsCaptureDestination ? UiFactory.Ring : UiFactory.Circle;
                if (visual.IsCaptureDestination)
                    UiFactory.SetAnchors(_marker.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
                else
                    UiFactory.SetAnchors(_marker.rectTransform, new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f));
            }

            _cursor.enabled = visual.HasCursor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this, eventData.button);
        }
    }
}
