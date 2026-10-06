using System;
using UnityEngine;

namespace BciChess.UI
{
    public enum PieceGlyphStyle
    {
        /// <summary>Unicode chess symbols from an OS font (Segoe UI Symbol on Windows).</summary>
        UnicodeSymbols,

        /// <summary>Plain letters (K, Q, R, B, N, P); works with any font.</summary>
        Letters
    }

    /// <summary>
    /// Interface colours and the board highlight colours. Square colours and piece images come from the
    /// player's choice in the lobby (<see cref="BoardColorScheme"/>, <see cref="PieceSet"/>). Tweak in the inspector.
    /// </summary>
    [Serializable]
    public sealed class BoardTheme
    {
        [Header("Surfaces")]
        public Color background = new Color32(0x1D, 0x21, 0x2C, 0xFF);
        public Color backgroundBottom = new Color32(0x10, 0x12, 0x18, 0xFF);
        public Color panel = new Color32(0x23, 0x28, 0x35, 0xFF);
        public Color panelRaised = new Color32(0x2D, 0x33, 0x43, 0xFF);
        public Color boardFrame = new Color32(0x14, 0x17, 0x1F, 0xFF);
        public Color divider = new Color(1f, 1f, 1f, 0.07f);

        [Header("Board highlights")]
        public Color lastMoveTint = new Color(1f, 0.93f, 0.25f, 0.42f);
        public Color selectedTint = new Color(1f, 0.86f, 0.2f, 0.62f);
        public Color selectableTint = new Color(0.36f, 0.62f, 1f, 0.22f);
        public Color checkTint = new Color(1f, 0.12f, 0.08f, 0.95f);
        public Color destinationMarker = new Color(0f, 0f, 0f, 0.2f);
        public Color cursor = new Color(0.15f, 0.95f, 1f, 1f);

        [Header("Symbol pieces (the 'Symbols' piece set)")]
        public PieceGlyphStyle glyphStyle = PieceGlyphStyle.UnicodeSymbols;
        [Tooltip("OS fonts tried in order for Unicode chess symbols.")]
        public string[] glyphFontNames = { "Segoe UI Symbol", "DejaVu Sans", "Arial Unicode MS" };
        public Color whitePiece = new Color(0.98f, 0.97f, 0.94f);
        public Color whitePieceOutline = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        public Color blackPiece = new Color(0.1f, 0.1f, 0.12f);
        public Color blackPieceOutline = new Color(0.9f, 0.9f, 0.9f, 0.55f);

        [Header("Text & controls")]
        public Color text = new Color32(0xEE, 0xF0, 0xF5, 0xFF);
        public Color mutedText = new Color32(0x8E, 0x96, 0xA8, 0xFF);
        public Color accent = new Color32(0x5B, 0x9D, 0xFF, 0xFF);
        public Color primary = new Color32(0x6C, 0xB0, 0x45, 0xFF);
        public Color warning = new Color32(0xFF, 0x6B, 0x5E, 0xFF);
        public Color gold = new Color32(0xF4, 0xC1, 0x53, 0xFF);
        public Color button = new Color32(0x38, 0x3F, 0x54, 0xFF);
        public Color buttonText = new Color32(0xF2, 0xF4, 0xF8, 0xFF);
    }
}
