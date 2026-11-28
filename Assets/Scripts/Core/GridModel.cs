using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cosecha
{
    public enum WordLocation { None, Grid, Wheelbarrow, Held, Shed, Merged }

    /// <summary>
    /// A word the player owns during a run. Words are horizontal bars: <see cref="Size"/> consecutive cells in one row,
    /// starting at <see cref="Anchor"/> (the leftmost cell, which carries the sign).
    /// </summary>
    public class WordInstance
    {
        public int Id;
        public VocabEntry Entry;
        public WordStats Stats;
        public Vector2Int Anchor;
        public int Size;
        public WordLocation Location;
        public int WheelbarrowSlot = -1;
        public int PricePaid;
        public string TypedGerman;

        /// <summary>Verbs: index into the present forms (0 yo … 5 ellos), -1 = infinitive.</summary>
        public int VerbPerson = -1;
        /// <summary>Adjectives: 0 m. Sg. (lemma), 1 f. Sg., 2 m. Pl., 3 f. Pl.</summary>
        public int AdjectiveForm;
        /// <summary>Nouns: the article merged into this word. It keeps the noun's size but adds its points, gold and price.</summary>
        public WordInstance MergedArticle;

        public WordType Type => Entry.Type;
        public bool HasMergedArticle => MergedArticle != null;
        /// <summary>Base points including a merged article.</summary>
        public int BasePoints => Stats.BasePoints + (MergedArticle?.BasePoints ?? 0);
        /// <summary>Gold per round including a merged article.</summary>
        public int GoldYield => Stats.Gold + (MergedArticle?.GoldYield ?? 0);
        /// <summary>Price paid including a merged article (for the compost refund).</summary>
        public int TotalPricePaid => PricePaid + (MergedArticle?.TotalPricePaid ?? 0);
        public bool IsConjugated => Type == WordType.Verb && VerbPerson >= 0;
        public bool IsInflected => Type == WordType.Adjective && AdjectiveForm != 0;

        /// <summary>The form the word currently shows on its sign.</summary>
        public string Shown
        {
            get
            {
                if (IsConjugated) return Entry.PresentForms[VerbPerson];
                if (Type == WordType.Adjective && Entry.AdjectiveForms != null) return Entry.AdjectiveForms[AdjectiveForm];
                if (MergedArticle != null) return MergedArticle.Entry.Es + " " + Entry.Es;
                return Entry.Es;
            }
        }

        public IEnumerable<Vector2Int> Cells() => CellsAt(Anchor, Size);

        public static IEnumerable<Vector2Int> CellsAt(Vector2Int anchor, int size)
        {
            for (int i = 0; i < size; i++) yield return new Vector2Int(anchor.x + i, anchor.y);
        }

        /// <summary>The word as the sentence engine sees it.</summary>
        public Token ToToken()
        {
            var e = Entry;
            var t = new Token { Id = Id, Text = Shown, Kind = TokenKind.None };
            switch (Type)
            {
                case WordType.Pronoun:
                    t.Kind = TokenKind.Pronoun;
                    t.Person = e.Pronoun.person;
                    t.Plural = e.Pronoun.number == "pl";
                    t.Gender = e.Pronoun.gender[0];
                    break;
                case WordType.Noun:
                    t.Kind = TokenKind.Noun;
                    t.Person = 3;
                    t.Plural = e.Noun.number == "pl";
                    t.Gender = e.Noun.gender[0];
                    t.ArticleDefinite = e.Noun.articleDefinite;
                    t.ArticleIndefinite = e.Noun.articleIndefinite;
                    t.HasArticle = HasMergedArticle;
                    break;
                case WordType.Article:
                    t.Kind = TokenKind.Article;
                    t.Plural = e.Article.number == "pl";
                    t.Gender = e.Article.gender[0];
                    break;
                case WordType.Adjective:
                    t.Kind = TokenKind.Adjective;
                    t.Forms = e.AdjectiveForms;
                    break;
                case WordType.Verb:
                    if (!IsConjugated) break;   // infinitives match nothing
                    t.Kind = TokenKind.Verb;
                    t.Person = VerbPerson % 3 + 1;
                    t.Plural = VerbPerson >= 3;
                    t.Transitive = e.Verb.transitive;
                    t.Copula = e.Verb.copula;
                    break;
                case WordType.QuestionWord:
                    t.Kind = TokenKind.QuestionWord;
                    t.Role = e.QuestionWord.role == "subject" ? QuestionRole.Subject
                        : e.QuestionWord.role == "object" ? QuestionRole.Object : QuestionRole.Adverb;
                    break;
                case WordType.Conjunction:
                    t.Kind = TokenKind.Conjunction;
                    t.IsY = string.Equals(e.Es.Trim(), "y", StringComparison.OrdinalIgnoreCase);
                    break;
            }
            return t;
        }
    }

    /// <summary>The plantable field (or the shed). Cell (0,0) is the top-left cell; y grows downwards.</summary>
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
                if (cell.y == w.Anchor.y && cell.x >= w.Anchor.x && cell.x < w.Anchor.x + w.Size) return w;
            }
            return null;
        }

        public bool CanPlace(WordInstance word, Vector2Int anchor, int? size = null, WordInstance ignore = null)
        {
            foreach (var c in WordInstance.CellsAt(anchor, size ?? word.Size))
            {
                if (!IsInside(c)) return false;
                if (OccupantAt(c, word, ignore) != null) return false;
            }
            return true;
        }

        /// <summary>Distinct words (other than <paramref name="word"/>) that the placement would overlap.</summary>
        public List<WordInstance> Overlaps(WordInstance word, Vector2Int anchor)
        {
            var list = new List<WordInstance>();
            foreach (var c in WordInstance.CellsAt(anchor, word.Size))
            {
                var o = OccupantAt(c, word);
                if (o != null && !list.Contains(o)) list.Add(o);
            }
            return list;
        }

        public bool AllInside(WordInstance word, Vector2Int anchor) => WordInstance.CellsAt(anchor, word.Size).All(IsInside);

        public bool TryPlace(WordInstance word, Vector2Int anchor)
        {
            if (!CanPlace(word, anchor)) return false;
            word.Anchor = anchor;
            word.Location = Location;
            word.WheelbarrowSlot = -1;
            if (!words.Contains(word)) words.Add(word);
            return true;
        }

        public void Remove(WordInstance word) => words.Remove(word);

        public bool Contains(WordInstance word) => words.Contains(word);

        /// <summary>Find any free spot for the word (row by row, left to right). Returns false if none.</summary>
        public bool FindFirstFit(WordInstance word, out Vector2Int anchor)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var a = new Vector2Int(x, y);
                    if (CanPlace(word, a)) { anchor = a; return true; }
                }
            anchor = default;
            return false;
        }

        /// <summary>Grows the grid. New rows appear at the bottom, new columns at the right, so placed words keep their cells.</summary>
        public void SetSize(int width, int height)
        {
            Width = Math.Min(width, MaxWidth);
            Height = Math.Min(height, MaxHeight);
        }

        /// <summary>The field as engine input.</summary>
        public List<PlacedToken> ToTokens() =>
            words.Select(w => new PlacedToken(w.ToToken(), w.Anchor.x, w.Anchor.y, w.Size)).ToList();

        public void Clear() => words.Clear();
    }

    /// <summary>The field grows by one row or one column at the start of certain rounds.</summary>
    public static class GrowthSchedule
    {
        /// <summary>Field size (columns, rows) in the given round.</summary>
        public static (int w, int h) SizeAt(BalanceConfig cfg, int round)
        {
            int w = cfg.startGridWidth, h = cfg.startGridHeight;
            if (cfg.growthIntervals == null || cfg.growthIntervals.Length == 0 || cfg.growthOrder == null || cfg.growthOrder.Length == 0)
                return (w, h);
            int k = 0;
            int next = 1 + Math.Max(1, cfg.growthIntervals[0]);
            while (next <= round)
            {
                var axis = cfg.growthOrder[k % cfg.growthOrder.Length];
                bool rowFull = h >= cfg.maxGridHeight, colFull = w >= cfg.maxGridWidth;
                if (rowFull && colFull) break;
                if (axis == GrowthAxis.Row && rowFull) axis = GrowthAxis.Column;
                else if (axis == GrowthAxis.Column && colFull) axis = GrowthAxis.Row;
                if (axis == GrowthAxis.Row) h++; else w++;
                k++;
                next += Math.Max(1, cfg.growthIntervals[k % cfg.growthIntervals.Length]);
            }
            return (w, h);
        }
    }
}
