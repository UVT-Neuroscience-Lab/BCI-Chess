using System;
using System.Collections.Generic;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>
    /// Keyboard control of the board for development and fallback. Feeds the same
    /// <see cref="SelectionController"/> as the mouse.
    /// </summary>
    public sealed class KeyboardBoardInput : MonoBehaviour
    {
        private static readonly KeyCode[] PromotionKeys = { KeyCode.Q, KeyCode.R, KeyCode.B, KeyCode.N };

        private SelectionController _selection;
        private BoardView _board;
        private int _candidateIndex = -1;

        /// <summary>Raised when the cursor moves or appears.</summary>
        public event Action CursorChanged;
        public event Action UndoRequested;
        public event Action NewGameRequested;
        public event Action FlipRequested;

        public Square Cursor { get; private set; } = new Square(4, 1);

        /// <summary>The cursor stays hidden until a keyboard navigation key is used.</summary>
        public bool CursorVisible { get; private set; }

        public void Initialize(SelectionController selection, BoardView board)
        {
            _selection = selection;
            _board = board;
            _selection.StateChanged += () => _candidateIndex = -1;
        }

        private void Update()
        {
            if (_selection == null)
                return;

            if (Input.GetKeyDown(KeyCode.F2))
                NewGameRequested?.Invoke();
            if (Input.GetKeyDown(KeyCode.Backspace))
                UndoRequested?.Invoke();
            if (Input.GetKeyDown(KeyCode.F))
                FlipRequested?.Invoke();

            if (_selection.State == InteractionState.SelectingPromotion)
            {
                for (int i = 0; i < PromotionKeys.Length; i++)
                {
                    if (Input.GetKeyDown(PromotionKeys[i]))
                        _selection.SelectPromotion(SelectionController.PromotionPieces[i]);
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                    _selection.Cancel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(0, -1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(1, 0);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool backwards = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                CycleCandidate(backwards ? -1 : 1);
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                if (CursorVisible)
                    _selection.SelectSquare(Cursor);
                else
                    ShowCursor();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                _selection.Cancel();
        }

        private void MoveCursor(int right, int up)
        {
            if (!CursorVisible)
            {
                ShowCursor();
                return;
            }
            var step = _board.ScreenStepToBoardStep(right, up);
            if (Cursor.TryOffset(step.x, step.y, out var next))
            {
                Cursor = next;
                CursorChanged?.Invoke();
            }
        }

        /// <summary>Jumps the cursor through the current candidates (pieces or destinations).</summary>
        private void CycleCandidate(int direction)
        {
            IReadOnlyList<Square> candidates = _selection.State == InteractionState.SelectingDestination
                ? _selection.SelectableDestinations
                : _selection.SelectablePieces;
            if (candidates.Count == 0)
                return;

            _candidateIndex = (_candidateIndex + direction + candidates.Count) % candidates.Count;
            Cursor = candidates[_candidateIndex];
            CursorVisible = true;
            CursorChanged?.Invoke();
        }

        private void ShowCursor()
        {
            CursorVisible = true;
            CursorChanged?.Invoke();
        }
    }
}
