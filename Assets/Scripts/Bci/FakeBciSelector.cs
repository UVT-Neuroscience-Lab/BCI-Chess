using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// Simulated BCI for development and demos without the headset. Something external (keyboard input,
    /// tests) reports which stimulus the "player" attended to via <see cref="TrySelectSlot"/>.
    /// </summary>
    public sealed class FakeBciSelector : IBciSelector, ISelectionProgress, IDisposable
    {
        private static readonly IReadOnlyList<BciTarget> NoTargets = Array.Empty<BciTarget>();

        private IReadOnlyList<BciTarget> _targets = NoTargets;
        private bool _isAvailable = true;
        private FlashCounter _flashes;

        /// <param name="minimumFlashes">
        /// Flashes a target must have had before it can be selected, like a real ERP classifier needs evidence.
        /// Only enforced once a stimulus source is attached (<see cref="AttachStimulusSource"/>).
        /// </param>
        public FakeBciSelector(int minimumFlashes = 0)
        {
            FlashesRequired = Math.Max(0, minimumFlashes);
        }

        public string Name => "Simulated (keyboard)";

        public int FlashesRequired { get; }

        public int FlashesCollected =>
            _flashes == null || !IsSelecting ? 0 : _flashes.MinimumOver(SlotsOf(_targets));

        /// <summary>Counts flashes from <paramref name="source"/> to enforce the minimum before a selection.</summary>
        public void AttachStimulusSource(IStimulusSource source)
        {
            _flashes?.Dispose();
            _flashes = source == null ? null : new FlashCounter(source);
        }

        public void Dispose()
        {
            _flashes?.Dispose();
            _flashes = null;
        }
        public bool IsAvailable => _isAvailable;
        public bool IsSelecting { get; private set; }
        public IReadOnlyList<BciTarget> CurrentTargets => IsSelecting ? _targets : NoTargets;

        public event Action AvailabilityChanged;
        public event Action<BciSelectionResult> SelectionFinished;

        public void StartSelection(IReadOnlyList<BciTarget> targets)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            foreach (var target in targets)
            {
                if (!target.Stimulus.HasValue)
                    throw new ArgumentException($"Target '{target.Id}' has no stimulus assigned.", nameof(targets));
            }

            _targets = targets;
            _flashes?.Reset();
            IsSelecting = _isAvailable;
        }

        public void StopSelection()
        {
            IsSelecting = false;
            _targets = NoTargets;
        }

        /// <summary>Simulates the player attending to the stimulus in slot <paramref name="slotIndex"/>.</summary>
        /// <returns>
        /// False when idle or when the slot has not flashed the required number of times yet (the selection keeps
        /// running); otherwise true (an unknown slot finishes with an Invalid result).
        /// </returns>
        public bool TrySelectSlot(int slotIndex)
        {
            if (!IsSelecting)
                return false;
            if (_flashes != null && _flashes.CountOf(slotIndex) < FlashesRequired)
                return false;

            foreach (var target in _targets)
            {
                if (target.Stimulus.Value.Index == slotIndex)
                {
                    Finish(BciSelectionResult.Selected(target));
                    return true;
                }
            }
            Finish(BciSelectionResult.Invalid($"No target on slot {slotIndex + 1}."));
            return true;
        }

        /// <summary>Simulates connecting/disconnecting the device.</summary>
        public void SetAvailable(bool available)
        {
            if (_isAvailable == available)
                return;
            _isAvailable = available;
            if (!available && IsSelecting)
                Finish(BciSelectionResult.Failed("Simulated BCI disconnected."));
            AvailabilityChanged?.Invoke();
        }

        private static IEnumerable<int> SlotsOf(IReadOnlyList<BciTarget> targets)
        {
            foreach (var target in targets)
                yield return target.Stimulus.Value.Index;
        }

        private void Finish(BciSelectionResult result)
        {
            IsSelecting = false;
            _targets = NoTargets;
            SelectionFinished?.Invoke(result);
        }
    }
}
