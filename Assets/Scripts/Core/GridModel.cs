using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cosecha
{
    public enum ShapeFamily { Line, Corner, Block }

    public static class Shapes
    {
        public const int MaxSize = 6;

        static readonly Vector2Int[][] Families =
        {
            new[] { V(0,0), V(1,0), V(2,0), V(3,0), V(4,0), V(0,1) },   // Line
            new[] { V(0,0), V(0,1), V(1,0), V(0,2), V(0,3), V(0,4) },   // Corner
            new[] { V(0,0), V(1,0), V(0,1), V(1,1), V(0,2), V(1,2) },   // Block
        };

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        /// <summary>Rotate a local offset by 90° counter-clockwise steps.</summary>
        public static Vector2Int Rotate(Vector2Int p, int rotation)
        {
            int r = ((rotation % 4) + 4) % 4;
            for (int i = 0; i < r; i++) p = new Vector2Int(-p.y, p.x);
            return p;
        }

        /// <summary>Local cell offsets (relative to the anchor) of a word of the given size.</summary>
        public static List<Vector2Int> LocalCells(ShapeFamily family, int size, int rotation)
        {
            var src = Families[(int)family];
            size = Mathf.Clamp(size, 1, MaxSize);
            var list = new List<Vector2Int>(size);
            for (int i = 0; i < size; i++) list.Add(Rotate(src[i], rotation));
            return list;
        }

        public static IEnumerable<Vector2Int> Cells(ShapeFamily family, int size, int rotation, Vector2Int anchor) =>
            LocalCells(family, size, rotation).Select(c => c + anchor);
    }

    public enum WordLocation { None, Grid, Wheelbarrow, Held, Shed }

    /// <summary>A word the player owns during a run.</summary>
    public class WordInstance
    {
        public int Id;
        public VocabEntry Entry;
        public WordStats Stats;
        public ShapeFamily Family;
        public int Rotation;
        public Vector2Int Anchor;
        public int Size;
        public WordLocation Location;
        public int WheelbarrowSlot = -1;
        public int PricePaid;
        public string TypedGerman;

        public bool HasArticleMod;
        public bool HasConjugationMod;
        public readonly bool[] ConjugationCorrect = new bool[6];

        public WordType Type => Entry.Type;
        public int CorrectForms => ConjugationCorrect.Count(b => b);
        public bool AllFormsCorrect => CorrectForms == 6;

        public IEnumerable<Vector2Int> Cells() => Shapes.Cells(Family, Size, Rotation, Anchor);
        public IEnumerable<Vector2Int> CellsAt(Vector2Int anchor, int rotation) => Shapes.Cells(Family, Size, rotation, anchor);
    }

    /// <summary>The plantable field. Origin is the bottom-left cell (0,0).</summary>
    public class GridModel
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public readonly int MaxWidth;
        public readonly int MaxHeight;
        /// <summary>Location assigned to words placed in this grid (Grid or Shed).</summary>
        public readonly WordLocation Location;
        readonly List<WordInstance> words = new List<WordInstance>();

        public IReadOnlyList<WordInstance> Words => words;

        public GridModel(int width, int height, int maxWidth, int maxHeight, WordLocation location = WordLocation.Grid)
        {
            Width = width; Height = height; MaxWidth = maxWidth; MaxHeight = maxHeight; Location = location;
        }

        public bool IsInside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public WordInstance OccupantAt(Vector2Int cell, WordInstance ignore = null, WordInstance ignore2 = null)
        {
            foreach (var w in words)
            {
                if (w == ignore || w == ignore2) continue;
                foreach (var c in w.Cells())
                    if (c == cell) return w;
            }
            return null;
        }

        public bool CanPlace(WordInstance word, Vector2Int anchor, int rotation, int? size = null, WordInstance ignore = null)
        {
            int s = size ?? word.Size;
            foreach (var c in Shapes.Cells(word.Family, s, rotation, anchor))
            {
                if (!IsInside(c)) return false;
                if (OccupantAt(c, word, ignore) != null) return false;
            }
            return true;
        }

        /// <summary>Distinct words (other than <paramref name="word"/>) that the placement would overlap.</summary>
        public List<WordInstance> Overlaps(WordInstance word, Vector2Int anchor, int rotation)
        {
            var list = new List<WordInstance>();
            foreach (var c in Shapes.Cells(word.Family, word.Size, rotation, anchor))
            {
                var o = OccupantAt(c, word);
                if (o != null && !list.Contains(o)) list.Add(o);
            }
            return list;
        }

        public bool AllInside(WordInstance word, Vector2Int anchor, int rotation) =>
            Shapes.Cells(word.Family, word.Size, rotation, anchor).All(IsInside);

        public bool TryPlace(WordInstance word, Vector2Int anchor, int rotation)
        {
            if (!CanPlace(word, anchor, rotation)) return false;
            word.Anchor = anchor;
            word.Rotation = ((rotation % 4) + 4) % 4;
            word.Location = Location;
            word.WheelbarrowSlot = -1;
            if (!words.Contains(word)) words.Add(word);
            return true;
        }

        public void Remove(WordInstance word) => words.Remove(word);

        public bool Contains(WordInstance word) => words.Contains(word);

        /// <summary>Find any free spot for the word (tries all rotations). Returns false if none.</summary>
        public bool FindFirstFit(WordInstance word, out Vector2Int anchor, out int rotation)
        {
            for (int r = 0; r < 4; r++)
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        var a = new Vector2Int(x, y);
                        if (CanPlace(word, a, r)) { anchor = a; rotation = r; return true; }
                    }
            anchor = default; rotation = 0;
            return false;
        }

        public bool CanAddColumn => Width < MaxWidth;
        public bool CanAddRow => Height < MaxHeight;
        public bool CanExpand => CanAddColumn || CanAddRow;

        public bool Expand(bool column)
        {
            if (column) { if (!CanAddColumn) return false; Width++; }
            else { if (!CanAddRow) return false; Height++; }
            return true;
        }

        public void SetSize(int width, int height) { Width = width; Height = height; }

        public void Clear() => words.Clear();
    }
}
