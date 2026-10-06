using System;
using BciChess.Unicorn;
using UnityEngine;

namespace BciChess.UI
{
    public enum BciMode
    {
        /// <summary>No BCI; mouse and keyboard only.</summary>
        Off,

        /// <summary>Fake BCI driven by number keys.</summary>
        Simulated,

        /// <summary>Unicorn Hybrid Black headset through the g.tec ERP pipeline.</summary>
        Unicorn
    }

    /// <summary>BCI configuration, tunable in the inspector.</summary>
    [Serializable]
    public sealed class BciSettings
    {
        public BciMode mode = BciMode.Simulated;

        [Tooltip("Start the game with BCI selection active (toggle at runtime with F3).")]
        public bool enabledOnStart = true;

        [Tooltip("ERP class ids used as stimuli, in assignment order. Must match the flash tag class ids " +
                 "configured in the g.tec ERP paradigm.")]
        public int[] stimulusClassIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [Tooltip("Maximum number of targets presented at once. More candidates are split into groups. " +
                 "The simulated BCI has keys for the first 10 slots.")]
        [Min(1)] public int maxSimultaneousTargets = 6;

        [Tooltip("Choose automatically when there is only one candidate (one movable piece or one destination).")]
        public bool autoSelectSingleCandidate = true;

        [Tooltip("Restart a selection if no result arrives within this many seconds (0 = wait forever).")]
        [Min(0f)] public float selectionTimeoutSeconds = 0f;

        [Tooltip("Offer a 'Cancel' target while choosing a destination or promotion piece.")]
        public bool offerCancelTarget = true;

        [Header("Selection reliability")]
        [Tooltip("A target must have flashed at least this many times since it appeared before it can be selected.")]
        [Min(0)] public int minimumFlashesBeforeSelection = 10;

        [Tooltip("Targets or groups whose squares are within this many squares of each other count as neighbours " +
                 "and get the most different stimuli (colour and flash timing). 1.5 = touching incl. diagonals; 0 = off.")]
        [Min(0f)] public float neighbourDistance = 1.5f;

        [Tooltip("Neighbour distance while choosing a destination. Legal moves often lie on one line; a larger " +
                 "distance keeps consecutive flashes several squares apart. 2.9 = within two squares, incl. knight jumps.")]
        [Min(0f)] public float destinationNeighbourDistance = 2.9f;

        [Tooltip("Minimum distance, in flashes, between flashes of two neighbours (2 = at least one other flash in " +
                 "between). Applies to the simulated flashing; in Unicorn mode the g.tec paradigm sets the flash order.")]
        [Min(1)] public int neighbourFlashSpacing = 2;

        [Tooltip("Flash timing (simulated mode) and target appearance.")]
        public StimulusVisualSettings visuals = new StimulusVisualSettings();

        [Tooltip("Used when Mode is Unicorn. In that mode the g.tec paradigm controls flash timing " +
                 "(On/Off Time on its ERPParadigm component).")]
        public UnicornSettings unicorn = new UnicornSettings();
    }
}
