using System;
using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using BciChess.Core;

namespace BciChess.Interaction
{
    public enum ChessTargetKind
    {
        Piece,
        Destination,
        Promotion,
        Cancel
    }

    /// <summary>Chess meaning of a <see cref="BciTarget"/>; stored as its opaque payload.</summary>
    public sealed class ChessTargetPayload
    {
        public ChessTargetPayload(ChessTargetKind kind, Square square = default, PieceType promotion = PieceType.None)
        {
            Kind = kind;
            Square = square;
            Promotion = promotion;
        }

        public ChessTargetKind Kind { get; }

        /// <summary>Board square for Piece and Destination targets.</summary>
        public Square Square { get; }

        /// <summary>Piece type for Promotion targets.</summary>
        public PieceType Promotion { get; }
    }

    public enum BciSessionStatus
    {
        /// <summary>BCI input is switched off.</summary>
        Disabled,

        /// <summary>Nothing to choose right now (e.g. game over).</summary>
        Idle,

        /// <summary>Targets are presented and the selector is running.</summary>
        AwaitingSelection,

        /// <summary>The candidates cannot be presented, even grouped (too few stimulus slots).</summary>
        TooManyCandidates,

        /// <summary>The selector (device) is not available.</summary>
        Unavailable
    }

    public sealed class BciSelectionOptions
    {
        /// <summary>Re-present the targets after this long without a result (0 = never).</summary>
        public float SelectionTimeoutSeconds { get; set; }

        /// <summary>Add a "Cancel" target when choosing a destination or promotion piece.</summary>
        public bool OfferCancelTarget { get; set; } = true;

        /// <summary>Choose automatically when only one candidate exists (CLAUDE.md §13).</summary>
        public bool AutoSelectSingleCandidate { get; set; } = true;

        /// <summary>How candidates are grouped when they exceed the available stimulus slots.</summary>
        public ICandidateGroupingStrategy GroupingStrategy { get; set; } = new BalancedGroupingStrategy();

        /// <summary>
        /// Targets (or groups) with squares within this distance, in squares, count as neighbours and get stimuli
        /// that are as different as possible. 1.5 = touching squares incl. diagonals. 0 = no neighbour handling.
        /// </summary>
        public float NeighbourDistance { get; set; } = 1.5f;

        /// <summary>
        /// Neighbour distance while choosing a destination. Legal moves often lie on one line (a rook's file, a
        /// bishop's diagonal); a larger distance keeps consecutive flashes several squares apart so they never look
        /// like a running sequence. 2.9 = anything within two squares, incl. knight jumps.
        /// </summary>
        public float DestinationNeighbourDistance { get; set; } = 2.9f;

        /// <summary>
        /// How different two stimulus slots are (larger = more different), e.g. by their on-screen colour.
        /// Null = distance of slot indices around the slot ring.
        /// </summary>
        public Func<int, int, double> SlotDistance { get; set; }
    }

    /// <summary>
    /// Presents the current choices of <see cref="SelectionController"/> as BCI targets (grouping them when
    /// there are more choices than stimulus slots) and feeds the BCI's result back into it.
    /// Owns no chess rules and no device code.
    /// </summary>
    public sealed class BciSelectionController : IDisposable
    {
        private static readonly IReadOnlyList<BciTarget> NoTargets = Array.Empty<BciTarget>();

        private readonly SelectionController _selection;
        private readonly IBciSelector _selector;
        private readonly StimulusManager _stimuli;
        private readonly BciSelectionOptions _options;
        private readonly CandidateNavigator _navigator;

        private IReadOnlyList<BciTarget> _targets = NoTargets;
        private IReadOnlyList<(int, int)> _neighbourSlotPairs = Array.Empty<(int, int)>();
        private bool _enabled;
        private float _elapsedSeconds;

        public BciSelectionController(SelectionController selection, IBciSelector selector, StimulusManager stimuli,
            BciSelectionOptions options = null)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _stimuli = stimuli ?? throw new ArgumentNullException(nameof(stimuli));
            _options = options ?? new BciSelectionOptions();
            _navigator = new CandidateNavigator(_options.GroupingStrategy);

            _selection.StateChanged += Restart;
            _selector.SelectionFinished += OnSelectionFinished;
            _selector.AvailabilityChanged += Restart;
            Restart();
        }

        /// <summary>Raised whenever status, targets or message change.</summary>
        public event Action Changed;

        /// <summary>Raised when the BCI picks a target, before it is acted on (for selection feedback).</summary>
        public event Action<BciTarget> TargetChosen;

        public IBciSelector Selector => _selector;
        public BciSessionStatus Status { get; private set; }

        /// <summary>Targets currently presented, each with its assigned stimulus.</summary>
        public IReadOnlyList<BciTarget> Targets => _targets;

        /// <summary>True while the targets are the legal destinations of the selected piece.</summary>
        public bool IsChoosingDestination => _selection.State == InteractionState.SelectingDestination;

        /// <summary>Stimulus slot pairs whose current targets are neighbours on screen (for flash scheduling).</summary>
        public IReadOnlyList<(int, int)> NeighbourSlotPairs => _neighbourSlotPairs;

        /// <summary>Number of chess candidates in the current step (pieces, destinations or promotion pieces).</summary>
        public int CandidateCount { get; private set; }

        /// <summary>Group nesting level: 0 = all candidates, 1 = inside a group, ...</summary>
        public int Depth => _navigator.Depth;

        public int Capacity => _stimuli.Capacity;

        /// <summary>Last notice for the player (auto-selection, timeout, device error); empty if none.</summary>
        public string Message { get; private set; } = string.Empty;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                Message = string.Empty;
                Restart();
            }
        }

        /// <summary>Advances the selection timeout. Call once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Status != BciSessionStatus.AwaitingSelection || _options.SelectionTimeoutSeconds <= 0f)
                return;

            _elapsedSeconds += deltaSeconds;
            if (_elapsedSeconds >= _options.SelectionTimeoutSeconds)
            {
                Message = "No selection detected - starting again";
                PresentLevel();
            }
        }

        public void Dispose()
        {
            _selection.StateChanged -= Restart;
            _selector.SelectionFinished -= OnSelectionFinished;
            _selector.AvailabilityChanged -= Restart;
            _selector.StopSelection();
        }

        /// <summary>
        /// The chess choices for the selection controller's current state. Squares are ordered by file, then
        /// rank, so consecutive candidates are neighbours on the board and groups form compact areas.
        /// </summary>
        public static List<BciTarget> BuildCandidates(SelectionController selection)
        {
            var targets = new List<BciTarget>();
            var position = selection.Game.Position;

            switch (selection.State)
            {
                case InteractionState.SelectingPiece:
                    foreach (var square in SpatialOrder(selection.SelectablePieces))
                    {
                        targets.Add(new BciTarget("piece:" + square, $"{position[square].Type} {square}",
                            new ChessTargetPayload(ChessTargetKind.Piece, square), position: PositionOf(square)));
                    }
                    break;

                case InteractionState.SelectingDestination:
                    foreach (var square in SpatialOrder(selection.SelectableDestinations))
                    {
                        string label = position[square].IsNone ? square.ToString() : $"{square} (capture)";
                        targets.Add(new BciTarget("dest:" + square, label,
                            new ChessTargetPayload(ChessTargetKind.Destination, square), position: PositionOf(square)));
                    }
                    break;

                case InteractionState.SelectingPromotion:
                    // The promotion buttons sit side by side in one row, well away from the board.
                    var pieces = SelectionController.PromotionPieces;
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        targets.Add(new BciTarget("promo:" + pieces[i], pieces[i].ToString(),
                            new ChessTargetPayload(ChessTargetKind.Promotion, promotion: pieces[i]),
                            position: new TargetPosition(i, PromotionRowY)));
                    }
                    break;
            }
            return targets;
        }

        private static IEnumerable<Square> SpatialOrder(IEnumerable<Square> squares) =>
            squares.OrderBy(s => s.File).ThenBy(s => s.Rank);

        private const float PromotionRowY = 100f;

        /// <summary>Board position in squares; flipping the board does not change which squares are adjacent.</summary>
        private static TargetPosition PositionOf(Square square) => new TargetPosition(square.File, square.Rank);

        /// <summary>Starts a new step from the top level (selection state, availability or enabled changed).</summary>
        private void Restart()
        {
            _selector.StopSelection();
            _targets = NoTargets;
            _neighbourSlotPairs = Array.Empty<(int, int)>();
            CandidateCount = 0;

            if (!_enabled)
            {
                SetStatus(BciSessionStatus.Disabled);
                return;
            }

            var candidates = BuildCandidates(_selection);
            CandidateCount = candidates.Count;
            if (candidates.Count == 0)
            {
                SetStatus(BciSessionStatus.Idle);
                return;
            }

            if (_options.AutoSelectSingleCandidate && candidates.Count == 1)
            {
                Message = $"Only one option - {candidates[0].Label} selected automatically";
                // Applying changes the selection state, which re-enters Restart for the next step.
                if (Apply(candidates[0].Payload as ChessTargetPayload))
                    return;
            }

            _navigator.Reset(candidates);
            PresentLevel();
        }

        /// <summary>Presents the navigator's current level: candidates or groups, plus Back/Cancel.</summary>
        private void PresentLevel()
        {
            _selector.StopSelection();
            _targets = NoTargets;
            _neighbourSlotPairs = Array.Empty<(int, int)>();
            _elapsedSeconds = 0f;

            if (!_selector.IsAvailable)
            {
                SetStatus(BciSessionStatus.Unavailable);
                return;
            }

            var extras = new List<BciTarget>(1);
            if (_navigator.Depth > 0)
                extras.Add(new BciTarget("back", "Back", NavigationCommand.Back));
            else if (_options.OfferCancelTarget && _selection.State != InteractionState.SelectingPiece)
                extras.Add(new BciTarget("cancel", "Cancel", new ChessTargetPayload(ChessTargetKind.Cancel)));

            if (!_navigator.TryGetOptions(_stimuli.Capacity - extras.Count, out var options))
            {
                SetStatus(BciSessionStatus.TooManyCandidates);
                return;
            }

            var all = new List<BciTarget>(options.Count + extras.Count);
            all.AddRange(options);
            all.AddRange(extras);
            float neighbourDistance = IsChoosingDestination
                ? Math.Max(_options.NeighbourDistance, _options.DestinationNeighbourDistance)
                : _options.NeighbourDistance;
            var neighbours = NeighbourGraph.Build(all, neighbourDistance);
            if (!_stimuli.TryAssign(all, neighbours, out var assigned, _options.SlotDistance))
            {
                SetStatus(BciSessionStatus.TooManyCandidates);
                return;
            }

            _targets = assigned;
            _neighbourSlotPairs = neighbours.Pairs()
                .Select(p => (assigned[p.Item1].Stimulus.Value.Index, assigned[p.Item2].Stimulus.Value.Index))
                .ToList();

            // Start the selector before announcing the new targets, so flashes shown in reaction to the
            // announcement are already counted for this selection.
            _selector.StartSelection(assigned);
            if (_targets == assigned) // unless the selector answered synchronously
                SetStatus(BciSessionStatus.AwaitingSelection);
        }

        private void OnSelectionFinished(BciSelectionResult result)
        {
            _targets = NoTargets;

            if (result.Status != BciSelectionStatus.Selected)
            {
                Message = result.Status == BciSelectionStatus.Invalid
                    ? "Selection not recognised - try again"
                    : string.IsNullOrEmpty(result.Message) ? "BCI selection failed" : result.Message;
                PresentLevel();
                return;
            }

            Message = string.Empty;
            TargetChosen?.Invoke(result.Target);
            switch (result.Target.Payload)
            {
                case CandidateGroup group:
                    _navigator.Enter(group);
                    PresentLevel();
                    break;

                case NavigationCommand command when command == NavigationCommand.Back:
                    _navigator.Back();
                    PresentLevel();
                    break;

                case ChessTargetPayload payload:
                    // A successful apply changes the selection state, which restarts the session.
                    if (!Apply(payload))
                    {
                        Message = $"Could not use selection '{result.Target.Label}'";
                        Restart();
                    }
                    break;

                default:
                    Message = $"Unknown target '{result.Target.Label}'";
                    PresentLevel();
                    break;
            }
        }

        private bool Apply(ChessTargetPayload payload)
        {
            if (payload == null)
                return false;

            switch (payload.Kind)
            {
                case ChessTargetKind.Piece:
                case ChessTargetKind.Destination:
                    return _selection.SelectSquare(payload.Square);
                case ChessTargetKind.Promotion:
                    return _selection.SelectPromotion(payload.Promotion);
                case ChessTargetKind.Cancel:
                    if (_selection.State != InteractionState.SelectingDestination &&
                        _selection.State != InteractionState.SelectingPromotion)
                        return false;
                    _selection.Cancel();
                    return true;
                default:
                    return false;
            }
        }

        private void SetStatus(BciSessionStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }
    }
}
