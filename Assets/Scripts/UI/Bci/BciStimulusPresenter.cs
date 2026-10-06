using System.Collections.Generic;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>
    /// Shows the current BCI targets on screen and flashes them. Maps each target to its on-screen elements
    /// (squares, promotion buttons, command tiles; a group maps to all its members) and lights the elements of
    /// the slot the stimulus source reports as lit.
    /// </summary>
    public sealed class BciStimulusPresenter : MonoBehaviour
    {
        private readonly BciTargetVisual[] _squares = new BciTargetVisual[64];
        private readonly Dictionary<PieceType, BciTargetVisual> _promotion = new Dictionary<PieceType, BciTargetVisual>();
        private readonly List<BciTargetVisual> _cancel = new List<BciTargetVisual>();
        private readonly List<BciTargetVisual> _back = new List<BciTargetVisual>();
        private readonly List<BciTargetVisual> _all = new List<BciTargetVisual>();
        private readonly List<(BciTargetVisual visual, int slot)> _active = new List<(BciTargetVisual, int)>();
        private readonly List<int> _slots = new List<int>();

        private BciSelectionController _bci;
        private BciCommandBar _commandBar;
        private FlashSequencer _sequencer;
        private IStimulusSource _source;
        private int _neighbourFlashSpacing = 2;
        private StimulusVisualSettings _settings;

        /// <summary>The stimulus timeline the visuals follow.</summary>
        public IStimulusSource Source => _source;

        /// <param name="source">
        /// What the visuals follow. A <see cref="FlashSequencer"/> (simulated mode) is driven by this presenter:
        /// started for each new set of targets, with neighbouring targets kept <paramref name="neighbourFlashSpacing"/>
        /// flashes apart. Any other source (e.g. the g.tec paradigm) owns its own timing.
        /// When null, a local sequencer is created.
        /// </param>
        public void Initialize(BciSelectionController bci, StimulusVisualSettings settings, BoardView board,
            GameHudView hud, BciCommandBar commandBar, IStimulusSource source = null, int neighbourFlashSpacing = 2)
        {
            _bci = bci;
            _commandBar = commandBar;
            _settings = settings;
            _source = source ?? new FlashSequencer(settings.flashOnTimeMs, settings.flashOffTimeMs);
            _sequencer = _source as FlashSequencer;
            _neighbourFlashSpacing = neighbourFlashSpacing;

            for (int i = 0; i < 64; i++)
                _squares[i] = Register(BciTargetVisual.Attach(board.GetSquareView(new Square(i)).Rect, settings, true));

            foreach (var type in SelectionController.PromotionPieces)
                _promotion[type] = Register(BciTargetVisual.Attach(hud.GetPromotionButton(type), settings, true));

            _cancel.Add(Register(commandBar.CancelVisual));
            _cancel.Add(Register(BciTargetVisual.Attach(hud.PromotionCancelButton, settings, true)));
            _back.Add(Register(commandBar.BackVisual));

            _bci.Changed += Refresh;
            _bci.TargetChosen += OnTargetChosen;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_bci == null)
                return;
            _bci.Changed -= Refresh;
            _bci.TargetChosen -= OnTargetChosen;
        }

        private void Update()
        {
            if (_source == null)
                return;

            _sequencer?.Tick(Time.unscaledDeltaTime);
            foreach (var (visual, slot) in _active)
                visual.SetLit(_source.IsLit(slot));
        }

        private BciTargetVisual Register(BciTargetVisual visual)
        {
            _all.Add(visual);
            return visual;
        }

        private void Refresh()
        {
            foreach (var visual in _all)
                visual.SetTarget(false);
            _active.Clear();
            _slots.Clear();

            bool hasCancel = false;
            bool hasBack = false;

            if (_bci.Status == BciSessionStatus.AwaitingSelection)
            {
                foreach (var target in _bci.Targets)
                {
                    int slot = target.Stimulus.Value.Index;
                    string label = FakeBciKeyboardInput.KeyLabel(slot);
                    _slots.Add(slot);
                    hasCancel |= target.Id == "cancel";
                    hasBack |= target.Payload is NavigationCommand;

                    foreach (var visual in VisualsFor(target))
                    {
                        visual.SetTarget(true, slot, label);
                        _active.Add((visual, slot));
                    }
                }
                if (_sequencer != null)
                {
                    // Legal-move targets get a longer dark pause after each flash.
                    _sequencer.SetTiming(_settings.flashOnTimeMs,
                        _bci.IsChoosingDestination ? _settings.destinationFlashOffTimeMs : _settings.flashOffTimeMs);
                    _sequencer.Start(_slots, _bci.NeighbourSlotPairs, _neighbourFlashSpacing);
                }
            }
            else
            {
                _sequencer?.Stop();
            }

            _commandBar.Show(hasCancel, hasBack);
        }

        private void OnTargetChosen(BciTarget target)
        {
            foreach (var visual in VisualsFor(target))
                visual.PlayChosen();
        }

        private IEnumerable<BciTargetVisual> VisualsFor(BciTarget target)
        {
            switch (target.Payload)
            {
                case CandidateGroup group:
                    foreach (var member in group.Members)
                    {
                        foreach (var visual in VisualsFor(member))
                            yield return visual;
                    }
                    break;

                case NavigationCommand _:
                    foreach (var visual in _back)
                        yield return visual;
                    break;

                case ChessTargetPayload payload:
                    switch (payload.Kind)
                    {
                        case ChessTargetKind.Piece:
                        case ChessTargetKind.Destination:
                            yield return _squares[payload.Square.Index];
                            break;
                        case ChessTargetKind.Promotion:
                            if (_promotion.TryGetValue(payload.Promotion, out var promotion))
                                yield return promotion;
                            break;
                        case ChessTargetKind.Cancel:
                            foreach (var visual in _cancel)
                                yield return visual;
                            break;
                    }
                    break;
            }
        }
    }
}
