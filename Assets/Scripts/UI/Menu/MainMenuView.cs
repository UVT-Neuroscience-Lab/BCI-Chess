using System;
using System.Collections.Generic;
using System.Linq;
using BciChess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Lobby shown over the game: game mode and computer strength, board and piece appearance, and sound.
    /// Edits <see cref="GamePreferences"/> in place (saved on every change); appearance and sound apply immediately.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private const float ColumnX = -440f;
        private const float ColumnWidth = 900f;

        private BoardTheme _theme;
        private GamePreferences _preferences;
        private ComputerSettings _computer;
        private GameAudio _audio;
        private Font _glyphFont;

        private readonly List<GameObject> _tabPages = new List<GameObject>();
        private GameObject _vsComputerSection;
        private GameObject _localSection;
        private Text _difficultySummary;
        private Button _resumeButton;
        private Text _startLabel;
        private BoardPreviewView _preview;
        private Text _previewCaption;
        private ChoiceGroup _schemes;
        private ChoiceGroup _pieceSets;
        private readonly List<ChessPieceView> _pieceSetIcons = new List<ChessPieceView>();
        private Text _volumeText;
        private Text _inputText;

        /// <summary>Start a new game with the current preferences.</summary>
        public event Action StartRequested;

        /// <summary>Close the menu and continue the current game.</summary>
        public event Action ResumeRequested;

        /// <summary>Board, piece or display preferences changed.</summary>
        public event Action AppearanceChanged;

        public bool IsOpen => gameObject.activeSelf;
        public bool CanResume { get; private set; }

        public void Build(RectTransform canvasRoot, BoardTheme theme, GamePreferences preferences,
            ComputerSettings computer, GameAudio audio, Font glyphFont)
        {
            _theme = theme;
            _preferences = preferences;
            _computer = computer;
            _audio = audio;
            _glyphFont = glyphFont;

            var root = (RectTransform)transform;
            root.SetParent(canvasRoot, false);
            UiFactory.Stretch(root);
            var background = UiFactory.CreateGradient("Background", root, theme.background, theme.backgroundBottom);
            background.raycastTarget = true;
            BuildDecoration(root);

            BuildHeader(root);

            var tabRow = UiFactory.CreateRect("Tabs", root);
            PlaceLeft(tabRow, 312f, 56f);
            UiFactory.AddHorizontalLayout(tabRow, 10f);
            ChoiceGroup.Create(tabRow, theme, new[] { "Play", "Board & pieces", "Sound" }, 0, ShowTab, 21);

            var card = UiFactory.CreateCard("Content", root, theme.panel, 22f);
            PlaceLeft(card.Root, -110f, 760f);
            _tabPages.Add(BuildPlayPage(card.BodyRect));
            _tabPages.Add(BuildAppearancePage(card.BodyRect));
            _tabPages.Add(BuildSoundPage(card.BodyRect));
            ShowTab(0);

            BuildPreview(root);
            RefreshPlay();
            RefreshAppearance();
        }

        public void Open(bool canResume)
        {
            CanResume = canResume;
            _resumeButton.gameObject.SetActive(canResume);
            _startLabel.text = canResume ? "New game" : "Start game";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Shows how the game is controlled (mouse, simulated BCI, headset).</summary>
        public void SetInputDescription(string description) => _inputText.text = "Input:  " + description;

        private void Update()
        {
            if (CanResume && Input.GetKeyDown(KeyCode.Escape))
                ResumeRequested?.Invoke();
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Count; i++)
                _tabPages[i].SetActive(i == index);
        }

        private void Changed()
        {
            _preferences.Save();
        }

        // ---------------------------------------------------------------- header & preview

        private void BuildHeader(RectTransform root)
        {
            var header = UiFactory.CreateRect("Header", root);
            PlaceLeft(header, 425f, 120f);

            var emblem = UiFactory.CreateRounded("Emblem", header, _theme.primary, 22f);
            var emblemRect = emblem.rectTransform;
            emblemRect.anchorMin = emblemRect.anchorMax = new Vector2(0f, 0.5f);
            emblemRect.pivot = new Vector2(0f, 0.5f);
            emblemRect.sizeDelta = new Vector2(104f, 104f);
            var knightRect = UiFactory.CreateRect("Knight", emblemRect);
            UiFactory.Stretch(knightRect, 12f);
            var knight = knightRect.gameObject.AddComponent<ChessPieceView>();
            knight.Build(_theme, _glyphFont, PieceSet.Find("cburnett"));
            knight.SetPiece(new Piece(PieceType.Knight, PieceColor.White));

            var title = UiFactory.CreateText("Title", header, "BCI CHESS", 72, _theme.text, TextAnchor.LowerLeft, UiFactory.Display);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetAnchors(title.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1.05f));
            title.rectTransform.offsetMin = new Vector2(132f, 0f);

            var tagline = UiFactory.CreateText("Tagline", header,
                "Play chess with your mind  ·  Unicorn Hybrid Black EEG", 22, _theme.mutedText, TextAnchor.UpperLeft);
            UiFactory.SetAnchors(tagline.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.36f));
            tagline.rectTransform.offsetMin = new Vector2(134f, 0f);
        }

        /// <summary>Faint oversized checkerboard behind the preview, for depth.</summary>
        private void BuildDecoration(RectTransform root)
        {
            var pattern = UiFactory.CreateRect("Pattern", root);
            UiFactory.Place(pattern, new Vector2(480f, 40f), new Vector2(1200f, 1200f));
            pattern.localRotation = Quaternion.Euler(0f, 0f, 12f);
            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    if (((file + rank) & 1) == 0)
                        continue;
                    var square = UiFactory.CreateImage("Square", pattern, new Color(1f, 1f, 1f, 0.018f));
                    UiFactory.SetAnchors(square.rectTransform, new Vector2(file / 8f, rank / 8f),
                        new Vector2((file + 1) / 8f, (rank + 1) / 8f));
                }
            }
        }

        private void BuildPreview(RectTransform root)
        {
            var card = UiFactory.CreateCard("Preview", root, _theme.boardFrame, 18f, 0.7f);
            UiFactory.Place(card.Root, new Vector2(480f, 40f), new Vector2(680f, 680f));
            var boardRect = UiFactory.CreateRect("Board", card.Body.transform);
            UiFactory.Stretch(boardRect, 14f);
            _preview = boardRect.gameObject.AddComponent<BoardPreviewView>();
            _preview.Build(_theme, _glyphFont, _preferences.PieceSet);

            _previewCaption = UiFactory.CreateText("Caption", root, "", 20, _theme.mutedText, TextAnchor.MiddleCenter, UiFactory.Semibold);
            UiFactory.Place(_previewCaption.rectTransform, new Vector2(480f, -340f), new Vector2(680f, 30f));

            _inputText = UiFactory.CreateText("Input", root, "", 18, _theme.mutedText,
                TextAnchor.MiddleCenter);
            UiFactory.Place(_inputText.rectTransform, new Vector2(480f, -380f), new Vector2(680f, 28f));
        }

        // ---------------------------------------------------------------- play

        private GameObject BuildPlayPage(RectTransform card)
        {
            var page = CreatePage(card, "Play");

            UiFactory.AddLayout(UiFactory.CreateLabel("ModeLabel", page, "Game mode", _theme), 24f);
            var modeRow = UiFactory.CreateRect("Mode", page);
            UiFactory.AddHorizontalLayout(modeRow, 16f);
            UiFactory.AddLayout(modeRow, 118f);
            var modes = ChoiceGroup.Create(modeRow, _theme, new[] { "", "" }, (int)_preferences.mode, index =>
            {
                _preferences.mode = (GameMode)index;
                Changed();
                RefreshPlay();
            });
            AddModeText(modes.GetChip(0), "vs Computer", "Play against Stockfish");
            AddModeText(modes.GetChip(1), "Local 2-Player", "Two players, one board - play both colours");

            // vs computer: difficulty and colour.
            var vsComputer = UiFactory.CreateRect("VsComputer", page);
            UiFactory.AddVerticalLayout(vsComputer, 12f);
            _vsComputerSection = vsComputer.gameObject;

            UiFactory.AddLayout(UiFactory.CreateLabel("DifficultyLabel", vsComputer, "Difficulty", _theme), 34f);
            var levels = UiFactory.CreateRect("Levels", vsComputer);
            UiFactory.AddHorizontalLayout(levels, 10f);
            UiFactory.AddLayout(levels, 54f);
            var names = (_computer.difficultyLevels ?? Array.Empty<DifficultyLevel>()).Select(l => l.name).ToArray();
            _preferences.difficulty = Mathf.Clamp(_preferences.difficulty, 0, Mathf.Max(0, names.Length - 1));
            ChoiceGroup.Create(levels, _theme, names, _preferences.difficulty, index =>
            {
                _preferences.difficulty = index;
                Changed();
                RefreshPlay();
            }, 18);
            _difficultySummary = UiFactory.CreateText("Summary", vsComputer, "", 17, _theme.mutedText, TextAnchor.MiddleLeft);
            UiFactory.AddLayout(_difficultySummary, 24f);

            UiFactory.AddLayout(UiFactory.CreateLabel("SideLabel", vsComputer, "Play as", _theme), 34f);
            var sides = UiFactory.CreateRect("Sides", vsComputer);
            UiFactory.AddHorizontalLayout(sides, 10f);
            UiFactory.AddLayout(sides, 54f);
            ChoiceGroup.Create(sides, _theme, new[] { "White", "Random", "Black" }, (int)_preferences.playAs, index =>
            {
                _preferences.playAs = (SideChoice)index;
                Changed();
            });

            // Local 2-player options.
            var local = UiFactory.CreateRect("Local", page);
            UiFactory.AddVerticalLayout(local, 12f);
            _localSection = local.gameObject;
            UiFactory.AddLayout(UiFactory.CreateLabel("OptionsLabel", local, "Options", _theme), 34f);
            var autoFlip = UiSwitch.Create("Rotate the board after every move", local, _theme, _preferences.autoFlip, on =>
            {
                _preferences.autoFlip = on;
                Changed();
            });
            UiFactory.AddLayout(autoFlip, 48f);
            var note = UiFactory.CreateText("Note", local,
                "Both players share the mouse, keyboard or BCI headset. Undo takes back one move.",
                18, _theme.mutedText, TextAnchor.UpperLeft);
            UiFactory.AddLayout(note, 50f);

            var spacer = UiFactory.CreateRect("Spacer", page);
            UiFactory.AddLayout(spacer, 0f, flexibleHeight: 1f);

            var buttons = UiFactory.CreateRect("Buttons", page);
            UiFactory.AddHorizontalLayout(buttons, 14f, expandWidth: false);
            UiFactory.AddLayout(buttons, 72f);
            var start = UiFactory.CreateButton("Start", buttons, "Start game", _theme, () => StartRequested?.Invoke(),
                28, ButtonStyle.Primary);
            start.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _startLabel = start.GetComponentInChildren<Text>();
            _resumeButton = UiFactory.CreateButton("Resume", buttons, "Resume", _theme, () => ResumeRequested?.Invoke(), 24);
            _resumeButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 240f;
            return page.gameObject;
        }

        private void AddModeText(Image chip, string title, string description)
        {
            var titleText = UiFactory.CreateText("Title", chip.transform, title, 28, _theme.text, TextAnchor.LowerLeft, UiFactory.Semibold);
            UiFactory.SetAnchors(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.9f));
            titleText.rectTransform.offsetMin = new Vector2(26f, 0f);
            var descriptionText = UiFactory.CreateText("Description", chip.transform, description, 17, _theme.mutedText, TextAnchor.UpperLeft);
            UiFactory.SetAnchors(descriptionText.rectTransform, new Vector2(0f, 0.1f), new Vector2(1f, 0.48f));
            descriptionText.rectTransform.offsetMin = new Vector2(26f, 0f);
        }

        private void RefreshPlay()
        {
            bool vsComputer = _preferences.mode == GameMode.VsComputer;
            _vsComputerSection.SetActive(vsComputer);
            _localSection.SetActive(!vsComputer);
            _difficultySummary.text = _computer.GetLevel(_preferences.difficulty).Summary;
        }

        // ---------------------------------------------------------------- appearance

        private GameObject BuildAppearancePage(RectTransform card)
        {
            var page = CreatePage(card, "Appearance");

            UiFactory.AddLayout(UiFactory.CreateLabel("BoardLabel", page, "Board", _theme), 24f);
            var schemeGrid = CreateGrid(page, new Vector2(156f, 80f), 5, 10f);
            var schemes = BoardColorScheme.All;
            _schemes = ChoiceGroup.Create(schemeGrid.transform, _theme, schemes.Select(_ => "").ToArray(),
                IndexOf(schemes, s => s.Id == _preferences.boardScheme), index =>
                {
                    _preferences.boardScheme = schemes[index].Id;
                    OnAppearanceChanged();
                });
            for (int i = 0; i < schemes.Count; i++)
                AddSchemeSwatch(_schemes.GetChip(i), schemes[i]);
            UiFactory.AddLayout(schemeGrid, 170f);

            UiFactory.AddLayout(UiFactory.CreateLabel("PiecesLabel", page, "Pieces", _theme), 30f);
            var setGrid = CreateGrid(page, new Vector2(196f, 92f), 4, 12f);
            var sets = PieceSet.All;
            _pieceSets = ChoiceGroup.Create(setGrid.transform, _theme, sets.Select(_ => "").ToArray(),
                IndexOf(sets, s => s.Id == _preferences.pieceSet), index =>
                {
                    _preferences.pieceSet = sets[index].Id;
                    OnAppearanceChanged();
                });
            for (int i = 0; i < sets.Count; i++)
                AddPieceSetSample(_pieceSets.GetChip(i), sets[i]);
            UiFactory.AddLayout(setGrid, 196f);

            UiFactory.AddLayout(UiFactory.CreateLabel("DisplayLabel", page, "Display", _theme), 30f);
            var switches = CreateGrid(page, new Vector2(405f, 44f), 2, 10f);
            switches.spacing = new Vector2(30f, 6f);
            AddAppearanceSwitch(switches.transform, "Show coordinates", _preferences.showCoordinates, on => _preferences.showCoordinates = on);
            AddAppearanceSwitch(switches.transform, "Show legal moves", _preferences.showLegalMoves, on => _preferences.showLegalMoves = on);
            AddAppearanceSwitch(switches.transform, "Highlight last move", _preferences.highlightLastMove, on => _preferences.highlightLastMove = on);
            AddAppearanceSwitch(switches.transform, "Animate moves", _preferences.animateMoves, on => _preferences.animateMoves = on);
            UiFactory.AddLayout(switches, 94f);
            return page.gameObject;
        }

        private void AddAppearanceSwitch(Transform parent, string label, bool value, Action<bool> set)
        {
            UiSwitch.Create(label, parent, _theme, value, on =>
            {
                set(on);
                OnAppearanceChanged();
            });
        }

        private void AddSchemeSwatch(Image chip, BoardColorScheme scheme)
        {
            var board = UiFactory.CreateRect("Swatch", chip.transform);
            board.anchorMin = board.anchorMax = new Vector2(0f, 0.5f);
            board.pivot = new Vector2(0f, 0.5f);
            board.sizeDelta = new Vector2(52f, 52f);
            board.anchoredPosition = new Vector2(12f, 0f);
            for (int i = 0; i < 4; i++)
            {
                int x = i % 2, y = i / 2;
                var cell = UiFactory.CreateImage("Cell", board, (x + y) % 2 == 0 ? scheme.Dark : scheme.Light);
                UiFactory.SetAnchors(cell.rectTransform, new Vector2(x / 2f, y / 2f), new Vector2((x + 1) / 2f, (y + 1) / 2f));
            }
            var name = UiFactory.CreateText("Name", chip.transform, scheme.Name, 17, _theme.text, TextAnchor.MiddleLeft, UiFactory.Semibold);
            UiFactory.Stretch(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(74f, 0f);
        }

        private void AddPieceSetSample(Image chip, PieceSet set)
        {
            var row = UiFactory.CreateRect("Sample", chip.transform);
            UiFactory.SetAnchors(row, new Vector2(0f, 0.3f), new Vector2(1f, 1f));
            for (int i = 0; i < 3; i++)
            {
                var piece = i == 0 ? new Piece(PieceType.Knight, PieceColor.White)
                    : i == 1 ? new Piece(PieceType.Queen, PieceColor.Black)
                    : new Piece(PieceType.Bishop, PieceColor.White);
                var rect = UiFactory.CreateRect("Piece" + i, row);
                UiFactory.SetAnchors(rect, new Vector2(0.14f + i * 0.24f, 0.05f), new Vector2(0.38f + i * 0.24f, 0.95f));
                var view = rect.gameObject.AddComponent<ChessPieceView>();
                view.Build(_theme, _glyphFont, set);
                view.SetPiece(piece);
                _pieceSetIcons.Add(view);
            }
            var name = UiFactory.CreateText("Name", chip.transform, set.Name, 16, _theme.text, TextAnchor.MiddleCenter, UiFactory.Semibold);
            UiFactory.SetAnchors(name.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.32f));
        }

        private void OnAppearanceChanged()
        {
            Changed();
            RefreshAppearance();
            AppearanceChanged?.Invoke();
        }

        private void RefreshAppearance()
        {
            _preview.Apply(_preferences);
            _previewCaption.text = $"{_preferences.BoardScheme.Name} board  ·  {_preferences.PieceSet.Name} pieces";
        }

        // ---------------------------------------------------------------- sound

        private GameObject BuildSoundPage(RectTransform card)
        {
            var page = CreatePage(card, "Sound");

            UiFactory.AddLayout(UiFactory.CreateLabel("VolumeLabel", page, "Master volume", _theme), 24f);
            var volumeRow = UiFactory.CreateRect("Volume", page);
            UiFactory.AddLayout(volumeRow, 48f);
            var slider = UiFactory.CreateSlider("Slider", volumeRow, _theme, 0f, 1f, _preferences.masterVolume, value =>
            {
                _preferences.masterVolume = value;
                _volumeText.text = Mathf.RoundToInt(value * 100f) + "%";
                Changed();
            });
            var sliderRect = (RectTransform)slider.transform;
            UiFactory.Stretch(sliderRect);
            sliderRect.offsetMax = new Vector2(-90f, 0f);
            _volumeText = UiFactory.CreateText("Value", volumeRow, Mathf.RoundToInt(_preferences.masterVolume * 100f) + "%",
                22, _theme.text, TextAnchor.MiddleRight, UiFactory.Semibold);
            _volumeText.rectTransform.anchorMin = new Vector2(1f, 0f);
            _volumeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _volumeText.rectTransform.pivot = new Vector2(1f, 0.5f);
            _volumeText.rectTransform.sizeDelta = new Vector2(80f, 0f);

            UiFactory.AddLayout(UiFactory.CreateLabel("SoundsLabel", page, "Sounds", _theme), 40f);
            AddSoundSwitch(page, "Sound on", _preferences.soundEnabled, on => _preferences.soundEnabled = on);
            AddSoundSwitch(page, "Moves, captures and check", _preferences.gameSounds, on => _preferences.gameSounds = on);
            AddSoundSwitch(page, "Interface clicks", _preferences.interfaceSounds, on => _preferences.interfaceSounds = on);
            AddSoundSwitch(page, "BCI selection chime", _preferences.bciSounds, on => _preferences.bciSounds = on);

            var spacer = UiFactory.CreateRect("Spacer", page);
            UiFactory.AddLayout(spacer, 0f, flexibleHeight: 1f);

            var buttons = UiFactory.CreateRect("Buttons", page);
            UiFactory.AddHorizontalLayout(buttons, 14f, expandWidth: false);
            UiFactory.AddLayout(buttons, 56f);
            AddTestButton(buttons, "Test move", GameSound.Move);
            AddTestButton(buttons, "Test capture", GameSound.Capture);
            AddTestButton(buttons, "Test check", GameSound.Check);
            AddTestButton(buttons, "Test BCI", GameSound.BciSelect);
            return page.gameObject;
        }

        private void AddSoundSwitch(RectTransform page, string label, bool value, Action<bool> set)
        {
            var view = UiSwitch.Create(label, page, _theme, value, on =>
            {
                set(on);
                Changed();
            });
            UiFactory.AddLayout(view, 50f);
        }

        private void AddTestButton(RectTransform parent, string label, GameSound sound)
        {
            var button = UiFactory.CreateButton(label, parent, label, _theme, () => _audio.Play(sound), 18);
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
        }

        // ---------------------------------------------------------------- helpers

        private static RectTransform CreatePage(RectTransform card, string name)
        {
            var page = UiFactory.CreateRect(name, card);
            UiFactory.Stretch(page);
            UiFactory.AddVerticalLayout(page, 10f, new RectOffset(40, 40, 30, 36));
            return page;
        }

        private static GridLayoutGroup CreateGrid(RectTransform parent, Vector2 cell, int columns, float spacing)
        {
            var rect = UiFactory.CreateRect("Grid", parent);
            var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            return grid;
        }

        private static void PlaceLeft(RectTransform rect, float y, float height)
        {
            UiFactory.Place(rect, new Vector2(ColumnX, y), new Vector2(ColumnWidth, height));
        }

        private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> match)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (match(items[i]))
                    return i;
            }
            return 0;
        }
    }
}
