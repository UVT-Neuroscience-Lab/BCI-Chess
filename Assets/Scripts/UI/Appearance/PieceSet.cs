using System.Collections.Generic;
using BciChess.Core;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>
    /// A selectable piece style: images from <c>Resources/PieceSets/&lt;folder&gt;</c>, or the built-in Unicode glyphs
    /// (no folder) which work without any image assets.
    /// </summary>
    public sealed class PieceSet
    {
        private readonly Dictionary<Piece, Sprite> _sprites = new Dictionary<Piece, Sprite>();
        private bool _loaded;

        private PieceSet(string id, string name, string folder)
        {
            Id = id;
            Name = name;
            Folder = folder;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Resources sub-folder holding the images; null for the glyph set.</summary>
        public string Folder { get; }

        public bool UsesGlyphs => Folder == null;

        public static readonly IReadOnlyList<PieceSet> All = new[]
        {
            new PieceSet("cburnett", "Classic", "PieceSets/cburnett"),
            new PieceSet("merida", "Merida", "PieceSets/merida"),
            new PieceSet("chessnut", "Chessnut", "PieceSets/chessnut"),
            new PieceSet("rhosgfx", "Cartoon", "PieceSets/rhosgfx"),
            new PieceSet("fantasy", "Fantasy", "PieceSets/fantasy"),
            new PieceSet("celtic", "Celtic", "PieceSets/celtic"),
            new PieceSet("spatial", "Spatial", "PieceSets/spatial"),
            new PieceSet("glyphs", "Symbols", null),
        };

        public static PieceSet Find(string id)
        {
            foreach (var set in All)
            {
                if (set.Id == id)
                    return set;
            }
            return All[0];
        }

        /// <summary>The image for <paramref name="piece"/>, or null for the glyph set or a missing image.</summary>
        public Sprite GetSprite(Piece piece)
        {
            if (UsesGlyphs || piece.IsNone)
                return null;
            if (!_loaded)
                Load();
            return _sprites.TryGetValue(piece, out var sprite) ? sprite : null;
        }

        private void Load()
        {
            _loaded = true;
            foreach (PieceColor color in new[] { PieceColor.White, PieceColor.Black })
            {
                foreach (PieceType type in new[] { PieceType.King, PieceType.Queen, PieceType.Rook, PieceType.Bishop,
                             PieceType.Knight, PieceType.Pawn })
                {
                    var piece = new Piece(type, color);
                    string path = $"{Folder}/{(color == PieceColor.White ? 'w' : 'b')}{char.ToUpperInvariant(piece.ToFenChar())}";
                    var sprite = Resources.Load<Sprite>(path);
                    if (sprite == null)
                    {
                        // Imported as a plain texture (e.g. before the importer settings applied).
                        var texture = Resources.Load<Texture2D>(path);
                        if (texture != null)
                            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f), 100f);
                    }
                    if (sprite == null)
                        Debug.LogWarning($"Piece image missing: Resources/{path}.png");
                    else
                        _sprites[piece] = sprite;
                }
            }
        }
    }
}
