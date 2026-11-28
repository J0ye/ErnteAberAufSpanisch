using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cosecha
{
    public interface IRng
    {
        /// <summary>[0,1)</summary>
        double NextDouble();
        /// <summary>[min,max)</summary>
        int Range(int min, int max);
    }

    public class SystemRng : IRng
    {
        readonly System.Random r;
        public SystemRng(int? seed = null) { r = seed.HasValue ? new System.Random(seed.Value) : new System.Random(); }
        public double NextDouble() => r.NextDouble();
        public int Range(int min, int max) => r.Next(min, max);
    }

    /// <summary>Roles for the shop's need weighting.</summary>
    public enum NeedRole { Pronoun, Verb, Noun, Function, Rest }

    public class WordOffer
    {
        public VocabEntry Entry;
        public WordStats Stats;
        public bool SpanishToGerman;
        /// <summary>Articles are bought without a quiz.</summary>
        public bool NoQuiz => Entry.Type == WordType.Article;
        /// <summary>Prompt shown in the quiz (first German entry when the direction is German → Spanish).</summary>
        public string Prompt => SpanishToGerman ? Entry.Es : Entry.De[0];
        public bool Sold;
        public bool Missed;
        public bool Available => !Sold && !Missed;
    }

    public class JokerOffer
    {
        public WordType Type;
        public int AttemptsLeft;
        public bool Used;
        public bool Available => !Used && AttemptsLeft > 0;
    }

    public class ShopState
    {
        public readonly List<WordOffer> Words = new List<WordOffer>();
        public JokerOffer Joker;
        /// <summary>Entry ids that would complete an almost-structure on the field (for the tooltip and tests).</summary>
        public readonly HashSet<string> Completing = new HashSet<string>();
    }

    public class ShopGenerator
    {
        public static readonly WordType[] JokerTypes = { WordType.Noun, WordType.Verb, WordType.Adjective, WordType.Phrase };

        readonly BalanceConfig cfg;
        readonly ComplexityCalculator complexity;

        public ShopGenerator(BalanceConfig cfg, ComplexityCalculator complexity)
        {
            this.cfg = cfg;
            this.complexity = complexity;
        }

        public int ComplexityGate(int round) => cfg.complexityGateBase + (int)Math.Floor(round / (double)cfg.complexityGateDivisor);

        public static NeedRole RoleOf(WordType t)
        {
            switch (t)
            {
                case WordType.Pronoun: return NeedRole.Pronoun;
                case WordType.Verb: return NeedRole.Verb;
                case WordType.Noun: return NeedRole.Noun;
                case WordType.Article:
                case WordType.Conjunction:
                case WordType.QuestionWord: return NeedRole.Function;
                default: return NeedRole.Rest;
            }
        }

        float[] TargetRatio() => new[] { cfg.ratioPronoun, cfg.ratioVerb, cfg.ratioNoun, cfg.ratioFunction, cfg.ratioRest };

        /// <summary>Roles sorted by deficit (target share − owned share), largest first. Ties keep the role order.</summary>
        public List<NeedRole> RolesByDeficit(IEnumerable<WordType> ownedTypes)
        {
            var counts = new int[5];
            foreach (var t in ownedTypes) counts[(int)RoleOf(t)]++;
            int total = counts.Sum();
            var ratio = TargetRatio();
            double ratioSum = ratio.Sum();
            var deficit = new double[5];
            for (int i = 0; i < 5; i++)
                deficit[i] = (ratioSum > 0 ? ratio[i] / ratioSum : 0) - (total > 0 ? counts[i] / (double)total : 0);
            return Enumerable.Range(0, 5).OrderByDescending(i => Math.Round(deficit[i], 9)).ThenBy(i => i).Select(i => (NeedRole)i).ToList();
        }

        /// <summary>Entries the shop may offer: not owned (function words always), content words and phrases within the gate.</summary>
        public List<VocabEntry> Eligible(IEnumerable<VocabEntry> vocab, ICollection<string> ownedIds, int round)
        {
            int gate = ComplexityGate(round);
            return vocab.Where(e => e.Type.IsFunctionWord() || (!ownedIds.Contains(e.Id) && complexity.ComputeK(e) <= gate)).ToList();
        }

        // ------------------------------------------------------------ almost-structures

        /// <summary>Virtual tokens a candidate could become (verbs in every person, adjectives in every form).</summary>
        static IEnumerable<Token> Variants(VocabEntry e)
        {
            if (e.Type == WordType.Verb)
            {
                for (int p = 0; p < 6; p++) yield return new WordInstance { Id = -1, Entry = e, VerbPerson = p }.ToToken();
            }
            else if (e.Type == WordType.Adjective)
            {
                for (int f = 0; f < 4; f++) yield return new WordInstance { Id = -1, Entry = e, AdjectiveForm = f }.ToToken();
            }
            else yield return new WordInstance { Id = -1, Entry = e }.ToToken();
        }

        static string Signature(Token t) =>
            $"{t.Kind}|{t.Person}|{t.Plural}|{t.Gender}|{t.ArticleDefinite}|{t.ArticleIndefinite}|{t.Transitive}|{t.Copula}|{t.Role}|{t.IsY}|" +
            (t.Kind == TokenKind.Adjective ? $"{Array.IndexOf(t.Forms, t.Text)}|{string.Join("/", t.Forms)}" : t.Text);

        /// <summary>
        /// Ids of candidates that complete an almost-structure: a sequence on the field that becomes a Satz or Frage with
        /// exactly one more word at its start or end, placed in a free neighboring cell. Found by running the engine with
        /// a virtual one-cell token in every free cell next to a planted word.
        /// </summary>
        public HashSet<string> CompletingEntries(IEnumerable<VocabEntry> candidates, GridModel field, SentenceEngine engine)
        {
            var result = new HashSet<string>();
            if (field == null || field.Words.Count == 0) return result;
            var tokens = field.ToTokens();
            var existing = new HashSet<string>(engine.Analyze(field.Width, field.Height, tokens).Structures
                .Where(s => s.Category >= StructureCategory.Satz).Select(s => string.Join(",", s.Words.Select(w => w.Id))));

            var freeCells = new List<Vector2Int>();
            for (int y = 0; y < field.Height; y++)
                for (int x = 0; x < field.Width; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (field.OccupantAt(c) != null) continue;
                    bool nextToWord = false;
                    for (int dy = -1; dy <= 1 && !nextToWord; dy++)
                        for (int dx = -1; dx <= 1 && !nextToWord; dx++)
                        {
                            var n = new Vector2Int(x + dx, y + dy);
                            if ((dx != 0 || dy != 0) && field.IsInside(n) && field.OccupantAt(n) != null) nextToWord = true;
                        }
                    if (nextToWord) freeCells.Add(c);
                }
            if (freeCells.Count == 0) return result;

            var cache = new Dictionary<string, bool>();
            foreach (var e in candidates)
            {
                foreach (var v in Variants(e))
                {
                    string sig = Signature(v);
                    if (!cache.TryGetValue(sig, out bool ok))
                    {
                        ok = freeCells.Any(c => Completes(engine, field, tokens, v, c, existing));
                        cache[sig] = ok;
                    }
                    if (ok) { result.Add(e.Id); break; }
                }
            }
            return result;
        }

        static bool Completes(SentenceEngine engine, GridModel field, List<PlacedToken> tokens, Token virt, Vector2Int cell, HashSet<string> existing)
        {
            var list = new List<PlacedToken>(tokens) { new PlacedToken(virt, cell.x, cell.y, 1) };
            foreach (var s in engine.Analyze(field.Width, field.Height, list, virt.Id).Structures)
            {
                if (s.Category < StructureCategory.Satz) continue;
                int i = s.Words.FindIndex(w => w.Id == virt.Id);
                if (i != 0 && i != s.Words.Count - 1) continue;
                var rest = string.Join(",", s.Words.Where(w => w.Id != virt.Id).Select(w => w.Id));
                if (s.Words.Count >= 2 && !existing.Contains(rest)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ offers

        public List<VocabEntry> DrawWords(IEnumerable<VocabEntry> vocab, IList<VocabEntry> owned, GridModel field, SentenceEngine engine,
            int round, int count, IRng rng, HashSet<string> completingOut = null)
        {
            var all = vocab.ToList();
            var ownedIds = new HashSet<string>(owned.Select(e => e.Id));
            var pool = Eligible(all, ownedIds, round);
            var roles = RolesByDeficit(owned.Select(e => e.Type));
            var completing = engine != null ? CompletingEntries(pool, field, engine) : new HashSet<string>();
            completingOut?.UnionWith(completing);

            double Weight(VocabEntry e)
            {
                double w = 1;
                var r = RoleOf(e.Type);
                if (r == roles[0]) w *= cfg.largestDeficitWeight;
                else if (r == roles[1]) w *= cfg.secondDeficitWeight;
                if (completing.Contains(e.Id)) w *= cfg.almostStructureWeight;
                return w;
            }

            var result = new List<VocabEntry>();
            void Pick(IEnumerable<VocabEntry> from)
            {
                var list = from.Where(e => !result.Contains(e)).ToList();
                if (list.Count == 0 || result.Count >= count) return;
                double total = list.Sum(Weight);
                double roll = rng.NextDouble() * total;
                foreach (var e in list)
                {
                    roll -= Weight(e);
                    if (roll < 0) { result.Add(e); return; }
                }
                result.Add(list[list.Count - 1]);
            }

            // guarantees: one offer completes an almost-structure, one offer fills the largest deficit
            Pick(pool.Where(e => completing.Contains(e.Id)));
            if (!result.Any(e => RoleOf(e.Type) == roles[0])) Pick(pool.Where(e => RoleOf(e.Type) == roles[0]));
            while (result.Count < count && pool.Any(e => !result.Contains(e))) Pick(pool);

            if (result.Count < count)
            {
                // fill up with the lowest-K words above the gate
                var rest = all.Where(e => !ownedIds.Contains(e.Id) && !result.Contains(e))
                    .OrderBy(e => complexity.ComputeK(e)).ThenBy(e => e.Id, StringComparer.Ordinal);
                result.AddRange(rest.Take(count - result.Count));
            }
            Shuffle(result, rng);
            return result;
        }

        public List<WordOffer> MakeWordOffers(IEnumerable<VocabEntry> vocab, IList<VocabEntry> owned, GridModel field, SentenceEngine engine,
            int round, IRng rng, HashSet<string> completingOut = null)
        {
            return DrawWords(vocab, owned, field, engine, round, cfg.wordOfferCount, rng, completingOut)
                .Select(e => new WordOffer
                {
                    Entry = e,
                    Stats = complexity.Compute(e),
                    // German "sie", "Sie", "wir", "ihr" fit several Spanish pronouns: always ask Spanish → German
                    SpanishToGerman = e.Type == WordType.Pronoun || rng.NextDouble() < cfg.spanishToGermanChance
                }).ToList();
        }

        public JokerOffer MakeJoker(IRng rng) => new JokerOffer
        {
            Type = JokerTypes[rng.Range(0, JokerTypes.Length)],
            AttemptsLeft = cfg.jokerAttempts
        };

        static void Shuffle<T>(IList<T> list, IRng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
