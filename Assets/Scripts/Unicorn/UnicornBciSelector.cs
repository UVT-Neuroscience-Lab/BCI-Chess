using System;
using System.Collections.Generic;
using BciChess.Bci;
using Gtec.Chain.Common.Templates.Utilities;
using Gtec.UnityInterface;
using static Gtec.Chain.Common.Templates.DataAcquisitionUnit.DataAcquisitionUnit;

namespace BciChess.Unicorn
{
    /// <summary>
    /// <see cref="IBciSelector"/> backed by the g.tec ERP pipeline and a Unicorn headset.
    /// The g.tec paradigm keeps flashing one tag per stimulus class; this selector maps the class the classifier
    /// reports back to the target currently assigned to that class. It is also the stimulus source for the
    /// visuals, so what the player sees flash is exactly what the paradigm flashes.
    /// A selection only counts once the chosen target has flashed a minimum number of times since the targets
    /// were presented; this also discards evidence gathered while other targets held the same class.
    /// </summary>
    public sealed class UnicornBciSelector : IBciSelector, IStimulusSource, IBciStatusProvider, ISelectionProgress,
        IDisposable
    {
        private static readonly IReadOnlyList<BciTarget> NoTargets = Array.Empty<BciTarget>();

        private readonly ERPParadigm _paradigm;
        private readonly ERPPipeline _pipeline;
        private readonly Device _device;
        private readonly StimulusSlot[] _slots;
        private readonly HashSet<int> _litClasses = new HashSet<int>();
        private readonly FlashCounter _flashes;

        private IReadOnlyList<BciTarget> _targets = NoTargets;
        private States _deviceState = States.Disconnected;
        private bool _available;

        public UnicornBciSelector(ERPParadigm paradigm, ERPPipeline pipeline, Device device,
            IReadOnlyList<StimulusSlot> slots, int minimumFlashes)
        {
            _paradigm = paradigm ? paradigm : throw new ArgumentNullException(nameof(paradigm));
            _pipeline = pipeline ? pipeline : throw new ArgumentNullException(nameof(pipeline));
            _device = device ? device : throw new ArgumentNullException(nameof(device));
            _slots = new List<StimulusSlot>(slots ?? throw new ArgumentNullException(nameof(slots))).ToArray();
            FlashesRequired = Math.Max(0, minimumFlashes);
            _flashes = new FlashCounter(this);

            _pipeline.OnClassSelection.AddListener(OnClassSelection);
            _device.OnDeviceStateChanged.AddListener(OnDeviceStateChanged);
            _paradigm.OnParadigmStarted.AddListener(UpdateAvailability);
            _paradigm.OnParadigmStopped.AddListener(UpdateAvailability);
        }

        public string Name => "Unicorn (ERP)";
        public bool IsAvailable => _available;
        public bool IsSelecting { get; private set; }
        public IReadOnlyList<BciTarget> CurrentTargets => IsSelecting ? _targets : NoTargets;

        public event Action AvailabilityChanged;
        public event Action<BciSelectionResult> SelectionFinished;
        public event Action<int> FlashStarted;

        /// <summary>Raised for notable events (ignored or unmatched selections, disconnects) for logging.</summary>
        public event Action<string> Diagnostic;

        public bool IsDeviceConnected => _deviceState == States.Connected || _deviceState == States.Acquiring;
        public bool IsTraining => _paradigm.IsRunning && _paradigm.ParadigmMode == ParadigmMode.Training;

        // IStimulusSource: the paradigm is the clock.
        public bool IsRunning => _paradigm.IsRunning;

        public bool IsLit(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slots.Length && _litClasses.Contains(_slots[slotIndex].ClassId);

        public int FlashesRequired { get; }

        public int FlashesCollected
        {
            get
            {
                if (!IsSelecting)
                    return 0;
                var slots = new List<int>(_targets.Count);
                foreach (var target in _targets)
                    slots.Add(target.Stimulus.Value.Index);
                return _flashes.MinimumOver(slots);
            }
        }

        public string StatusText
        {
            get
            {
                if (!IsDeviceConnected)
                    return "Headset not connected - connect it in the g.tec BCI bar";
                if (!_paradigm.IsRunning)
                    return "Connected - press Start Training, then Continue when training is done";
                if (IsTraining)
                    return "Calibrating - look at the highlighted target and count its flashes";
                return "Ready";
            }
        }

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
            _flashes.Reset();
            IsSelecting = _available;
        }

        public void StopSelection()
        {
            IsSelecting = false;
            _targets = NoTargets;
        }

        /// <summary>Called by the stimulus proxy tags when the paradigm switches a class on or off.</summary>
        public void OnStimulusChanged(int classId, bool on)
        {
            if (!on)
            {
                _litClasses.Remove(classId);
                return;
            }

            // Several proxy tags can share a class (e.g. the training object); count each flash once.
            if (!_litClasses.Add(classId))
                return;
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (_slots[slot].ClassId == classId)
                    FlashStarted?.Invoke(slot);
            }
        }

        /// <summary>
        /// Manual override: selects the current target on slot <paramref name="slotIndex"/> as if the classifier had
        /// reported it, without waiting for flashes. Returns false when idle or when no current target uses the slot.
        /// </summary>
        public bool TrySelectSlot(int slotIndex)
        {
            if (!IsSelecting)
                return false;
            foreach (var target in _targets)
            {
                if (target.Stimulus.Value.Index != slotIndex)
                    continue;
                Diagnostic?.Invoke($"Manual override selected slot {slotIndex + 1} ({target.Id}).");
                Finish(BciSelectionResult.Selected(target));
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            StopSelection();
            _flashes.Dispose();
            // The g.tec components may already be destroyed when the scene unloads.
            if (_pipeline)
                _pipeline.OnClassSelection.RemoveListener(OnClassSelection);
            if (_device)
                _device.OnDeviceStateChanged.RemoveListener(OnDeviceStateChanged);
            if (_paradigm)
            {
                _paradigm.OnParadigmStarted.RemoveListener(UpdateAvailability);
                _paradigm.OnParadigmStopped.RemoveListener(UpdateAvailability);
            }
        }

        private void OnClassSelection(ERPPipeline sender, ClassSelection selection)
        {
            if (!IsSelecting || selection == null)
                return;

            foreach (var target in _targets)
            {
                if (target.Stimulus.Value.ClassId != selection.Class)
                    continue;

                int flashes = _flashes.CountOf(target.Stimulus.Value.Index);
                if (flashes < FlashesRequired)
                {
                    Diagnostic?.Invoke($"Ignored class {selection.Class}: only {flashes}/{FlashesRequired} flashes " +
                                       "since the targets changed.");
                    return;
                }
                Finish(BciSelectionResult.Selected(target));
                return;
            }

            Diagnostic?.Invoke($"Class {selection.Class} (confidence {selection.Confidence:0.##}) matches no current target.");
            Finish(BciSelectionResult.Invalid($"Class {selection.Class} is not a current target."));
        }

        private void OnDeviceStateChanged(States state)
        {
            _deviceState = state;
            if (state == States.Disconnected)
                Diagnostic?.Invoke("Unicorn disconnected.");
            UpdateAvailability();
        }

        private void UpdateAvailability()
        {
            bool available = IsDeviceConnected && _paradigm.IsRunning && _paradigm.ParadigmMode == ParadigmMode.Application;
            if (!_paradigm.IsRunning)
                _litClasses.Clear();
            if (available == _available)
                return;

            _available = available;
            if (!available && IsSelecting)
                Finish(BciSelectionResult.Failed(StatusText));
            AvailabilityChanged?.Invoke();
        }

        private void Finish(BciSelectionResult result)
        {
            IsSelecting = false;
            _targets = NoTargets;
            SelectionFinished?.Invoke(result);
        }
    }
}
