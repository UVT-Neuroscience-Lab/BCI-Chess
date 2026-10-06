using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Engine;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Who plays what in the current game, for display.</summary>
    public sealed class MatchInfo
    {
        public GameMode Mode;

        /// <summary>The human's colour against the computer.</summary>
        public PieceColor HumanColor;

        public DifficultyLevel Level;

        public bool IsVsComputer => Mode == GameMode.VsComputer;
    }

    /// <summary>
    /// Side panel (turn, prompt, move list, BCI status, buttons), the player strips above and below the board,
    /// the promotion picker and the game-over dialog.
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        private const int MoveRows = 8;

        private static readonly PieceType[] CaptureOrder =
            { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen };

        private BoardTheme _theme;
        private Font _glyphFont;
        private PlayerCardView _topPlayer;
        private PlayerCardView _bottomPlayer;
        private Text _matchText;
        private Image _turnDot;
        private Text _turnText;
        private GameObject _checkBadge;
        private Text _promptText;
        private Text _stateText;
        private Text _engineText;
        private readonly List<MoveRow> _moveRows = new List<MoveRow>();
        private Text _noMovesText;
        private Button _undoButton;
        private GameObject _promotionOverlay;
        private readonly List<ChessPieceView> _promotionPieces = new List<ChessPieceView>();
        private readonly List<Image> _promotionTiles = new List<Image>();
        private GameObject _gameOverOverlay;
        private Text _gameOverTitle;
        private Text _gameOverSubtitle;
        private bool _gameOverDismissed;
        private Text _bciStatusText;
        private Text _bciTargetsText;
        private Text _bciMessageText;
        private readonly List<Text> _promotionKeyLabels = new List<Text>();
        private Text _promotionCancelLabel;
        private readonly Dictionary<PieceType, RectTransform> _promotionButtons = new Dictionary<PieceType, RectTransform>();
        private RectTransform _promotionCancelButton;
        private GameObject _listeningPill;
        private Image _listeningDot;
        private bool _listening;

        private struct MoveRow
        {
            public Image Background;
            public Text Number;
            public Text White;
            public Text Black;
        }

        /// <summary>The promotion picker button for <paramref name="type"/> (for attaching BCI visuals).</summary>
        public RectTransform GetPromotionButton(PieceType type) => _promotionButtons[type];

        public RectTransform PromotionCancelButton => _promotionCancelButton;

        public event Action NewGameClicked;
        public event Action UndoClicked;
        public event Action FlipClicked;
        public event Action MenuClicked;
        public event Action<PieceType> PromotionChosen;
        public event Action PromotionCancelled;

        public void Build(RectTransform canvasRoot, RectTransform panel, RectTransform boardFrame,
            PlayerCardView topPlayer, PlayerCardView bottomPlayer, BoardTheme theme, Font glyphFont, PieceSet set)
        {
            _theme = theme;
            _glyphFont = glyphFont;
            _topPlayer = topPlayer;
            _bottomPlayer = bottomPlayer;
            BuildSidePanel(panel);
            BuildGameOverDialog(boardFrame);
            BuildPromotionOverlay(canvasRoot, boardFrame, set);
        }

        public void ApplyAppearance(GamePreferences preferences)
        {
            var set = preferences.PieceSet;
            foreach (var view in _promotionPieces)
                view.SetPieceSet(set);
            foreach (var tile in _promotionTiles)
                tile.color = preferences.BoardScheme.Light;
            _topPlayer.SetPieceSet(set);
            _bottomPlayer.SetPieceSet(set);
        }

        public void Render(ChessGame game, SelectionController selection)
        {
            if (game.IsGameOver)
            {
                _turnText.text = "Game over";
                _turnDot.enabled = false;
            }
            else
            {
                _turnText.text = game.SideToMove == PieceColor.White ? "White to move" : "Black to move";
                _turnDot.enabled = true;
                _turnDot.color = game.SideToMove == PieceColor.White ? new Color(0.95f, 0.95f, 0.93f) : new Color(0.08f, 0.08f, 0.1f);
            }
            _checkBadge.SetActive(game.IsInCheck && !game.IsGameOver);

            _promptText.text = Prompt(game, selection);
            _stateText.text = ToConstantName(selection.State.ToString());
            RenderMoves(game.MoveHistory);
            _undoButton.interactable = game.MoveHistory.Count > 0;

            if (!game.IsGameOver)
                _gameOverDismissed = false;
            _gameOverOverlay.SetActive(game.IsGameOver && !_gameOverDismissed);

            bool promoting = selection.State == InteractionState.SelectingPromotion;
            _promotionOverlay.SetActive(promoting);
            if (promoting)
            {
                for (int i = 0; i < _promotionPieces.Count; i++)
                    _promotionPieces[i].SetPiece(new Piece(SelectionController.PromotionPieces[i], game.SideToMove));
            }
        }

        /// <summary>Player strips, match summary and engine state. <paramref name="computer"/> is null in local games.</summary>
        public void RenderMatch(ChessGame game, MatchInfo match, ComputerPlayer computer, bool boardFlipped)
        {
            if (match == null)
                return;

            var bottomColor = boardFlipped ? PieceColor.Black : PieceColor.White;
            var topColor = bottomColor.Opponent();
            RenderPlayer(_bottomPlayer, bottomColor, game, match, computer);
            RenderPlayer(_topPlayer, topColor, game, match, computer);

            if (match.IsVsComputer)
            {
                _matchText.text = $"vs {(computer != null ? computer.EngineName : "Stockfish")}  ·  {match.Level.name}  ·  " +
                                  $"you play {match.HumanColor}";
                _engineText.text = computer != null ? computer.Message : string.Empty;
            }
            else
            {
                _matchText.text = "Local 2-player  ·  both sides on this screen";
                _engineText.text = string.Empty;
            }

            if (game.IsGameOver)
            {
                _gameOverTitle.text = ResultTitle(game, match);
                _gameOverSubtitle.text = ResultSubtitle(game);
            }
        }

        private void RenderPlayer(PlayerCardView card, PieceColor color, ChessGame game, MatchInfo match,
            ComputerPlayer computer)
        {
            bool isComputer = match.IsVsComputer && color != match.HumanColor;
            string name = isComputer ? (computer != null ? computer.EngineName : "Stockfish")
                : match.IsVsComputer ? "You" : color == PieceColor.White ? "White" : "Black";
            string subtitle = isComputer ? match.Level.name : match.IsVsComputer ? $"Playing {color}" : "Player";
            card.SetPlayer(color, name, subtitle);

            bool toMove = game.SideToMove == color;
            card.SetTurn(toMove, isComputer && computer != null && computer.IsThinking, game.IsGameOver);

            var captured = CapturedBy(game.Position, color, out int advantage);
            card.SetCaptured(captured, advantage);
        }

        /// <summary>
        /// Shows BCI status and the current targets with their slot keys. Pass null when BCI is off.
        /// <paramref name="notice"/> is a standing message (e.g. a setup fallback) shown when nothing more recent is.
        /// </summary>
        public void RenderBci(BciSelectionController bci, string notice = "")
        {
            var slotByTarget = new Dictionary<string, string>();
            _listening = bci != null && bci.Status == BciSessionStatus.AwaitingSelection;
            _listeningPill.SetActive(_listening);
            if (bci == null)
            {
                _bciStatusText.text = "Off - mouse and keyboard only";
                _bciTargetsText.text = string.Empty;
                _bciMessageText.text = string.Empty;
            }
            else
            {
                _bciStatusText.text = BciStatusText(bci);
                _bciMessageText.text = string.IsNullOrEmpty(bci.Message) ? notice : bci.Message;

                var list = new StringBuilder();
                foreach (var target in bci.Targets)
                {
                    string key = FakeBciKeyboardInput.KeyLabel(target.Stimulus.Value.Index);
                    slotByTarget[target.Id] = key;
                    if (list.Length > 0)
                        list.Append("    ");
                    list.Append("<b>").Append(key).Append("</b>  ").Append(target.Label);
                }
                _bciTargetsText.text = list.ToString();
            }

            var pieces = SelectionController.PromotionPieces;
            for (int i = 0; i < _promotionKeyLabels.Count; i++)
            {
                string letter = MoveNotation.PieceLetter(pieces[i]);
                _promotionKeyLabels[i].text = slotByTarget.TryGetValue("promo:" + pieces[i], out var key)
                    ? $"{letter}  ·  BCI {key}"
                    : letter;
            }
            _promotionCancelLabel.text = slotByTarget.TryGetValue("cancel", out var cancelKey)
                ? $"Cancel (Esc · BCI {cancelKey})"
                : "Cancel (Esc)";
        }

        private void Update()
        {
            if (!_listening)
                return;
            // Slow pulse so the player can see at a glance that the BCI is waiting for input.
            var color = _theme.warning;
            color.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            _listeningDot.color = color;
        }

        private static string BciStatusText(BciSelectionController bci)
        {
            switch (bci.Status)
            {
                case BciSessionStatus.Disabled:
                    return $"{bci.Selector.Name} - paused (F3 to resume)";
                case BciSessionStatus.Idle:
                    return $"{bci.Selector.Name} - idle";
                case BciSessionStatus.AwaitingSelection:
                    bool grouped = bci.Targets.Any(t => t.Payload is CandidateGroup);
                    string step = bci.Depth > 0 ? "inside group - choose an option or Back"
                        : grouped ? $"{bci.CandidateCount} options grouped - choose a group"
                        : "choose an option";
                    if (bci.Selector is ISelectionProgress progress && progress.FlashesRequired > 0)
                    {
                        step += progress.FlashesCollected < progress.FlashesRequired
                            ? $" (collecting flashes {progress.FlashesCollected}/{progress.FlashesRequired})"
                            : " (ready to select)";
                    }
                    return $"{bci.Selector.Name}: {step}";
                case BciSessionStatus.TooManyCandidates:
                    return $"{bci.CandidateCount} options do not fit {bci.Capacity} BCI targets, even grouped - use mouse/keyboard";
                case BciSessionStatus.Unavailable:
                    return bci.Selector is IBciStatusProvider provider
                        ? $"{bci.Selector.Name}: {provider.StatusText}"
                        : $"{bci.Selector.Name} - unavailable";
                default:
                    return string.Empty;
            }
        }

        public static string ResultText(ChessGame game)
        {
            switch (game.Status)
            {
                case GameStatus.Checkmate: return $"Checkmate - {game.Winner} wins";
                case GameStatus.Stalemate: return "Draw - stalemate";
                case GameStatus.DrawFiftyMoveRule: return "Draw - fifty-move rule";
                case GameStatus.DrawInsufficientMaterial: return "Draw - insufficient material";
                case GameStatus.DrawThreefoldRepetition: return "Draw - threefold repetition";
                default: return string.Empty;
            }
        }

        private static string ResultTitle(ChessGame game, MatchInfo match)
        {
            if (game.Winner == null)
                return "Draw";
            if (match.IsVsComputer)
                return game.Winner == match.HumanColor ? "You won!" : "You lost";
            return $"{game.Winner} wins";
        }

        private static string ResultSubtitle(ChessGame game)
        {
            switch (game.Status)
            {
                case GameStatus.Checkmate: return $"by checkmate  ·  {game.Winner} wins";
                case GameStatus.Stalemate: return "by stalemate";
                case GameStatus.DrawFiftyMoveRule: return "by the fifty-move rule";
                case GameStatus.DrawInsufficientMaterial: return "by insufficient material";
                case GameStatus.DrawThreefoldRepetition: return "by threefold repetition";
                default: return string.Empty;
            }
        }

        private static string Prompt(ChessGame game, SelectionController selection)
        {
            switch (selection.State)
            {
                case InteractionState.SelectingPiece:
                    return "Select a piece";
                case InteractionState.SelectingDestination:
                    var square = selection.SelectedPiece.Value;
                    return $"Move the {game.Position[square].Type.ToString().ToLowerInvariant()} on {square}";
                case InteractionState.SelectingPromotion:
                    return "Choose a promotion piece";
                case InteractionState.WaitingForPlayer:
                    return "Waiting for player...";
                case InteractionState.ProcessingSelection:
                    return "Processing selection...";
                case InteractionState.ComputerThinking:
                    return "Computer thinking...";
                case InteractionState.GameOver:
                    return ResultText(game);
                default:
                    return string.Empty;
            }
        }

        /// <summary>Opponent pieces missing from the board, cheapest first, and the material lead of <paramref name="color"/>.</summary>
        private static List<Piece> CapturedBy(ChessPosition position, PieceColor color, out int advantage)
        {
            var counts = new Dictionary<Piece, int>();
            int material = 0;
            for (int i = 0; i < 64; i++)
            {
                var piece = position[new Square(i)];
                if (piece.IsNone)
                    continue;
                counts.TryGetValue(piece, out int count);
                counts[piece] = count + 1;
                material += (piece.Color == color ? 1 : -1) * PieceValue(piece.Type);
            }
            advantage = material;

            var captured = new List<Piece>();
            var opponent = color.Opponent();
            foreach (var type in CaptureOrder)
            {
                var piece = new Piece(type, opponent);
                counts.TryGetValue(piece, out int onBoard);
                for (int n = onBoard; n < StartingCount(type); n++)
                    captured.Add(piece);
            }
            return captured;
        }

        private static int StartingCount(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return 8;
                case PieceType.Queen: return 1;
                case PieceType.King: return 1;
                default: return 2;
            }
        }

        private static int PieceValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return 1;
                case PieceType.Knight:
                case PieceType.Bishop: return 3;
                case PieceType.Rook: return 5;
                case PieceType.Queen: return 9;
                default: return 0;
            }
        }

        private void RenderMoves(IReadOnlyList<ChessMove> moves)
        {
            // Pair the moves into numbered rows; a game may start with Black to move.
            var rows = new List<(string white, string black)>();
            for (int i = 0; i < moves.Count; i++)
            {
                string text = MoveNotation.ToLongAlgebraic(moves[i]);
                if (moves[i].Piece.Color == PieceColor.White || rows.Count == 0)
                    rows.Add(moves[i].Piece.Color == PieceColor.White ? (text, "") : ("...", text));
                else
                    rows[rows.Count - 1] = (rows[rows.Count - 1].white, text);
            }

            int first = Mathf.Max(0, rows.Count - MoveRows);
            for (int i = 0; i < MoveRows; i++)
            {
                var row = _moveRows[i];
                int index = first + i;
                bool show = index < rows.Count;
                row.Background.gameObject.SetActive(show);
                if (!show)
                    continue;
                bool isLast = index == rows.Count - 1;
                bool lastIsBlack = isLast && !string.IsNullOrEmpty(rows[index].black);
                row.Number.text = (index + 1) + ".";
                row.White.text = rows[index].white;
                row.Black.text = rows[index].black;
                row.White.color = isLast && !lastIsBlack ? _theme.gold : _theme.text;
                row.Black.color = lastIsBlack ? _theme.gold : _theme.text;
            }
            _noMovesText.enabled = rows.Count == 0;
        }

        /// <summary>"SelectingPiece" -> "SELECTING_PIECE".</summary>
        private static string ToConstantName(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToUpperInvariant(name[i]));
            }
            return sb.ToString();
        }

        private void BuildSidePanel(RectTransform panel)
        {
            UiFactory.AddVerticalLayout(panel, 12f, new RectOffset(30, 30, 26, 24));

            // Header: title + menu button.
            var header = UiFactory.CreateRect("Header", panel);
            UiFactory.AddHorizontalLayout(header, 12f, expandWidth: false);
            UiFactory.AddLayout(header, 50f);
            var title = UiFactory.CreateText("Title", header, "BCI CHESS", 34, _theme.text, TextAnchor.MiddleLeft, UiFactory.Display);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var menu = UiFactory.CreateButton("Menu", header, "Menu", _theme, () => MenuClicked?.Invoke(), 20);
            menu.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;

            _matchText = UiFactory.CreateText("Match", panel, "", 17, _theme.mutedText, TextAnchor.MiddleLeft);
            UiFactory.AddLayout(_matchText, 24f);

            // Status block.
            var status = UiFactory.CreateRounded("Status", panel, _theme.panelRaised, 14f);
            UiFactory.AddLayout(status, 150f);
            UiFactory.AddVerticalLayout(status, 4f, new RectOffset(22, 22, 16, 14));

            var turnRow = UiFactory.CreateRect("TurnRow", status.transform);
            UiFactory.AddLayout(turnRow, 30f);
            _turnDot = UiFactory.CreateImage("Dot", turnRow, Color.white, UiFactory.Circle);
            var dotRect = _turnDot.rectTransform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.sizeDelta = new Vector2(18f, 18f);
            _turnDot.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);
            _turnText = UiFactory.CreateText("Turn", turnRow, "", 20, _theme.mutedText, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.Stretch(_turnText.rectTransform);
            _turnText.rectTransform.offsetMin = new Vector2(28f, 0f);

            var badge = UiFactory.CreateRounded("CheckBadge", turnRow, _theme.warning, 12f);
            var badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 0.5f);
            badgeRect.pivot = new Vector2(1f, 0.5f);
            badgeRect.sizeDelta = new Vector2(92f, 26f);
            var badgeText = UiFactory.CreateText("Text", badge.transform, "CHECK", 15, Color.white, TextAnchor.MiddleCenter, UiFactory.Semibold);
            UiFactory.Stretch(badgeText.rectTransform);
            _checkBadge = badge.gameObject;

            _promptText = UiFactory.CreateText("Prompt", status.transform, "", 30, _theme.text, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.AddLayout(_promptText, 46f);

            _stateText = UiFactory.CreateText("State", status.transform, "", 14, _theme.mutedText, TextAnchor.MiddleLeft);
            UiFactory.AddLayout(_stateText, 20f);
            _engineText = UiFactory.CreateText("Engine", status.transform, "", 15, _theme.warning, TextAnchor.UpperLeft);
            UiFactory.AddLayout(_engineText, 20f);

            // Move list.
            var movesLabel = UiFactory.CreateLabel("MovesLabel", panel, "Moves", _theme);
            UiFactory.AddLayout(movesLabel, 22f);
            var moves = UiFactory.CreateRect("Moves", panel);
            UiFactory.AddLayout(moves, MoveRows * 32f, flexibleHeight: 1f);
            var movesLayout = UiFactory.AddVerticalLayout(moves, 0f);
            movesLayout.childAlignment = TextAnchor.UpperLeft;
            for (int i = 0; i < MoveRows; i++)
                _moveRows.Add(CreateMoveRow(moves, i));
            _noMovesText = UiFactory.CreateText("Empty", moves, "No moves yet", 18, _theme.mutedText, TextAnchor.MiddleLeft);
            _noMovesText.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiFactory.SetAnchors(_noMovesText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f));
            _noMovesText.rectTransform.sizeDelta = new Vector2(-24f, 32f);
            _noMovesText.rectTransform.anchoredPosition = new Vector2(0f, -16f);

            // BCI block.
            var bci = UiFactory.CreateRounded("Bci", panel, _theme.panelRaised, 14f);
            UiFactory.AddLayout(bci, 168f);
            UiFactory.AddVerticalLayout(bci, 4f, new RectOffset(22, 22, 14, 12));

            var bciHeader = UiFactory.CreateRect("Header", bci.transform);
            UiFactory.AddLayout(bciHeader, 26f);
            var bciLabel = UiFactory.CreateLabel("Label", bciHeader, "Brain-computer interface", _theme);
            bciLabel.alignment = TextAnchor.MiddleLeft;
            UiFactory.Stretch(bciLabel.rectTransform);

            var pill = UiFactory.CreateRounded("Listening", bciHeader, new Color(_theme.warning.r, _theme.warning.g, _theme.warning.b, 0.16f), 12f);
            var pillRect = pill.rectTransform;
            pillRect.anchorMin = pillRect.anchorMax = new Vector2(1f, 0.5f);
            pillRect.pivot = new Vector2(1f, 0.5f);
            pillRect.sizeDelta = new Vector2(124f, 26f);
            _listeningDot = UiFactory.CreateImage("Dot", pill.transform, _theme.warning, UiFactory.Circle);
            var listeningDotRect = _listeningDot.rectTransform;
            listeningDotRect.anchorMin = listeningDotRect.anchorMax = new Vector2(0f, 0.5f);
            listeningDotRect.sizeDelta = new Vector2(10f, 10f);
            listeningDotRect.anchoredPosition = new Vector2(16f, 0f);
            var listeningText = UiFactory.CreateText("Text", pill.transform, "LISTENING", 13, _theme.warning, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.Stretch(listeningText.rectTransform);
            listeningText.rectTransform.offsetMin = new Vector2(28f, 0f);
            _listeningPill = pill.gameObject;
            _listeningPill.SetActive(false);

            _bciStatusText = UiFactory.CreateText("Status", bci.transform, "", 17, _theme.accent, TextAnchor.UpperLeft, UiFactory.Semibold);
            UiFactory.AddLayout(_bciStatusText, 46f);
            _bciTargetsText = UiFactory.CreateText("Targets", bci.transform, "", 17, _theme.text, TextAnchor.UpperLeft);
            _bciTargetsText.lineSpacing = 1.1f;
            UiFactory.AddLayout(_bciTargetsText, 46f);
            _bciMessageText = UiFactory.CreateText("Message", bci.transform, "", 15, _theme.gold, TextAnchor.UpperLeft);
            UiFactory.AddLayout(_bciMessageText, 20f);

            // Buttons.
            var buttonRow = UiFactory.CreateRect("Buttons", panel);
            UiFactory.AddHorizontalLayout(buttonRow, 12f);
            UiFactory.AddLayout(buttonRow, 54f);
            UiFactory.CreateButton("NewGame", buttonRow, "New game", _theme, () => NewGameClicked?.Invoke(), 20);
            _undoButton = UiFactory.CreateButton("Undo", buttonRow, "Undo", _theme, () => UndoClicked?.Invoke(), 20);
            UiFactory.CreateButton("Flip", buttonRow, "Flip board", _theme, () => FlipClicked?.Invoke(), 20);

            var help = UiFactory.CreateText("Help", panel,
                "Click a piece, then its destination  ·  right-click or Esc cancels\n" +
                "Arrows + Enter move the cursor  ·  Backspace undo  ·  F flip  ·  F3 BCI on/off",
                14, _theme.mutedText, TextAnchor.LowerLeft);
            help.lineSpacing = 1.15f;
            UiFactory.AddLayout(help, 40f);
        }

        private MoveRow CreateMoveRow(Transform parent, int index)
        {
            var background = UiFactory.CreateRounded("Row" + index, parent,
                index % 2 == 0 ? new Color(1f, 1f, 1f, 0.035f) : new Color(1f, 1f, 1f, 0f), 6f);
            UiFactory.AddLayout(background, 32f);

            var number = UiFactory.CreateText("Number", background.transform, "", 17, _theme.mutedText, TextAnchor.MiddleLeft);
            UiFactory.SetAnchors(number.rectTransform, new Vector2(0f, 0f), new Vector2(0.14f, 1f));
            number.rectTransform.offsetMin = new Vector2(12f, 0f);
            var white = UiFactory.CreateText("White", background.transform, "", 18, _theme.text, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.SetAnchors(white.rectTransform, new Vector2(0.14f, 0f), new Vector2(0.57f, 1f));
            var black = UiFactory.CreateText("Black", background.transform, "", 18, _theme.text, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.SetAnchors(black.rectTransform, new Vector2(0.57f, 0f), new Vector2(1f, 1f));
            return new MoveRow { Background = background, Number = number, White = white, Black = black };
        }

        private void BuildGameOverDialog(RectTransform boardFrame)
        {
            var overlay = UiFactory.CreateImage("GameOver", boardFrame, new Color(0f, 0f, 0f, 0.45f), raycastTarget: true);
            UiFactory.Stretch(overlay.rectTransform);
            _gameOverOverlay = overlay.gameObject;

            var card = UiFactory.CreateCard("Dialog", overlay.transform, _theme.panel, 20f, 0.6f);
            UiFactory.Place(card.Root, Vector2.zero, new Vector2(540f, 300f));

            _gameOverTitle = UiFactory.CreateText("Title", card.Body.transform, "", 48, _theme.text, TextAnchor.MiddleCenter, UiFactory.Display);
            UiFactory.SetAnchors(_gameOverTitle.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.92f));
            _gameOverSubtitle = UiFactory.CreateText("Subtitle", card.Body.transform, "", 22, _theme.mutedText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchors(_gameOverSubtitle.rectTransform, new Vector2(0f, 0.47f), new Vector2(1f, 0.62f));

            var rematch = UiFactory.CreateButton("Rematch", card.Body.transform, "Rematch", _theme,
                () => NewGameClicked?.Invoke(), 22, ButtonStyle.Primary);
            UiFactory.Place((RectTransform)rematch.transform, new Vector2(-118f, -50f), new Vector2(220f, 58f));
            var menu = UiFactory.CreateButton("Menu", card.Body.transform, "Main menu", _theme,
                () => MenuClicked?.Invoke(), 22);
            UiFactory.Place((RectTransform)menu.transform, new Vector2(118f, -50f), new Vector2(220f, 58f));
            var view = UiFactory.CreateButton("ViewBoard", card.Body.transform, "View board", _theme, () =>
            {
                _gameOverDismissed = true;
                _gameOverOverlay.SetActive(false);
            }, 17, ButtonStyle.Ghost);
            UiFactory.Place((RectTransform)view.transform, new Vector2(0f, -116f), new Vector2(180f, 36f));

            _gameOverOverlay.SetActive(false);
        }

        private void BuildPromotionOverlay(RectTransform canvasRoot, RectTransform boardFrame, PieceSet set)
        {
            // Full-screen dimmer blocks clicks on the board while choosing.
            var dimmer = UiFactory.CreateImage("PromotionOverlay", canvasRoot, new Color(0f, 0f, 0f, 0.5f),
                raycastTarget: true);
            UiFactory.Stretch(dimmer.rectTransform);
            _promotionOverlay = dimmer.gameObject;

            var card = UiFactory.CreateCard("Dialog", dimmer.transform, _theme.panel, 20f, 0.6f);
            UiFactory.Place(card.Root, boardFrame.anchoredPosition, new Vector2(660f, 350f));
            var box = card.Body.transform;

            var title = UiFactory.CreateText("Title", box, "Promote pawn to", 30, _theme.text,
                TextAnchor.MiddleCenter, UiFactory.Semibold);
            UiFactory.SetAnchors(title.rectTransform, new Vector2(0f, 0.8f), new Vector2(1f, 0.96f));

            var pieces = SelectionController.PromotionPieces;
            const float buttonSize = 124f;
            const float spacing = 24f;
            float totalWidth = pieces.Count * buttonSize + (pieces.Count - 1) * spacing;
            for (int i = 0; i < pieces.Count; i++)
            {
                var type = pieces[i];
                var tile = UiFactory.CreateRounded(type.ToString(), box, Color.white, 12f, raycastTarget: true);
                var button = tile.gameObject.AddComponent<Button>();
                UiFactory.DisableKeyboardSubmit(button);
                button.onClick.AddListener(() => UiFactory.ButtonClickFeedback?.Invoke());
                button.onClick.AddListener(() => PromotionChosen?.Invoke(type));
                float x = -totalWidth / 2f + buttonSize / 2f + i * (buttonSize + spacing);
                UiFactory.Place((RectTransform)button.transform, new Vector2(x, 18f), new Vector2(buttonSize, buttonSize));
                _promotionButtons[type] = (RectTransform)button.transform;
                _promotionTiles.Add(tile);

                var pieceRect = UiFactory.CreateRect("Piece", tile.transform);
                UiFactory.Stretch(pieceRect, 8f);
                var piece = pieceRect.gameObject.AddComponent<ChessPieceView>();
                piece.Build(_theme, _glyphFont, set);
                _promotionPieces.Add(piece);

                var key = UiFactory.CreateText("Key", box, MoveNotation.PieceLetter(type), 18,
                    _theme.mutedText, TextAnchor.MiddleCenter, UiFactory.Semibold);
                key.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Place(key.rectTransform, new Vector2(x, 18f - buttonSize / 2f - 20f), new Vector2(buttonSize, 28f));
                _promotionKeyLabels.Add(key);
            }

            var cancel = UiFactory.CreateButton("Cancel", box, "Cancel (Esc)", _theme,
                () => PromotionCancelled?.Invoke(), 20);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(0f, -132f), new Vector2(300f, 48f));
            _promotionCancelLabel = cancel.GetComponentInChildren<Text>();
            _promotionCancelButton = (RectTransform)cancel.transform;

            _promotionOverlay.SetActive(false);
        }
    }
}
