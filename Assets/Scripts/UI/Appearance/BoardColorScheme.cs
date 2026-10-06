using System.Collections.Generic;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>A named pair of square colours, selectable in the lobby.</summary>
    public sealed class BoardColorScheme
    {
        public BoardColorScheme(string id, string name, Color32 light, Color32 dark)
        {
            Id = id;
            Name = name;
            Light = light;
            Dark = dark;
        }

        public string Id { get; }
        public string Name { get; }
        public Color Light { get; }
        public Color Dark { get; }

        public static readonly IReadOnlyList<BoardColorScheme> All = new[]
        {
            new BoardColorScheme("green", "Green", new Color32(0xEE, 0xEE, 0xD2, 0xFF), new Color32(0x76, 0x96, 0x56, 0xFF)),
            new BoardColorScheme("brown", "Brown", new Color32(0xF0, 0xD9, 0xB5, 0xFF), new Color32(0xB5, 0x88, 0x63, 0xFF)),
            new BoardColorScheme("blue", "Blue", new Color32(0xDE, 0xE3, 0xE6, 0xFF), new Color32(0x8C, 0xA2, 0xAD, 0xFF)),
            new BoardColorScheme("icy", "Icy Sea", new Color32(0xD3, 0xE2, 0xEA, 0xFF), new Color32(0x6F, 0x95, 0xAF, 0xFF)),
            new BoardColorScheme("purple", "Purple", new Color32(0xEF, 0xEB, 0xF5, 0xFF), new Color32(0x88, 0x77, 0xB7, 0xFF)),
            new BoardColorScheme("walnut", "Walnut", new Color32(0xE4, 0xC6, 0x9C, 0xFF), new Color32(0x96, 0x63, 0x3E, 0xFF)),
            new BoardColorScheme("sand", "Sand", new Color32(0xF4, 0xE8, 0xD0, 0xFF), new Color32(0xC9, 0xA6, 0x6B, 0xFF)),
            new BoardColorScheme("coral", "Coral", new Color32(0xB1, 0xE4, 0xB9, 0xFF), new Color32(0x70, 0xA2, 0xA3, 0xFF)),
            new BoardColorScheme("cherry", "Cherry", new Color32(0xF5, 0xDE, 0xE2, 0xFF), new Color32(0xC0, 0x72, 0x84, 0xFF)),
            new BoardColorScheme("slate", "Slate", new Color32(0xA7, 0xB1, 0xBE, 0xFF), new Color32(0x50, 0x5E, 0x72, 0xFF)),
        };

        public static BoardColorScheme Find(string id)
        {
            foreach (var scheme in All)
            {
                if (scheme.Id == id)
                    return scheme;
            }
            return All[0];
        }
    }
}
