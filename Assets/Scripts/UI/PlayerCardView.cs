using System.Collections.Generic;
using BciChess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Strip above or below the board for one side: avatar, name, captured pieces with the material lead, and
    /// whether this side is to move or thinking.
    /// </summary>
    public sealed class PlayerCardView : MonoBehaviour
    {
        private const int MaxCaptured = 15;

        private BoardTheme _theme;
        private Image _body;
        private Image _activeBar;
        private Image _avatarBackground;
        private ChessPieceView _avatar;
        private Text _name;
        private Text _subtitle;
        private Text _advantage;
        private Image _statusPill;
        private Text _statusText;
        private readonly List<ChessPieceView> _captured = new List<ChessPieceView>();
        private bool _thinking;

        public PieceColor Color { get; private set; }

        public void Build(BoardTheme theme, Font glyphFont, PieceSet set)
        {
            _theme = theme;
            var card = UiFactory.CreateCard("Card", transform, theme.panel, 14f, 0.3f);
            UiFactory.Stretch(card.Root);
            _body = card.Body;

            _activeBar = UiFactory.CreateRounded("ActiveBar", _body.transform, theme.primary, 3f);
            UiFactory.SetAnchors(_activeBar.rectTransform, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f));
            _activeBar.rectTransform.offsetMin = new Vector2(6f, 0f);
            _activeBar.rectTransform.offsetMax = new Vector2(11f, 0f);

            _avatarBackground = UiFactory.CreateRounded("Avatar", _body.transform, theme.panelRaised, 10f);
            var avatarRect = _avatarBackground.rectTransform;
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.sizeDelta = new Vector2(50f, 50f);
            avatarRect.anchoredPosition = new Vector2(20f, 0f);
            var avatarPiece = UiFactory.CreateRect("Piece", avatarRect);
            UiFactory.Stretch(avatarPiece, 4f);
            _avatar = avatarPiece.gameObject.AddComponent<ChessPieceView>();
            _avatar.Build(theme, glyphFont, set);

            _name = UiFactory.CreateText("Name", _body.transform, "", 22, theme.text, TextAnchor.LowerLeft, UiFactory.Semibold);
            _name.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetBand(_name.rectTransform, 84f, 0.5f, 0.95f, 240f);

            _subtitle = UiFactory.CreateText("Subtitle", _body.transform, "", 15, theme.mutedText, TextAnchor.UpperLeft);
            _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetBand(_subtitle.rectTransform, 84f, 0.08f, 0.48f, 240f);

            var captured = UiFactory.CreateRect("Captured", _body.transform);
            SetBand(captured, 330f, 0.2f, 0.8f, 340f);
            var layout = UiFactory.AddHorizontalLayout(captured, -6f, expandWidth: false);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            for (int i = 0; i < MaxCaptured; i++)
            {
                var rect = UiFactory.CreateRect("Piece" + i, captured);
                rect.sizeDelta = new Vector2(28f, 28f);
                var view = rect.gameObject.AddComponent<ChessPieceView>();
                view.Build(theme, glyphFont, set);
                rect.gameObject.SetActive(false);
                _captured.Add(view);
            }
            _advantage = UiFactory.CreateText("Advantage", captured, "", 16, theme.mutedText, TextAnchor.MiddleLeft,
                UiFactory.Semibold);
            _advantage.horizontalOverflow = HorizontalWrapMode.Overflow;
            _advantage.rectTransform.sizeDelta = new Vector2(50f, 28f);

            _statusPill = UiFactory.CreateRounded("Status", _body.transform, theme.panelRaised, 15f);
            var pillRect = _statusPill.rectTransform;
            pillRect.anchorMin = pillRect.anchorMax = new Vector2(1f, 0.5f);
            pillRect.pivot = new Vector2(1f, 0.5f);
            pillRect.sizeDelta = new Vector2(150f, 32f);
            pillRect.anchoredPosition = new Vector2(-16f, 0f);
            _statusText = UiFactory.CreateText("Text", pillRect, "", 15, theme.text, TextAnchor.MiddleCenter, UiFactory.Semibold);
            UiFactory.Stretch(_statusText.rectTransform);
        }

        public void SetPieceSet(PieceSet set)
        {
            _avatar.SetPieceSet(set);
            foreach (var view in _captured)
                view.SetPieceSet(set);
        }

        public void SetPlayer(PieceColor color, string name, string subtitle)
        {
            Color = color;
            _name.text = name;
            _subtitle.text = subtitle;
            _avatar.SetPiece(new Piece(PieceType.King, color));
            _avatarBackground.color = color == PieceColor.White
                ? new Color(0.93f, 0.93f, 0.9f)
                : new Color(0.17f, 0.18f, 0.22f);
        }

        /// <summary>Shows who is to move. <paramref name="thinking"/> marks the computer while it searches.</summary>
        public void SetTurn(bool toMove, bool thinking, bool gameOver)
        {
            _thinking = thinking && toMove;
            _activeBar.enabled = toMove && !gameOver;
            _body.color = toMove && !gameOver ? _theme.panelRaised : _theme.panel;
            _statusPill.gameObject.SetActive(toMove && !gameOver);
            _statusPill.color = _thinking ? _theme.accent : _theme.primary;
            _statusText.text = _thinking ? "Thinking" : "To move";
        }

        /// <summary>Opponent pieces this side has captured, and its material lead (0 = none).</summary>
        public void SetCaptured(IReadOnlyList<Piece> captured, int advantage)
        {
            for (int i = 0; i < _captured.Count; i++)
            {
                bool show = i < captured.Count;
                _captured[i].gameObject.SetActive(show);
                if (show)
                    _captured[i].SetPiece(captured[i]);
            }
            _advantage.text = advantage > 0 ? $"  +{advantage}" : string.Empty;
        }

        private void Update()
        {
            if (!_thinking)
                return;
            int dots = (int)(Time.unscaledTime * 3f) % 4;
            _statusText.text = "Thinking" + new string('.', dots);
        }

        /// <summary>Left-anchored band at <paramref name="x"/> covering a vertical fraction of the card.</summary>
        private static void SetBand(RectTransform rect, float x, float yMin, float yMax, float width)
        {
            rect.anchorMin = new Vector2(0f, yMin);
            rect.anchorMax = new Vector2(0f, yMax);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
