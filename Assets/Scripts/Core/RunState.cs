using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Cosecha
{
    public class HarvestWordScore
    {
        public WordInstance Word;
        public double Score;
        public Vector2Int Anchor;
    }

    public class HarvestResult
    {
        public int Round;
        public double Score;
        public double Target;
        public bool Passed;
        public bool ScarecrowSaved;
        public string PenaltyMessage;
        public int GoldIncome;
        public bool GameOver;
        public List<HarvestWordScore> WordScores = new List<HarvestWordScore>();
        /// <summary>Structures counted at harvest.</summary>
        public List<Structure> Structures = new List<Structure>();
        public int Count(StructureCategory c) => Structures.Count(s => s.Category == c);
    }

    public class RoundStats
    {
        public int GoldAtStart;
        public int WordsBought;
        public int Rerolls;
        public int GrammarActions;
        public int QuizAttempts;
        public int QuizFailures;
    }

    public enum ForcedOutcome { None, Pass, Fail }

    /// <summary>All state of one run plus the rules that change it. No Unity scene dependencies.</summary>
    public class RunState
    {
        public readonly BalanceConfig Cfg;
        public VocabularyDatabase Vocab { get; private set; }
        public readonly IRng Rng;
        public readonly ComplexityCalculator Complexity;
        public readonly ScoreCalculator Scoring;
        public readonly ShopGenerator ShopGen;
        public readonly SentenceEngine Engine;
        public AnswerChecker Checker { get; private set; }

        public string RunId { get; private set; }
        public int Round;
        public int Gold;
        public int GrammarActionsLeft;
        public bool ScarecrowAvailable;
        public double Rank;
        public int RerollPrice;
        /// <summary>True when the field grew at the start of the current round (for the banner).</summary>
        public bool FieldGrewThisRound;
        public GridModel Grid { get; private set; }
        public GridModel Shed { get; private set; }
        public WordInstance[] Wheelbarrow { get; private set; }
        public ShopState Shop { get; private set; } = new ShopState();
        public bool IsOver { get; private set; }

        public int WordsPlanted;
        /// <summary>Successful grammar actions (conjugations and inflections) in this run.</summary>
        public int GrammarLearned;
        public RoundStats CurrentRound = new RoundStats();

        int nextWordId = 1;

        public RunState(BalanceConfig cfg, VocabularyDatabase vocab, IRng rng)
        {
            Cfg = cfg;
            Vocab = vocab;
            Rng = rng;
            Complexity = new ComplexityCalculator(cfg);
            Scoring = new ScoreCalculator(cfg);
            ShopGen = new ShopGenerator(cfg, Complexity);
            Engine = CreateEngine(cfg);
            Checker = new AnswerChecker(cfg);
        }

        /// <summary>Builds the engine from the config's pattern list; invalid patterns are skipped with a warning.</summary>
        public static SentenceEngine CreateEngine(BalanceConfig cfg)
        {
            var list = new List<SentencePattern>();
            foreach (var def in cfg.sentencePatterns ?? Array.Empty<SentencePatternDef>())
            {
                try { list.Add(SentencePattern.Parse(def.category, def.sequence)); }
                catch (FormatException ex) { Debug.LogWarning("[Satzmuster] " + ex.Message); }
            }
            return new SentenceEngine(list, cfg.BonusOf);
        }

        public void SetVocabulary(VocabularyDatabase vocab) => Vocab = vocab;
        public void RefreshChecker() => Checker = new AnswerChecker(Cfg);

        // ---------------------------------------------------------------- run lifecycle

        public void StartNewRun()
        {
            RunId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Round = 1;
            Gold = Cfg.startGold;
            ScarecrowAvailable = true;
            Rank = 0;
            IsOver = false;
            WordsPlanted = 0;
            GrammarLearned = 0;
            Grid = new GridModel(Cfg.startGridWidth, Cfg.startGridHeight, Cfg.maxGridWidth, Cfg.maxGridHeight);
            Shed = new GridModel(Cfg.shedWidth, Cfg.shedHeight, Cfg.shedWidth, Cfg.shedHeight, WordLocation.Shed);
            Wheelbarrow = new WordInstance[Cfg.wheelbarrowSlots];
            PlaceStartingWords();
            BeginRound();
        }

        /// <summary>Start layout: pronoun at column 0, verb from column 1 of row 0, noun in the shed.</summary>
        static readonly (WordLocation where, Vector2Int anchor)[] StartLayout =
        {
            (WordLocation.Grid, new Vector2Int(0, 0)),
            (WordLocation.Grid, new Vector2Int(1, 0)),
            (WordLocation.Shed, new Vector2Int(0, 0)),
        };

        void PlaceStartingWords()
        {
            var used = new HashSet<string>();
            for (int i = 0; i < Cfg.startWordIds.Length; i++)
            {
                var entry = ResolveStartWord(i, used);
                if (entry == null) continue;
                used.Add(entry.Id);
                var layout = i < StartLayout.Length ? StartLayout[i] : (WordLocation.Grid, Vector2Int.zero);
                var w = CreateWord(entry, 0);
                var g = layout.Item1 == WordLocation.Shed ? Shed : Grid;
                if (g.TryPlace(w, layout.Item2)) continue;
                if (!StoreAnywhere(w, Grid)) StoreAnywhere(w, Shed);
            }
        }

        bool StoreAnywhere(WordInstance w, GridModel g) => g.FindFirstFit(w, out var a) && g.TryPlace(w, a);

        VocabEntry ResolveStartWord(int i, HashSet<string> used)
        {
            if (Cfg.randomizeStartVerbAndNoun && i < Cfg.startWordTypes.Length)
            {
                var random = RandomStartWord(Cfg.startWordTypes[i], used);
                if (random != null) return random;
            }
            var e = Vocab.Get(Cfg.startWordIds[i]);
            if (e != null && !used.Contains(e.Id)) return e;
            if (i >= Cfg.startWordTypes.Length) return null;
            var type = Cfg.startWordTypes[i];
            return Vocab.Entries.Where(x => x.Type == type && !used.Contains(x.Id))
                .OrderBy(x => Complexity.ComputeK(x)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>Random easy verb (transitive, so "pronoun verb noun" can form a Satz) or noun; null for other types.</summary>
        VocabEntry RandomStartWord(WordType type, HashSet<string> used)
        {
            if (type != WordType.Verb && type != WordType.Noun) return null;
            var pool = Vocab.Entries
                .Where(x => x.Type == type && !used.Contains(x.Id))
                .Where(x => type != WordType.Verb || (x.Verb != null && x.Verb.transitive && !x.Verb.copula))
                .OrderBy(x => Complexity.ComputeK(x)).ThenBy(x => x.Id, StringComparer.Ordinal)
                .Take(Math.Max(1, Cfg.startWordPoolSize))
                .ToList();
            return pool.Count == 0 ? null : pool[Rng.Range(0, pool.Count)];
        }

        void BeginRound()
        {
            var (w, h) = GrowthSchedule.SizeAt(Cfg, Round);
            FieldGrewThisRound = w > Grid.Width || h > Grid.Height;
            if (FieldGrewThisRound) Grid.SetSize(Math.Max(w, Grid.Width), Math.Max(h, Grid.Height));
            GrammarActionsLeft = Cfg.freeGrammarActionsPerRound;
            RerollPrice = Cfg.rerollStartPrice;
            CurrentRound = new RoundStats { GoldAtStart = Gold };
            RefillShop();
        }

        public double Target => Scoring.Target(Round);

        // ---------------------------------------------------------------- words

        public WordInstance CreateWord(VocabEntry entry, int pricePaid)
        {
            var stats = Complexity.Compute(entry);
            return new WordInstance
            {
                Id = nextWordId++,
                Entry = entry,
                Stats = stats,
                Size = stats.BaseSize,
                PricePaid = pricePaid,
                Location = WordLocation.None
            };
        }

        public IEnumerable<WordInstance> OwnedWords()
        {
            foreach (var w in Grid.Words) yield return w;
            foreach (var w in Shed.Words) yield return w;
            foreach (var w in Wheelbarrow) if (w != null) yield return w;
        }

        public HashSet<string> OwnedIds(WordInstance extra = null)
        {
            var set = new HashSet<string>(OwnedWords().Select(w => w.Entry.Id));
            if (extra != null) set.Add(extra.Entry.Id);
            return set;
        }

        // ---------------------------------------------------------------- sentences and score

        string analysisKey;
        SentenceAnalysis analysis;

        /// <summary>Structures on the field (cached until the field changes).</summary>
        public SentenceAnalysis Analysis
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Grid.Width).Append('x').Append(Grid.Height);
                foreach (var w in Grid.Words)
                    sb.Append('|').Append(w.Id).Append(',').Append(w.Anchor.x).Append(',').Append(w.Anchor.y).Append(',')
                      .Append(w.Size).Append(',').Append(w.VerbPerson).Append(',').Append(w.AdjectiveForm)
                      .Append(',').Append(w.MergedArticle?.Id ?? 0);
                string key = sb.ToString();
                if (key != analysisKey || analysis == null)
                {
                    analysis = Engine.Analyze(Grid.Width, Grid.Height, Grid.ToTokens());
                    analysisKey = key;
                }
                return analysis;
            }
        }

        public double RoundScorePreview => Scoring.RoundScore(Grid.Words, Analysis);
        public ScoreBreakdown ScoreOf(WordInstance w) =>
            Scoring.Score(w, w.Location == WordLocation.Grid ? Analysis : null);
        public int IncomePreview => Scoring.GoldIncome(Grid.Words);

        // ---------------------------------------------------------------- placement

        public int FreeWheelbarrowSlot() => Array.IndexOf(Wheelbarrow, null);

        public bool PutInFreeWheelbarrowSlot(WordInstance w)
        {
            int slot = FreeWheelbarrowSlot();
            if (slot < 0) return false;
            return PutInWheelbarrow(w, slot);
        }

        public bool PutInWheelbarrow(WordInstance w, int slot)
        {
            if (slot < 0 || slot >= Wheelbarrow.Length || Wheelbarrow[slot] != null) return false;
            Detach(w);
            Wheelbarrow[slot] = w;
            w.Location = WordLocation.Wheelbarrow;
            w.WheelbarrowSlot = slot;
            return true;
        }

        public bool PlaceOnGrid(WordInstance w, Vector2Int anchor) => PlaceIn(Grid, w, anchor);

        public bool PlaceIn(GridModel g, WordInstance w, Vector2Int anchor)
        {
            if (!g.CanPlace(w, anchor)) return false;
            Detach(w);
            return g.TryPlace(w, anchor);
        }

        /// <summary>Stores a word in the shed (first free spot). False if the shed is full.</summary>
        public bool StoreInShed(WordInstance w)
        {
            if (PutInFreeWheelbarrowSlot(w)) return true;
            return Shed.FindFirstFit(w, out var a) && PlaceIn(Shed, w, a);
        }

        /// <summary>The single word a placement would swap out, or null if no swap is possible.</summary>
        public WordInstance SwapCandidate(GridModel g, WordInstance held, Vector2Int anchor)
        {
            if (!g.AllInside(held, anchor)) return null;
            var overlaps = g.Overlaps(held, anchor);
            if (overlaps.Count != 1) return null;
            return g.CanPlace(held, anchor, null, overlaps[0]) ? overlaps[0] : null;
        }

        /// <summary>Hot swap: plants <paramref name="held"/> and returns the displaced word, which is now held.</summary>
        public WordInstance PlaceWithSwap(GridModel g, WordInstance held, Vector2Int anchor)
        {
            var other = SwapCandidate(g, held, anchor);
            if (other == null) return null;
            g.Remove(other);
            other.Location = WordLocation.Held;
            Detach(held);
            g.TryPlace(held, anchor);
            return other;
        }

        /// <summary>Removes a word from field/shed and marks it held by the cursor.</summary>
        public void PickUp(WordInstance w)
        {
            Detach(w);
            w.Location = WordLocation.Held;
        }

        void Detach(WordInstance w)
        {
            if (Grid.Contains(w)) Grid.Remove(w);
            if (Shed != null && Shed.Contains(w)) Shed.Remove(w);
            for (int i = 0; i < Wheelbarrow.Length; i++)
                if (Wheelbarrow[i] == w) Wheelbarrow[i] = null;
            w.WheelbarrowSlot = -1;
            w.Location = WordLocation.None;
        }

        public int CompostRefund(WordInstance w) => (int)Math.Floor(Math.Round(BalanceConfig.D(Cfg.discardRefund) * w.TotalPricePaid, 6));

        /// <summary>Discards a word and refunds part of its price. Returns the refund.</summary>
        public int Compost(WordInstance w, bool refund = true)
        {
            Detach(w);
            int r = refund ? CompostRefund(w) : 0;
            Gold += r;
            return r;
        }

        // ---------------------------------------------------------------- article merge

        /// <summary>Can <paramref name="article"/> merge into <paramref name="noun"/>? The noun must take that article
        /// (definite or indefinite) and must not carry one yet.</summary>
        public static bool CanMerge(WordInstance article, WordInstance noun) =>
            article != null && noun != null && article != noun &&
            article.Type == WordType.Article && article.MergedArticle == null &&
            noun.Type == WordType.Noun && noun.Entry.Noun != null && !noun.HasMergedArticle &&
            SentenceEngine.ArticleFits(article.Entry.Es, noun.ToToken());

        /// <summary>The word in <paramref name="cell"/> that the held article would merge into, or null.</summary>
        public WordInstance MergeCandidate(GridModel g, WordInstance held, Vector2Int cell)
        {
            if (!g.IsInside(cell)) return null;
            var target = g.OccupantAt(cell, held);
            return CanMerge(held, target) ? target : null;
        }

        /// <summary>Merges the article into the noun: the noun keeps its size and cells, points and gold add up.</summary>
        public bool MergeArticle(WordInstance article, WordInstance noun)
        {
            if (!CanMerge(article, noun)) return false;
            Detach(article);
            article.Location = WordLocation.Merged;
            noun.MergedArticle = article;
            return true;
        }

        // ---------------------------------------------------------------- shop

        public void RefillShop()
        {
            Shop = new ShopState();
            var owned = OwnedWords().Select(w => w.Entry).ToList();
            Shop.Words.AddRange(ShopGen.MakeWordOffers(Vocab.Entries, owned, Grid, Engine, Round, Rng, Shop.Completing));
            Shop.Joker = ShopGen.MakeJoker(Rng);
        }

        public bool CanAfford(int price) => Gold >= price;

        public bool TryReroll()
        {
            if (!CanAfford(RerollPrice)) return false;
            Gold -= RerollPrice;
            RerollPrice += Cfg.rerollPriceStep;
            CurrentRound.Rerolls++;
            RefillShop();
            return true;
        }

        public void RegisterQuizAttempt() => CurrentRound.QuizAttempts++;
        public void RegisterQuizFailure() => CurrentRound.QuizFailures++;

        /// <summary>Purchase (quiz passed, or no quiz for articles): pay and create the held word.</summary>
        public WordInstance CompleteWordPurchase(WordOffer offer, string typedGerman)
        {
            if (!offer.Available || !CanAfford(offer.Stats.Price)) return null;
            Gold -= offer.Stats.Price;
            offer.Sold = true;
            var w = CreateWord(offer.Entry, offer.Stats.Price);
            w.TypedGerman = typedGerman;
            w.Location = WordLocation.Held;
            WordsPlanted++;
            CurrentRound.WordsBought++;
            return w;
        }

        public void FailWordPurchase(WordOffer offer) => offer.Missed = true;

        public int JokerPrice(VocabEntry e) => Complexity.JokerPrice(Complexity.Compute(e).Price);

        /// <summary>Validates a joker input. Returns the entry or null with a reason.</summary>
        public VocabEntry ValidateJoker(string input, WordType type, WordInstance held, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(input)) { reason = UiText.JokerEmpty; return null; }
            var matches = Vocab.Entries.Where(e => Checker.MatchesSpanish(input, e)).ToList();
            if (matches.Count == 0) { reason = UiText.JokerUnknown; return null; }
            var ofType = matches.Where(e => e.Type == type).ToList();
            if (ofType.Count == 0) { reason = UiText.JokerWrongType(type); return null; }
            var owned = OwnedIds(held);
            var free = ofType.Where(e => e.Type.IsFunctionWord() || !owned.Contains(e.Id)).ToList();
            if (free.Count == 0) { reason = UiText.JokerOwned; return null; }
            // Prefer an exact-spelling match.
            return free.FirstOrDefault(e => Checker.CheckSpanish(input, e).Result == AnswerResult.Correct) ?? free[0];
        }

        public WordInstance BuyJoker(VocabEntry e)
        {
            int price = JokerPrice(e);
            if (Shop.Joker == null || !Shop.Joker.Available || !CanAfford(price)) return null;
            Gold -= price;
            Shop.Joker.Used = true;
            var w = CreateWord(e, price);
            w.Location = WordLocation.Held;
            WordsPlanted++;
            CurrentRound.WordsBought++;
            return w;
        }

        // ---------------------------------------------------------------- free grammar action

        /// <summary>Verbs and adjectives on the field can take the grammar action.</summary>
        public static bool IsGrammarTarget(WordInstance w) =>
            w != null && w.Location == WordLocation.Grid &&
            ((w.Type == WordType.Verb && w.Entry.PresentForms != null) || (w.Type == WordType.Adjective && w.Entry.AdjectiveForms != null));

        public bool CanUseGrammarAction(WordInstance w) => GrammarActionsLeft > 0 && IsGrammarTarget(w);

        public bool HasGrammarTarget => GrammarActionsLeft > 0 && Grid.Words.Any(IsGrammarTarget);

        /// <summary>Conjugation quiz finished. Success: the verb shows the form of <paramref name="person"/> (0–5);
        /// the first conjugation removes the infinitive cell. Failure: nothing changes. Both use the action.</summary>
        public void ApplyConjugation(WordInstance w, int person, bool success)
        {
            GrammarActionsLeft = Math.Max(0, GrammarActionsLeft - 1);
            CurrentRound.GrammarActions++;
            if (!success) { CurrentRound.QuizFailures++; return; }
            if (!w.IsConjugated) w.Size = Complexity.ConjugatedSize(w.Stats);
            w.VerbPerson = person;
            GrammarLearned++;
        }

        /// <summary>Inflection quiz finished. Success: the adjective shows form <paramref name="form"/> (0 msg, 1 fsg, 2 mpl, 3 fpl).</summary>
        public void ApplyInflection(WordInstance w, int form, bool success)
        {
            GrammarActionsLeft = Math.Max(0, GrammarActionsLeft - 1);
            CurrentRound.GrammarActions++;
            if (!success) { CurrentRound.QuizFailures++; return; }
            w.AdjectiveForm = form;
            GrammarLearned++;
        }

        // ---------------------------------------------------------------- harvest

        public HarvestResult Harvest(ForcedOutcome forced = ForcedOutcome.None)
        {
            var res = new HarvestResult { Round = Round, Target = Target };
            var a = Analysis;
            res.Structures.AddRange(a.Structures);
            foreach (var w in Grid.Words)
                res.WordScores.Add(new HarvestWordScore { Word = w, Score = Scoring.Score(w, a).Total, Anchor = w.Anchor });
            res.Score = res.WordScores.Sum(s => s.Score);
            res.Passed = forced == ForcedOutcome.Pass || (forced == ForcedOutcome.None && ScoreCalculator.Reaches(res.Score, res.Target));
            int income = Scoring.GoldIncome(Grid.Words);

            if (!res.Passed && ScarecrowAvailable)
            {
                ScarecrowAvailable = false;
                res.ScarecrowSaved = true;
                res.PenaltyMessage = ApplyScarecrowPenalty();
            }

            if (res.Passed || res.ScarecrowSaved)
            {
                Rank += res.Score;
                Gold += income;
                res.GoldIncome = income;
            }
            else
            {
                res.GameOver = true;
                IsOver = true;
            }
            return res;
        }

        public void AdvanceRound()
        {
            Round++;
            BeginRound();
        }

        /// <summary>
        /// The highest-scoring conjugated verb falls back to the infinitive and regrows its infinitive cell; otherwise the
        /// highest-scoring inflected adjective falls back to m. Sg.; otherwise nothing happens.
        /// </summary>
        public string ApplyScarecrowPenalty()
        {
            var verb = Grid.Words.Where(w => w.IsConjugated).OrderByDescending(w => ScoreOf(w).Total).FirstOrDefault();
            if (verb != null)
            {
                string shown = verb.Shown;
                verb.VerbPerson = -1;
                return UiText.ScarecrowVerb(shown, verb.Entry.Es) + " " + Regrow(verb, verb.Stats.BaseSize);
            }
            var adj = Grid.Words.Where(w => w.IsInflected).OrderByDescending(w => ScoreOf(w).Total).FirstOrDefault();
            if (adj != null)
            {
                string shown = adj.Shown;
                adj.AdjectiveForm = 0;
                return UiText.ScarecrowAdjective(shown, adj.Entry.Es);
            }
            return UiText.ScarecrowNoPenalty;
        }

        /// <summary>Grows a word at its right end; moves it to the shed or composts it if the cells are taken or outside.</summary>
        public string Regrow(WordInstance w, int newSize)
        {
            if (w.Location != WordLocation.Grid)
            {
                w.Size = newSize;
                return "";
            }
            if (Grid.CanPlace(w, w.Anchor, newSize))
            {
                w.Size = newSize;
                return UiText.RegrowInPlace;
            }
            w.Size = newSize;
            if (StoreInShed(w)) return UiText.RegrowToShed;
            Compost(w, refund: false);
            return UiText.RegrowComposted;
        }

        // ---------------------------------------------------------------- debug

        public void DebugSetRound(int round)
        {
            Round = Math.Max(1, round);
            var (w, h) = GrowthSchedule.SizeAt(Cfg, Round);
            Grid.SetSize(Math.Max(w, Grid.Width), Math.Max(h, Grid.Height));
            RefillShop();
        }
    }
}
