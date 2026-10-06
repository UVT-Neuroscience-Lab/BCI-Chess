using System;
using System.Collections.Generic;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BciChess.UI
{
    /// <summary>
    /// The 8x8 board. Presents the state of <see cref="ChessGame"/> and <see cref="SelectionController"/>
    /// and reports clicks; it contains no chess rules.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float MoveAnimationSeconds = 0.16f;

        private readonly SquareView[] _squares = new SquareView[64];
        private readonly List<PieceSlide> _slides = new List<PieceSlide>();
        private bool _flipped;
        private bool _showCoordinates = true;
        private bool _showLegalMoves = true;
        private bool _highlightLastMove = true;

        private struct PieceSlide
        {
            public SquareView From;
            public SquareView To;
            public float Elapsed;
        }

        /// <summary>Left click on a square.</summary>
        public event Action<Square> SquareClicked;

        /// <summary>Right click anywhere on the board.</summary>
        public event Action CancelRequested;

        /// <summary>When true, Black is at the bottom.</summary>
        public bool Flipped
        {
            get => _flipped;
            set
            {
                _flipped = value;
                Layout();
            }
        }

        public SquareView GetSquareView(Square square) => _squares[square.Index];

        public void Build(BoardTheme theme, Font glyphFont, PieceSet pieceSet)
        {
            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                var rect = UiFactory.CreateRect(square.ToString(), transform);
                var view = rect.gameObject.AddComponent<SquareView>();
                view.Build(square, theme, glyphFont, pieceSet);
                view.Clicked += OnSquareClicked;
                _squares[i] = view;
            }
            Layout();
        }

        /// <summary>Applies the player's board colours, piece set and display options.</summary>
        public void ApplyAppearance(GamePreferences preferences)
        {
            var scheme = preferences.BoardScheme;
            var set = preferences.PieceSet;
            foreach (var view in _squares)
            {
                view.SetColors(scheme);
                view.PieceView.SetPieceSet(set);
            }
            _showCoordinates = preferences.showCoordinates;
            _showLegalMoves = preferences.showLegalMoves;
            _highlightLastMove = preferences.highlightLastMove;
            Layout();
        }

        public void Render(ChessGame game, SelectionController selection, Square? cursor)
        {
            var lastMove = _highlightLastMove ? game.LastMove : null;
            Square? checkedKing = null;
            if (game.IsInCheck && game.Position.TryFindKing(game.SideToMove, out var king))
                checkedKing = king;

            var selectable = selection.State == InteractionState.SelectingPiece
                ? selection.SelectablePieces
                : (IReadOnlyList<Square>)Array.Empty<Square>();
            var destinations = _showLegalMoves ? selection.SelectableDestinations : Array.Empty<Square>();

            var captureSquares = new HashSet<Square>();
            if (selection.SelectedPiece.HasValue)
            {
                foreach (var move in game.GetLegalMoves(selection.SelectedPiece.Value))
                {
                    if (move.IsCapture)
                        captureSquares.Add(move.To);
                }
            }

            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                bool isDestination = Contains(destinations, square);
                var visual = new SquareVisual
                {
                    Piece = game.Position[square],
                    IsLastMove = lastMove.HasValue && (lastMove.Value.From == square || lastMove.Value.To == square),
                    IsSelected = selection.SelectedPiece == square || selection.PromotionTarget == square,
                    IsSelectable = Contains(selectable, square),
                    IsDestination = isDestination,
                    IsCaptureDestination = isDestination && captureSquares.Contains(square),
                    IsCheck = checkedKing == square,
                    HasCursor = cursor == square
                };
                _squares[i].Render(visual);
            }
        }

        /// <summary>Slides the moved piece (and the rook when castling) from its origin to its new square.</summary>
        public void AnimateMove(ChessMove move)
        {
            StartSlide(move.From, move.To);
            if (move.IsCastling)
            {
                int rank = move.From.Rank;
                bool kingSide = move.To.File > move.From.File;
                StartSlide(new Square(kingSide ? 7 : 0, rank), new Square(kingSide ? 5 : 3, rank));
            }
        }

        /// <summary>Snaps every piece to its square (after undo, reset, ...).</summary>
        public void StopAnimations()
        {
            foreach (var slide in _slides)
                slide.To.PieceView.Rect.anchoredPosition = Vector2.zero;
            _slides.Clear();
        }

        /// <summary>Converts a step in screen direction (right/up) into a step in board files/ranks.</summary>
        public Vector2Int ScreenStepToBoardStep(int right, int up) =>
            _flipped ? new Vector2Int(-right, -up) : new Vector2Int(right, up);

        private void StartSlide(Square from, Square to)
        {
            var slide = new PieceSlide { From = _squares[from.Index], To = _squares[to.Index] };
            // Draw the moving piece above the squares it passes over.
            slide.To.transform.SetAsLastSibling();
            _slides.Add(slide);
            UpdateSlide(ref slide);
        }

        private void Update()
        {
            for (int i = _slides.Count - 1; i >= 0; i--)
            {
                var slide = _slides[i];
                slide.Elapsed += Time.unscaledDeltaTime;
                if (UpdateSlide(ref slide))
                    _slides[i] = slide;
                else
                    _slides.RemoveAt(i);
            }
        }

        /// <summary>Positions the sliding piece; returns false once it has arrived.</summary>
        private static bool UpdateSlide(ref PieceSlide slide)
        {
            float t = Mathf.Clamp01(slide.Elapsed / MoveAnimationSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            // Recomputed every frame so a board flip during the slide is handled.
            Vector2 offset = slide.From.Rect.localPosition - slide.To.Rect.localPosition;
            slide.To.PieceView.Rect.anchoredPosition = offset * (1f - eased);
            return t < 1f;
        }

        private void Layout()
        {
            foreach (var view in _squares)
            {
                if (view == null)
                    continue;
                int column = _flipped ? 7 - view.Square.File : view.Square.File;
                int row = _flipped ? 7 - view.Square.Rank : view.Square.Rank;
                UiFactory.SetAnchors(view.Rect, new Vector2(column / 8f, row / 8f),
                    new Vector2((column + 1) / 8f, (row + 1) / 8f));
                view.SetCoordinateLabels(showFile: _showCoordinates && row == 0, showRank: _showCoordinates && column == 0);
            }
        }

        private void OnSquareClicked(SquareView view, PointerEventData.InputButton button)
        {
            if (button == PointerEventData.InputButton.Left)
                SquareClicked?.Invoke(view.Square);
            else if (button == PointerEventData.InputButton.Right)
                CancelRequested?.Invoke();
        }

        private static bool Contains(IReadOnlyList<Square> squares, Square square)
        {
            for (int i = 0; i < squares.Count; i++)
            {
                if (squares[i] == square)
                    return true;
            }
            return false;
        }
    }
}
