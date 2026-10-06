using System;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>How BCI targets look and flash. Tunable in the inspector.</summary>
    [Serializable]
    public sealed class StimulusVisualSettings
    {
        [Header("Flash timing (simulated mode)")]
        [Tooltip("How long one flash stays lit, in milliseconds.")]
        [Min(1f)] public float flashOnTimeMs = 100f;

        [Tooltip("Dark gap between two flashes, in milliseconds.")]
        [Min(0f)] public float flashOffTimeMs = 75f;

        [Tooltip("Dark gap between two flashes while choosing where the selected piece moves. A clear pause " +
                 "after every legal-move flash keeps them from blending into a moving sequence.")]
        [Min(0f)] public float destinationFlashOffTimeMs = 300f;

        [Header("Look")]
        [Tooltip("Overlay drawn over a target while it flashes.")]
        public Color flashColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Pulse shown on a target when the BCI selects it.")]
        public Color chosenColor = new Color(0.3f, 1f, 0.45f, 0.85f);

        [Min(0.05f)] public float chosenFeedbackSeconds = 0.6f;

        [Tooltip("Show the slot number (simulated BCI key) on each target.")]
        public bool showSlotNumbers = true;

        [Tooltip("Frame/badge colour per stimulus slot, so each target's stimulus is identifiable. Cycles if shorter.")]
        public Color[] slotColors =
        {
            new Color32(0x4F, 0xA3, 0xFF, 0xFF), // blue
            new Color32(0xF2, 0x9A, 0x2E, 0xFF), // orange
            new Color32(0x3C, 0xC8, 0x6E, 0xFF), // green
            new Color32(0xE0, 0x4F, 0xC8, 0xFF), // magenta
            new Color32(0xF5, 0xD4, 0x3C, 0xFF), // yellow
            new Color32(0x2E, 0xD3, 0xD6, 0xFF), // cyan
            new Color32(0xA0, 0x6C, 0xF0, 0xFF), // purple
            new Color32(0xF0, 0x5A, 0x5A, 0xFF), // red
            new Color32(0x9C, 0xD6, 0x3A, 0xFF), // lime
            new Color32(0xF0, 0xF0, 0xF0, 0xFF)  // white
        };

        /// <summary>How different two slots look: distance of their colours in RGB space (0 = identical).</summary>
        public double SlotColorDistance(int a, int b)
        {
            var ca = SlotColor(a);
            var cb = SlotColor(b);
            float dr = ca.r - cb.r, dg = ca.g - cb.g, db = ca.b - cb.b;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        public Color SlotColor(int slotIndex)
        {
            if (slotColors == null || slotColors.Length == 0)
                return Color.white;
            return slotColors[((slotIndex % slotColors.Length) + slotColors.Length) % slotColors.Length];
        }
    }
}
