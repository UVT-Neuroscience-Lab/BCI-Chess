using System;
using System.Threading.Tasks;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Engine;
using BciChess.Interaction;
using BciChess.Unicorn;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Scene entry point: creates the chess game, the selection pipeline, the BCI, the computer opponent, the UI,
    /// sound and the lobby, and wires them together. The lobby is shown first; a game starts from there.
    /// </summary>
    public sealed class ChessGameBootstrap : MonoBehaviour
    {
        [Tooltip("Interface colours. Board colours and piece sets are chosen in the lobby.")]
        [SerializeField] private BoardTheme uiTheme = new BoardTheme();

        [Tooltip("Optional FEN to start from. Leave empty for the standard starting position.")]
        [SerializeField] private string startFen = "";

        [SerializeField] private BciSettings bci = new BciSettings();

        [SerializeField] private ComputerSettings computer = new ComputerSettings();

        [Tooltip("Seconds to wait after a move before turning the board in local 2-player games.")]
        [Min(0f)] [SerializeField] private float autoFlipDelaySeconds = 0.45f;

        private GamePreferences _preferences;
        private ChessGame _game;
        private SelectionController _selection;
        private BoardView _board;
        private GameHudView _hud;
        private MainMenuView _menu;
        private GameAudio _audio;
        private KeyboardBoardInput _keyboard;
        private BciSelectionController _bci;
        private BciCommandBar _commandBar;
        private ComputerPlayer _computer;
        private IChessEngine _engine;
        private DifficultyLevel _engineLevel;
        private IChessEngine _fallbackEngine;
        private UnicornBciRig _unicorn;
        private RectTransform _canvasRoot;
        private MatchInfo _match;
        private string _bciNotice = string.Empty;
        private bool _bciWanted;
        private bool _gameStarted;
        private bool _moveJustMade;
        private float _flipAt = -1f;
        private int _lastFlashProgress = -1;

        private const int GameCanvasSortingOrder = 0;

        public ChessGame Game => _game;
        public SelectionController Selection => _selection;

        private void Awake()
        {
            _preferences = GamePreferences.Load();
            _game = new ChessGame(ResolveStartFen());
            _selection = new SelectionController(_game);

            _audio = gameObject.AddComponent<GameAudio>();
            _audio.Initialize(_preferences);
            UiFactory.ButtonClickFeedback = () => _audio.Play(GameSound.Click);

            BuildUi();
            _match = CreateMatch(PieceColor.White);

            _selection.StateChanged += Render;
            _keyboard.CursorChanged += Render;
            _game.MoveMade += OnMoveMade;
            _game.PositionChanged += OnPositionChanged;

            _board.SquareClicked += square => _selection.SelectSquare(square);
            _board.CancelRequested += _selection.Cancel;

            _hud.NewGameClicked += StartGame;
            _hud.UndoClicked += Undo;
            _hud.FlipClicked += Flip;
            _hud.MenuClicked += OpenMenu;
            _hud.PromotionChosen += type => _selection.SelectPromotion(type);
            _hud.PromotionCancelled += _selection.Cancel;

            _keyboard.NewGameRequested += StartGame;
            _keyboard.UndoRequested += Undo;
            _keyboard.FlipRequested += Flip;

            _menu.StartRequested += StartGame;
            _menu.ResumeRequested += Resume;
            _menu.AppearanceChanged += ApplyAppearance;

            SetupBci();
            _menu.SetInputDescription(InputDescription());
            ApplyAppearance();
            OpenMenu();
        }

        private void Update()
        {
            if (_flipAt >= 0f && Time.unscaledTime >= _flipAt)
            {
                _flipAt = -1f;
                FlipToSideToMove();
            }

            if (_bci == null)
                return;
            if (Input.GetKeyDown(KeyCode.F3) && !_menu.IsOpen)
            {
                _bciWanted = !_bciWanted;
                _bci.Enabled = _bciWanted;
            }
            _bci.Tick(Time.deltaTime);

            // Flash progress changes with every round; refresh only the BCI status when it does.
            if (_bci.Selector is ISelectionProgress progress && progress.FlashesCollected != _lastFlashProgress)
            {
                _lastFlashProgress = progress.FlashesCollected;
                _hud.RenderBci(_bci, _bciNotice);
            }
        }

        private void OnDestroy()
        {
            UiFactory.ButtonClickFeedback = null;
            _computer?.Dispose();
            _engine?.Dispose();
            _fallbackEngine?.Dispose();
            _bci?.Dispose();
            _selection?.Dispose();
        }

        // ---------------------------------------------------------------- game flow

        /// <summary>Starts a new game with the lobby settings (also used for "New game" and "Rematch").</summary>
        private void StartGame()
        {
            _menu.Close();
            _computer?.Dispose();
            _computer = null;
            _flipAt = -1f;

            var humanColor = _preferences.playAs == SideChoice.Black ? PieceColor.Black
                : _preferences.playAs == SideChoice.Random ? (UnityEngine.Random.value < 0.5f ? PieceColor.White : PieceColor.Black)
                : PieceColor.White;
            _match = CreateMatch(humanColor);

            _board.StopAnimations();
            _game.Reset(ResolveStartFen());

            if (_match.IsVsComputer)
            {
                var computerColor = humanColor.Opponent();
                _computer = new ComputerPlayer(_game, GetEngine(_match.Level), computerColor, _fallbackEngine,
                    computer.minimumThinkSeconds);
                _computer.StateChanged += Render;
                _selection.ComputerSide = computerColor;
                // Keep the human's pieces at the bottom.
                _board.Flipped = humanColor == PieceColor.Black;
            }
            else
            {
                _selection.ComputerSide = null;
                _board.Flipped = false;
            }

            _gameStarted = true;
            _keyboard.enabled = true;
            if (_bci != null)
                _bci.Enabled = _bciWanted;
            _audio.Play(GameSound.GameStart);
            Render();

            // Last, because the computer may move immediately when it plays White.
            if (_computer != null)
                _computer.Enabled = true;
        }

        /// <summary>Shows the lobby and pauses the game (BCI, computer, keyboard).</summary>
        private void OpenMenu()
        {
            _menu.Open(canResume: _gameStarted && !_game.IsGameOver);
            _keyboard.enabled = false;
            if (_bci != null)
                _bci.Enabled = false;
            if (_computer != null)
                _computer.Enabled = false;
        }

        private void Resume()
        {
            _menu.Close();
            _keyboard.enabled = true;
            if (_bci != null)
                _bci.Enabled = _bciWanted;
            if (_computer != null)
                _computer.Enabled = true;
            Render();
        }

        private MatchInfo CreateMatch(PieceColor humanColor) => new MatchInfo
        {
            Mode = _preferences.mode,
            HumanColor = humanColor,
            Level = computer.GetLevel(_preferences.difficulty)
        };

        /// <summary>Takes back the last move; against the computer, back to the human's previous turn.</summary>
        private void Undo()
        {
            if (!_game.Undo() || _computer == null)
                return;
            while (_game.SideToMove == _computer.Color && _game.MoveHistory.Count > 0)
                _game.Undo();
        }

        private void Flip()
        {
            _board.Flipped = !_board.Flipped;
            Render();
        }

        private void OnMoveMade(ChessMove move)
        {
            _moveJustMade = true;
            if (_preferences.animateMoves)
                _board.AnimateMove(move);
            _audio.PlayMove(_game, move, _match.IsVsComputer ? _match.HumanColor : (PieceColor?)null);
        }

        private void OnPositionChanged()
        {
            bool afterMove = _moveJustMade;
            _moveJustMade = false;
            if (!afterMove)
                _board.StopAnimations();

            if (_match.Mode == GameMode.LocalTwoPlayer && _preferences.autoFlip && _gameStarted)
            {
                // After a move, let the piece land before turning the board; after undo/reset turn at once.
                if (afterMove)
                    _flipAt = Time.unscaledTime + autoFlipDelaySeconds;
                else
                    FlipToSideToMove();
            }
        }

        private void FlipToSideToMove()
        {
            if (_game.IsGameOver)
                return;
            bool flipped = _game.SideToMove == PieceColor.Black;
            if (_board.Flipped == flipped)
                return;
            _board.Flipped = flipped;
            Render();
        }

        private void ApplyAppearance()
        {
            _board.ApplyAppearance(_preferences);
            _hud.ApplyAppearance(_preferences);
            Render();
        }

        // ---------------------------------------------------------------- computer

        /// <summary>Stockfish configured for <paramref name="level"/>; restarted only when the level changes.</summary>
        private IChessEngine GetEngine(DifficultyLevel level)
        {
            if (_fallbackEngine == null && computer.useFallbackOpponent)
                _fallbackEngine = new SimpleEngine();
            if (_engine != null && _engineLevel == level)
                return _engine;

            _engine?.Dispose();
            var stockfish = new StockfishClient(new StockfishOptions
            {
                ExecutablePath = computer.ResolveStockfishPath(),
                SkillLevel = level.skillLevel,
                MoveTimeMs = level.moveTimeMs,
                Depth = level.searchDepth
            });
            stockfish.Diagnostic += message => Debug.Log(message);
            _engine = stockfish;
            _engineLevel = level;
            _ = WarmUpAsync(stockfish);
            return _engine;
        }

        /// <summary>Starts Stockfish in the background so the first move is quick and a missing binary is reported early.</summary>
        private async Task WarmUpAsync(StockfishClient stockfish)
        {
            try
            {
                await stockfish.StartAsync();
                Debug.Log($"Stockfish ready ({computer.ResolveStockfishPath()}).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Stockfish unavailable: {e.Message}" +
                                 (computer.useFallbackOpponent ? " The built-in opponent will be used." : ""));
            }
        }

        // ---------------------------------------------------------------- BCI

        private void SetupBci()
        {
            if (bci.mode == BciMode.Off)
                return;

            StimulusManager stimuli;
            try
            {
                stimuli = new StimulusManager(bci.stimulusClassIds ?? Array.Empty<int>(), bci.maxSimultaneousTargets);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"BCI disabled: invalid stimulus configuration. {e.Message}", this);
                return;
            }

            IBciSelector selector = null;
            IStimulusSource stimulusSource = null;
            if (bci.mode == BciMode.Unicorn)
            {
                try
                {
                    // The g.tec UI (connect, signal quality, training) is drawn above the game UI.
                    _unicorn = UnicornBciRig.Create(bci.unicorn, stimuli.Slots, GameCanvasSortingOrder + 10,
                        bci.minimumFlashesBeforeSelection);
                    selector = _unicorn.Selector;
                    stimulusSource = _unicorn.Selector;
                    _unicorn.StateChanged += Render;
                    gameObject.AddComponent<UnicornKeypadOverride>().Initialize(_unicorn.Selector);
                    gameObject.AddComponent<BciCalibrationView>().Build(_canvasRoot, uiTheme, bci.visuals, _unicorn);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unicorn BCI setup failed: {e.Message}", this);
                    if (!bci.unicorn.fallBackToSimulated)
                        return;
                    _bciNotice = "Unicorn setup failed - using the simulated BCI. See the Console for details.";
                }
            }

            if (selector == null)
            {
                // The simulated flashes come from a local sequencer; the fake selector counts them so the
                // minimum-flash rule behaves as with the headset.
                var sequencer = new FlashSequencer(bci.visuals.flashOnTimeMs, bci.visuals.flashOffTimeMs);
                var fake = new FakeBciSelector(bci.minimumFlashesBeforeSelection);
                fake.AttachStimulusSource(sequencer);
                gameObject.AddComponent<FakeBciKeyboardInput>().Initialize(fake);
                selector = fake;
                stimulusSource = sequencer;
            }

            var options = new BciSelectionOptions
            {
                SelectionTimeoutSeconds = bci.selectionTimeoutSeconds,
                OfferCancelTarget = bci.offerCancelTarget,
                AutoSelectSingleCandidate = bci.autoSelectSingleCandidate,
                NeighbourDistance = bci.neighbourDistance,
                DestinationNeighbourDistance = bci.destinationNeighbourDistance,
                SlotDistance = bci.visuals.SlotColorDistance
            };
            _bci = new BciSelectionController(_selection, selector, stimuli, options);
            _bci.Changed += Render;
            _bci.TargetChosen += _ => _audio.Play(GameSound.BciSelect);
            gameObject.AddComponent<BciStimulusPresenter>()
                .Initialize(_bci, bci.visuals, _board, _hud, _commandBar, stimulusSource, bci.neighbourFlashSpacing);
            // Stays off while the lobby is open; a started game turns it on.
            _bciWanted = bci.enabledOnStart;
            _bci.Enabled = false;
        }

        private string InputDescription()
        {
            if (_bci == null)
                return "mouse & keyboard";
            return _unicorn != null ? "Unicorn Hybrid Black EEG headset" : "mouse, keyboard & simulated BCI";
        }

        // ---------------------------------------------------------------- presentation

        private void Render()
        {
            _board.Render(_game, _selection, _keyboard.CursorVisible ? _keyboard.Cursor : (Square?)null);
            _hud.Render(_game, _selection);
            _hud.RenderMatch(_game, _match, _computer, _board.Flipped);
            _hud.RenderBci(_bci, _bciNotice);
        }

        private string ResolveStartFen()
        {
            if (string.IsNullOrWhiteSpace(startFen))
                return ChessPosition.StartFen;
            try
            {
                ChessPosition.FromFen(startFen);
                return startFen;
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"Invalid start FEN, using the standard position instead. {e.Message}", this);
                return ChessPosition.StartFen;
            }
        }

        private void BuildUi()
        {
            EnsureEventSystem();
            var theme = uiTheme;
            var glyphFont = UiFactory.CreateGlyphFont(theme);
            var pieceSet = _preferences.PieceSet;

            var canvasObject = new GameObject("ChessCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GameCanvasSortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasObject.transform;
            _canvasRoot = root;

            UiFactory.CreateGradient("Background", root, theme.background, theme.backgroundBottom);

            const float boardX = -230f;
            var frame = UiFactory.CreateCard("BoardFrame", root, theme.boardFrame, 16f, 0.6f);
            UiFactory.Place(frame.Root, new Vector2(boardX, 0f), new Vector2(832f, 832f));
            var boardRect = UiFactory.CreateRect("Board", frame.Body.transform);
            UiFactory.Stretch(boardRect, 16f);
            _board = boardRect.gameObject.AddComponent<BoardView>();
            _board.Build(theme, glyphFont, pieceSet);

            var topPlayer = CreatePlayerCard("TopPlayer", root, new Vector2(boardX, 462f), theme, glyphFont, pieceSet);
            var bottomPlayer = CreatePlayerCard("BottomPlayer", root, new Vector2(boardX, -462f), theme, glyphFont, pieceSet);

            // Off-board BCI targets (Cancel, Back) live in a column left of the board.
            var commandBarRect = UiFactory.CreateRect("BciCommandBar", root);
            UiFactory.Place(commandBarRect, new Vector2(-792f, 0f), new Vector2(220f, 300f));
            _commandBar = commandBarRect.gameObject.AddComponent<BciCommandBar>();
            _commandBar.Build(theme, bci.visuals);

            var panel = UiFactory.CreateCard("SidePanel", root, theme.panel, 22f);
            UiFactory.Place(panel.Root, new Vector2(590f, 0f), new Vector2(600f, 988f));

            _hud = canvasObject.AddComponent<GameHudView>();
            _hud.Build(root, panel.BodyRect, frame.Root, topPlayer, bottomPlayer, theme, glyphFont, pieceSet);

            _keyboard = gameObject.AddComponent<KeyboardBoardInput>();
            _keyboard.Initialize(_selection, _board);

            // Built last so it covers the game; the BCI calibration view is added later and covers the menu.
            var menuRect = UiFactory.CreateRect("MainMenu", root);
            _menu = menuRect.gameObject.AddComponent<MainMenuView>();
            _menu.Build(root, theme, _preferences, computer, _audio, glyphFont);
        }

        private static PlayerCardView CreatePlayerCard(string name, RectTransform root, Vector2 position,
            BoardTheme theme, Font glyphFont, PieceSet set)
        {
            var rect = UiFactory.CreateRect(name, root);
            UiFactory.Place(rect, position, new Vector2(832f, 68f));
            var card = rect.gameObject.AddComponent<PlayerCardView>();
            card.Build(theme, glyphFont, set);
            return card;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
