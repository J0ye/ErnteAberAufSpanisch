using System;
using System.Collections.Generic;
using System.Linq;

// Pure C# (no Unity dependencies): finds Spanish sentence structures on the field by matching word features
// against a small, data-driven set of patterns. The engine does not understand language.

namespace Cosecha
{
    /// <summary>Structure categories, in ascending priority (Frage beats Satz beats Satzteil).</summary>
    public enum StructureCategory { Satzteil, Satz, Frage }

    /// <summary>→ left to right, ↓ top to bottom, ↘ and ↗ (both left to right).</summary>
    public enum LineDirection { Right, Down, DownRight, UpRight }

    public enum TokenKind { None, Pronoun, Noun, Article, Adjective, Verb, QuestionWord, Conjunction }

    public enum QuestionRole { Subject, Object, Adverb }

    /// <summary>The features of one word on the field, as the engine sees them.</summary>
    public class Token
    {
        public int Id;
        /// <summary>None = matches nothing (other, phrase, infinitive).</summary>
        public TokenKind Kind;
        /// <summary>Form currently shown (for articles: the article itself).</summary>
        public string Text;
        public int Person;              // pronoun, noun (3), verb
        public bool Plural;             // pronoun, noun, article, verb
        public char Gender = 'm';       // 'm', 'f' or 'x' (pronouns only)
        public string ArticleDefinite;  // noun
        public string ArticleIndefinite;
        /// <summary>Noun with a merged article: already a full "article noun" NP and takes no further article.</summary>
        public bool HasArticle;
        public bool Transitive, Copula; // verb
        /// <summary>Adjective forms msg, fsg, mpl, fpl.</summary>
        public string[] Forms;
        public QuestionRole Role;       // question word
        public bool IsY;                // conjunction "y"

        public override string ToString() => Text;

        /// <summary>Spanish words this token stands for (a noun with a merged article counts as two).</summary>
        public int WordCount => HasArticle ? 2 : 1;

        public static int FormIndex(char gender, bool plural) => (gender == 'f' ? 1 : 0) + (plural ? 2 : 0);

        /// <summary>Adjective: does the shown form stand for this gender and number? Gender 'x' accepts m or f.</summary>
        public bool ShowsFormFor(char gender, bool plural)
        {
            if (Kind != TokenKind.Adjective || Forms == null) return false;
            if (gender == 'x')
                return Equal(Forms[FormIndex('m', plural)], Text) || Equal(Forms[FormIndex('f', plural)], Text);
            return Equal(Forms[FormIndex(gender, plural)], Text);
        }

        internal static bool Equal(string a, string b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A word bar on the field: <see cref="Size"/> cells starting at column X in row Y (row 0 = top).</summary>
    public class PlacedToken
    {
        public Token Token;
        public int X, Y, Size;
        public PlacedToken(Token t, int x, int y, int size) { Token = t; X = x; Y = y; Size = size; }
    }

    public class Structure
    {
        public StructureCategory Category;
        public double Bonus;
        public LineDirection Direction;
        public List<Token> Words;
        public string Pattern;

        /// <summary>Stable identity for view diffing.</summary>
        public string Key => $"{Category}|{Direction}|{string.Join(",", Words.Select(w => w.Id))}";
        public bool Contains(int id) => Words.Any(w => w.Id == id);
        public override string ToString() => $"{Category} {Direction}: {string.Join(" ", Words.Select(w => w.Text))}";
    }

    public class SentenceAnalysis
    {
        public readonly List<Structure> Structures = new List<Structure>();
        public readonly Dictionary<int, double> BonusByWord = new Dictionary<int, double>();

        public double Bonus(int id) => BonusByWord.TryGetValue(id, out var b) ? b : 0;
        public IEnumerable<Structure> StructuresOf(int id) => Structures.Where(s => s.Contains(id));
        public int Count(StructureCategory c) => Structures.Count(s => s.Category == c);
    }

    // ------------------------------------------------------------------ patterns (data)

    public enum PatternSymbol { Subject, Object, NP, NP2, Pronoun, Noun, Verb, Adjective, Question, Y }

    public class PatternElement
    {
        public PatternSymbol Symbol;
        public bool Optional;
        public bool Transitive, Intransitive, Copula, ThirdSingular;  // verb constraints
        public QuestionRole? Role;                                   // question constraint
    }

    /// <summary>
    /// A role sequence with a category, parsed from text such as "Subject V:transitive Object" or "Q:object V:transitive Subject?".
    /// Symbols: Subject, Object, NP, NP2 (NP of 2+ words or coordinated NP), Pronoun, Noun,
    /// V[:transitive|:intransitive|:copula|:3sg], Adjective, Q[:subject|:object|:adverb], Y. A trailing '?' marks an optional element.
    /// Agreement (subject–verb, copula–adjective) is checked in code.
    /// </summary>
    public class SentencePattern
    {
        public StructureCategory Category;
        public string Source;
        public readonly List<PatternElement> Elements = new List<PatternElement>();

        public static SentencePattern Parse(StructureCategory category, string text)
        {
            var p = new SentencePattern { Category = category, Source = text };
            foreach (var raw in (text ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var e = new PatternElement();
                string s = raw;
                if (s.EndsWith("?")) { e.Optional = true; s = s.Substring(0, s.Length - 1); }
                var parts = s.Split(':');
                string head = parts[0].ToLowerInvariant();
                switch (head)
                {
                    case "subject": e.Symbol = PatternSymbol.Subject; break;
                    case "object": e.Symbol = PatternSymbol.Object; break;
                    case "np": e.Symbol = PatternSymbol.NP; break;
                    case "np2": e.Symbol = PatternSymbol.NP2; break;
                    case "pronoun": e.Symbol = PatternSymbol.Pronoun; break;
                    case "noun": e.Symbol = PatternSymbol.Noun; break;
                    case "v": case "verb": e.Symbol = PatternSymbol.Verb; break;
                    case "adj": case "adjective": e.Symbol = PatternSymbol.Adjective; break;
                    case "q": case "question": e.Symbol = PatternSymbol.Question; break;
                    case "y": e.Symbol = PatternSymbol.Y; break;
                    default: throw new FormatException($"Unbekanntes Mustersymbol '{raw}' in '{text}'.");
                }
                for (int i = 1; i < parts.Length; i++)
                {
                    switch (parts[i].ToLowerInvariant())
                    {
                        case "transitive": e.Transitive = true; break;
                        case "intransitive": e.Intransitive = true; break;
                        case "copula": e.Copula = true; break;
                        case "3sg": e.ThirdSingular = true; break;
                        case "subject": e.Role = QuestionRole.Subject; break;
                        case "object": e.Role = QuestionRole.Object; break;
                        case "adverb": e.Role = QuestionRole.Adverb; break;
                        default: throw new FormatException($"Unbekannte Einschränkung '{parts[i]}' in '{text}'.");
                    }
                }
                p.Elements.Add(e);
            }
            if (p.Elements.Count == 0) throw new FormatException("Leeres Muster.");
            return p;
        }
    }

    // ------------------------------------------------------------------ engine

    public class SentenceEngine
    {
        struct Agr
        {
            public int Person;
            public bool Plural;
            public char Gender;
        }

        /// <summary>Bindings collected while matching one pattern.</summary>
        struct Bind
        {
            public bool HasSubject;
            public Agr Subject;
            public Token Verb;
            public Token Adjective;
        }

        readonly List<SentencePattern> patterns;
        readonly Func<StructureCategory, double> bonus;

        public IReadOnlyList<SentencePattern> Patterns => patterns;

        public SentenceEngine(IEnumerable<SentencePattern> patterns, Func<StructureCategory, double> bonus)
        {
            this.patterns = patterns.ToList();
            this.bonus = bonus;
        }

        static readonly (int dx, int dy)[] Steps = { (1, 0), (0, 1), (1, 1), (1, -1) };

        /// <summary>All word sequences on the field (duplicates removed), with their direction.</summary>
        public static List<(LineDirection dir, List<Token> tokens)> Sequences(int width, int height, IEnumerable<PlacedToken> words)
        {
            var grid = new Token[width, height];
            foreach (var w in words)
                for (int i = 0; i < w.Size; i++)
                {
                    int x = w.X + i;
                    if (x >= 0 && x < width && w.Y >= 0 && w.Y < height) grid[x, w.Y] = w.Token;
                }

            var result = new List<(LineDirection, List<Token>)>();
            // Duplicates are dropped across all directions: with horizontal bars, a vertical neighbor pair that overlaps in
            // two columns is also a diagonal neighbor pair; it is one physical adjacency and must count once.
            var seen = new HashSet<string>();
            for (int d = 0; d < 4; d++)
            {
                var (dx, dy) = Steps[d];
                foreach (var (sx, sy) in LineStarts(d, width, height))
                {
                    var seq = new List<Token>();
                    int x = sx, y = sy;
                    while (true)
                    {
                        bool inside = x >= 0 && y >= 0 && x < width && y < height;
                        var t = inside ? grid[x, y] : null;
                        if (t == null)
                        {
                            if (seq.Count > 0)
                            {
                                if (seen.Add(string.Join(",", seq.Select(s => s.Id)))) result.Add(((LineDirection)d, seq));
                                seq = new List<Token>();
                            }
                            if (!inside) break;
                        }
                        else if (seq.Count == 0 || seq[seq.Count - 1] != t) seq.Add(t);
                        x += dx; y += dy;
                    }
                }
            }
            return result;
        }

        static IEnumerable<(int, int)> LineStarts(int dir, int w, int h)
        {
            switch ((LineDirection)dir)
            {
                case LineDirection.Right:
                    for (int y = 0; y < h; y++) yield return (0, y);
                    break;
                case LineDirection.Down:
                    for (int x = 0; x < w; x++) yield return (x, 0);
                    break;
                case LineDirection.DownRight:
                    for (int y = h - 1; y >= 0; y--) yield return (0, y);
                    for (int x = 1; x < w; x++) yield return (x, 0);
                    break;
                case LineDirection.UpRight:
                    for (int y = 0; y < h; y++) yield return (0, y);
                    for (int x = 1; x < w; x++) yield return (x, h - 1);
                    break;
            }
        }

        /// <summary>
        /// Analyzes the field. With <paramref name="onlyWithId"/>, only sequences containing that word are matched
        /// (used for the shop's virtual-token lookahead).
        /// </summary>
        public SentenceAnalysis Analyze(int width, int height, IEnumerable<PlacedToken> words, int? onlyWithId = null)
        {
            var a = new SentenceAnalysis();
            foreach (var (dir, seq) in Sequences(width, height, words))
            {
                if (seq.Sum(t => t.WordCount) < 2) continue;
                if (onlyWithId.HasValue && !seq.Any(t => t.Id == onlyWithId.Value)) continue;
                foreach (var s in MatchSequence(seq, dir))
                {
                    a.Structures.Add(s);
                    foreach (var w in s.Words)
                        a.BonusByWord[w.Id] = a.Bonus(w.Id) + s.Bonus;
                }
            }
            return a;
        }

        /// <summary>All contiguous matches in one sequence, reduced to a non-overlapping set (greedy).</summary>
        public List<Structure> MatchSequence(IList<Token> seq, LineDirection dir)
        {
            var matches = new List<(int start, int end, int pattern)>();
            for (int start = 0; start < seq.Count; start++)
                for (int p = 0; p < patterns.Count; p++)
                    foreach (int end in MatchPattern(patterns[p], seq, start).Distinct())
                        if (end > start) matches.Add((start, end, p));

            var ordered = matches
                .OrderByDescending(m => (int)patterns[m.pattern].Category)
                .ThenByDescending(m => m.end - m.start)
                .ThenBy(m => m.start)
                .ThenBy(m => m.pattern);
            var used = new bool[seq.Count];
            var chosen = new List<Structure>();
            foreach (var m in ordered)
            {
                bool free = true;
                for (int i = m.start; i < m.end; i++) if (used[i]) { free = false; break; }
                if (!free) continue;
                for (int i = m.start; i < m.end; i++) used[i] = true;
                var cat = patterns[m.pattern].Category;
                chosen.Add(new Structure
                {
                    Category = cat,
                    Bonus = bonus(cat),
                    Direction = dir,
                    Words = seq.Skip(m.start).Take(m.end - m.start).ToList(),
                    Pattern = patterns[m.pattern].Source
                });
            }
            return chosen.OrderBy(s => seq.IndexOf(s.Words[0])).ToList();
        }

        /// <summary>End positions (exclusive) of every way <paramref name="p"/> matches starting at <paramref name="start"/>.</summary>
        IEnumerable<int> MatchPattern(SentencePattern p, IList<Token> seq, int start)
        {
            foreach (var (end, b) in MatchFrom(p, 0, seq, start, new Bind()))
                if (Agrees(b)) yield return end;
        }

        IEnumerable<(int end, Bind bind)> MatchFrom(SentencePattern p, int elem, IList<Token> seq, int pos, Bind b)
        {
            if (elem == p.Elements.Count) { yield return (pos, b); yield break; }
            var e = p.Elements[elem];
            foreach (var (end, nb) in MatchElement(e, seq, pos, b))
                foreach (var r in MatchFrom(p, elem + 1, seq, end, nb))
                    yield return r;
            if (e.Optional)
                foreach (var r in MatchFrom(p, elem + 1, seq, pos, b))
                    yield return r;
        }

        IEnumerable<(int end, Bind bind)> MatchElement(PatternElement e, IList<Token> seq, int pos, Bind b)
        {
            if (pos >= seq.Count) yield break;
            var t = seq[pos];
            switch (e.Symbol)
            {
                case PatternSymbol.Subject:
                    foreach (var (end, agr) in Subject(seq, pos))
                    {
                        var nb = b; nb.HasSubject = true; nb.Subject = agr;
                        yield return (end, nb);
                    }
                    break;
                case PatternSymbol.Object:
                    foreach (var (end, _) in NominalPhrases(seq, pos, true)) yield return (end, b);
                    break;
                case PatternSymbol.NP:
                    foreach (var (end, _) in NP(seq, pos)) yield return (end, b);
                    break;
                case PatternSymbol.NP2:
                    foreach (var (end, _) in NominalPhrases(seq, pos, true))
                        if (WordsBetween(seq, pos, end) >= 2) yield return (end, b);
                    break;
                case PatternSymbol.Pronoun:
                    if (t.Kind == TokenKind.Pronoun) yield return (pos + 1, b);
                    break;
                case PatternSymbol.Noun:
                    if (t.Kind == TokenKind.Noun) yield return (pos + 1, b);
                    break;
                case PatternSymbol.Verb:
                    if (t.Kind != TokenKind.Verb) break;
                    if (e.Transitive && !t.Transitive) break;
                    if (e.Copula && !t.Copula) break;
                    if (e.Intransitive && (t.Transitive || t.Copula)) break;
                    if (e.ThirdSingular && (t.Person != 3 || t.Plural)) break;
                    { var nb = b; nb.Verb = t; yield return (pos + 1, nb); }
                    break;
                case PatternSymbol.Adjective:
                    if (t.Kind == TokenKind.Adjective) { var nb = b; nb.Adjective = t; yield return (pos + 1, nb); }
                    break;
                case PatternSymbol.Question:
                    if (t.Kind == TokenKind.QuestionWord && (!e.Role.HasValue || e.Role.Value == t.Role)) yield return (pos + 1, b);
                    break;
                case PatternSymbol.Y:
                    if (t.Kind == TokenKind.Conjunction && t.IsY) yield return (pos + 1, b);
                    break;
            }
        }

        static bool Agrees(Bind b)
        {
            if (b.HasSubject && b.Verb != null)
                if (b.Subject.Person != b.Verb.Person || b.Subject.Plural != b.Verb.Plural) return false;
            if (b.Adjective != null && b.HasSubject && (b.Verb == null || b.Verb.Copula))
                if (!b.Adjective.ShowsFormFor(b.Subject.Gender, b.Subject.Plural)) return false;
            return true;
        }

        // ------------------------------------------------------------ constituents

        static IEnumerable<(int end, Agr agr)> Subject(IList<Token> seq, int i)
        {
            var t = seq[i];
            if (t.Kind == TokenKind.Pronoun)
                yield return (i + 1, new Agr { Person = t.Person, Plural = t.Plural, Gender = t.Gender });
            foreach (var r in NominalPhrases(seq, i, true)) yield return r;
        }

        /// <summary>NP, plus coordinated NP (NP y NP) when <paramref name="coordinated"/>.</summary>
        static IEnumerable<(int end, Agr agr)> NominalPhrases(IList<Token> seq, int i, bool coordinated)
        {
            foreach (var np in NP(seq, i))
            {
                yield return np;
                if (!coordinated) continue;
                int y = np.end;
                if (y < seq.Count && seq[y].Kind == TokenKind.Conjunction && seq[y].IsY && y + 1 < seq.Count)
                    foreach (var np2 in NP(seq, y + 1))
                        yield return (np2.end, new Agr
                        {
                            Person = 3,
                            Plural = true,
                            Gender = np.agr.Gender == 'm' || np2.agr.Gender == 'm' ? 'm' : 'f'
                        });
            }
        }

        static int WordsBetween(IList<Token> seq, int start, int end)
        {
            int n = 0;
            for (int i = start; i < end; i++) n += seq[i].WordCount;
            return n;
        }

        /// <summary>Is <paramref name="article"/> the definite or indefinite article of the noun?</summary>
        public static bool ArticleFits(string article, Token noun) =>
            Token.Equal(article, noun.ArticleDefinite) || Token.Equal(article, noun.ArticleIndefinite);

        /// <summary>NP = [article] noun [adjective]; the noun may carry a merged article instead.</summary>
        static IEnumerable<(int end, Agr agr)> NP(IList<Token> seq, int i)
        {
            int n = i;
            Token article = null;
            if (seq[i].Kind == TokenKind.Article) { article = seq[i]; n = i + 1; }
            if (n >= seq.Count || seq[n].Kind != TokenKind.Noun) yield break;
            var noun = seq[n];
            if (article != null && (noun.HasArticle || !ArticleFits(article.Text, noun)))
                yield break;
            var agr = new Agr { Person = 3, Plural = noun.Plural, Gender = noun.Gender };
            yield return (n + 1, agr);
            if (n + 1 < seq.Count && seq[n + 1].ShowsFormFor(noun.Gender, noun.Plural))
                yield return (n + 2, agr);
        }
    }
}
