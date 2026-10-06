using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    public enum BciSelectionStatus
    {
        /// <summary>The player chose <see cref="BciSelectionResult.Target"/>.</summary>
        Selected,

        /// <summary>The device reported something that matches no current target.</summary>
        Invalid,

        /// <summary>The device failed or disconnected during the selection.</summary>
        Failed
    }

    public readonly struct BciSelectionResult
    {
        public readonly BciSelectionStatus Status;
        public readonly BciTarget Target;
        public readonly string Message;

        private BciSelectionResult(BciSelectionStatus status, BciTarget target, string message)
        {
            Status = status;
            Target = target;
            Message = message ?? string.Empty;
        }

        public static BciSelectionResult Selected(BciTarget target) =>
            new BciSelectionResult(BciSelectionStatus.Selected, target, null);

        public static BciSelectionResult Invalid(string message) =>
            new BciSelectionResult(BciSelectionStatus.Invalid, null, message);

        public static BciSelectionResult Failed(string message) =>
            new BciSelectionResult(BciSelectionStatus.Failed, null, message);
    }

    /// <summary>
    /// A device (or simulation) that lets the player pick one of a set of stimulus-tagged targets.
    /// The game talks only to this interface, never to device-specific code.
    /// Implementations must raise events on the Unity main thread.
    /// </summary>
    public interface IBciSelector
    {
        /// <summary>Human-readable name for the UI, e.g. "Simulated (keyboard)".</summary>
        string Name { get; }

        /// <summary>True when the selector can currently run a selection (device connected, classifier trained).</summary>
        bool IsAvailable { get; }

        /// <summary>True between <see cref="StartSelection"/> and a result or <see cref="StopSelection"/>.</summary>
        bool IsSelecting { get; }

        /// <summary>The targets of the running selection; empty when idle.</summary>
        IReadOnlyList<BciTarget> CurrentTargets { get; }

        event Action AvailabilityChanged;

        /// <summary>Raised once per selection when it ends with a result. Not raised for <see cref="StopSelection"/>.</summary>
        event Action<BciSelectionResult> SelectionFinished;

        /// <summary>
        /// Starts selecting among <paramref name="targets"/>, replacing any running selection.
        /// Every target must already have a stimulus assigned (see <see cref="StimulusManager"/>).
        /// </summary>
        void StartSelection(IReadOnlyList<BciTarget> targets);

        /// <summary>Stops the running selection without a result. Safe to call when idle.</summary>
        void StopSelection();
    }
}
