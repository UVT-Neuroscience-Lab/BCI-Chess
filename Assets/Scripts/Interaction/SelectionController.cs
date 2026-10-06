using System;
using System.Collections.Generic;
using BciChess.Core;

namespace BciChess.Interaction
{
    /// <summary>What the game currently expects from the player.</summary>
    public enum InteractionState
    {
        WaitingForPlayer,
        SelectingPiece,
        SelectingDestination,
        SelectingPromotion,
        ProcessingSelection,
        ComputerThinking,
        GameOver
    }

    /// <summary>
    /// Turns square/promotion selections into chess moves. Every input source (mouse, keyboard,
    /// and later BCI) feeds this one pipeline; it never computes legality itself but asks <see cref="ChessGame"/>.
    /// </summary>
    public sealed class SelectionController : IDisposable
    {
        private static readonly PieceType[] PromotionChoices =
            { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        private static readonly IReadOnlyList<Square> NoSquares = Array.Empty<Square>();

        private readonly ChessGame _game;
        private IReadOnlyList<Square> _destinations = NoSquares;
        private PieceColor? _computerSide;

        public SelectionController(ChessGame game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _game.PositionChanged += Refresh;
            Refresh();
        }

        /// <summary>Raised after any change of state or selection.</summary>
        public event Action StateChanged;

        public ChessGame Game => _game;
        public InteractionState State { get; private set; }
        public Square? SelectedPiece { get; private set; }

        /// <summary>Destination awaiting a promotion choice, while in <see cref="InteractionState.SelectingPromotion"/>.</summary>
        public Square? PromotionTarget { get; private set; }

        public static IReadOnlyList<PieceType> PromotionPieces => PromotionChoices;

        /// <summary>
        /// The colour played by the computer, or null when humans play both sides. On the computer's turn the
        /// state is <see cref="InteractionState.ComputerThinking"/> and player selections are ignored.
        /// </summary>
        public PieceColor? ComputerSide
        {
            get => _computerSide;
            set
            {
                _computerSide = value;
                Refresh();
            }
        }

        /// <summary>Pieces that may be (re)selected right now.</summary>
        public IReadOnlyList<Square> SelectablePieces =>
            State == InteractionState.SelectingPiece || State == InteractionState.SelectingDestination
                ? _game.GetSelectablePieces()
                : NoSquares;

        /// <summary>Legal destinations of the selected piece.</summary>
        public IReadOnlyList<Square> SelectableDestinations =>
            State == InteractionState.SelectingDestination ? _destinations : NoSquares;

        /// <summary>
        /// Selects a square: picks a piece, picks a destination, switches to another own piece,
        /// or deselects the current piece. Returns false if the square is not a valid choice.
        /// </summary>
        public bool SelectSquare(Square square)
        {
            switch (State)
            {
                case InteractionState.SelectingPiece:
                    return TrySelectPiece(square);

                case InteractionState.SelectingDestination:
                    if (SelectedPiece == square)
                    {
                        ClearSelection();
                        return true;
                    }
                    if (Contains(_destinations, square))
                        return ChooseDestination(square);
                    return TrySelectPiece(square);

                default:
                    return false;
            }
        }

        public bool SelectPromotion(PieceType type)
        {
            if (State != InteractionState.SelectingPromotion || Array.IndexOf(PromotionChoices, type) < 0)
                return false;
            return _game.TryMakeMove(SelectedPiece.Value, PromotionTarget.Value, type);
        }

        /// <summary>Steps back one level: promotion -> destination -> piece.</summary>
        public void Cancel()
        {
            if (State == InteractionState.SelectingPromotion)
            {
                PromotionTarget = null;
                SetState(InteractionState.SelectingDestination);
            }
            else if (State == InteractionState.SelectingDestination)
            {
                ClearSelection();
            }
        }

        /// <summary>Clears any selection and re-reads the game. Called automatically when the position changes.</summary>
        public void Refresh()
        {
            SelectedPiece = null;
            PromotionTarget = null;
            _destinations = NoSquares;
            if (_game.IsGameOver)
                SetState(InteractionState.GameOver);
            else if (_computerSide == _game.SideToMove)
                SetState(InteractionState.ComputerThinking);
            else
                SetState(InteractionState.SelectingPiece);
        }

        public void Dispose()
        {
            _game.PositionChanged -= Refresh;
        }

        private bool TrySelectPiece(Square square)
        {
            if (!Contains(_game.GetSelectablePieces(), square))
                return false;

            SelectedPiece = square;
            PromotionTarget = null;
            _destinations = _game.GetSelectableDestinations(square);
            SetState(InteractionState.SelectingDestination);
            return true;
        }

        private bool ChooseDestination(Square destination)
        {
            var from = SelectedPiece.Value;
            if (_game.RequiresPromotion(from, destination))
            {
                PromotionTarget = destination;
                SetState(InteractionState.SelectingPromotion);
                return true;
            }
            // On success the game raises PositionChanged, which refreshes this controller.
            return _game.TryMakeMove(from, destination);
        }

        private void ClearSelection()
        {
            SelectedPiece = null;
            _destinations = NoSquares;
            SetState(InteractionState.SelectingPiece);
        }

        private void SetState(InteractionState state)
        {
            State = state;
            StateChanged?.Invoke();
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
