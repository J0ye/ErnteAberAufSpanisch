using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    public class RoundStats
    {
        public int GoldAtStart;
        public int WordsBought;
        public int Rerolls;
        public int ModsApplied;
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
        public AnswerChecker Checker { get; private set; }

        public string RunId { get; private set; }
        public int Round;
        public int Gold;
        public int MaxActions;
        public int ActionsLeft;
        public bool ScarecrowAvailable;
        public double Rank;
        public int RerollPrice;
        public GridModel Grid { get; private set; }
        public GridModel Shed { get; private set; }
        public WordInstance[] Wheelbarrow { get; private set; }
        public readonly UpgradeState Upgrades = new UpgradeState();
        public ShopState Shop { get; private set; } = new ShopState();
        public bool IsOver { get; private set; }

        public int WordsPlanted;
        public int ModsLearned;
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
            Checker = new AnswerChecker(cfg);
        }

        public void SetVocabulary(VocabularyDatabase vocab) => Vocab = vocab;
        public void RefreshChecker() => Checker = new AnswerChecker(Cfg);

        // ---------------------------------------------------------------- run lifecycle

        public void StartNewRun()
        {
            RunId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Round = 1;
            Gold = Cfg.startGold;
            MaxActions = Cfg.startActions;
            ActionsLeft = MaxActions;
            ScarecrowAvailable = true;
            Rank = 0;
            IsOver = false;
            WordsPlanted = 0;
            ModsLearned = 0;
            Upgrades.Reset();
            Grid = new GridModel(Cfg.startGridWidth, Cfg.startGridHeight, Cfg.maxGridWidth, Cfg.maxGridHeight);
            Shed = new GridModel(Cfg.shedWidth, Cfg.shedHeight, Cfg.shedWidth, Cfg.shedHeight, WordLocation.Shed);
            Wheelbarrow = new WordInstance[Cfg.wheelbarrowSlots];
            PlaceStartingWords();
            BeginRound();
        }

        static readonly (ShapeFamily family, Vector2Int anchor, int rot)[] StartLayout =
        {
            (ShapeFamily.Line, new Vector2Int(0, 0), 0),
            (ShapeFamily.Block, new Vector2Int(0, 1), 0),
            (ShapeFamily.Corner, new Vector2Int(2, 1), 0),
            (ShapeFamily.Line, new Vector2Int(0, 3), 0),
        };

        void PlaceStartingWords()
        {
            var used = new HashSet<string>();
            for (int i = 0; i < Cfg.startWordIds.Length; i++)
            {
                var entry = ResolveStartWord(i, used);
                if (entry == null) continue;
                used.Add(entry.Id);
                var layout = i < StartLayout.Length ? StartLayout[i] : (family: ShapeFamily.Block, anchor: Vector2Int.zero, rot: 0);
                var w = CreateWord(entry, layout.family, 0);
                if (!Grid.TryPlace(w, layout.anchor, layout.rot))
                {
                    if (Grid.FindFirstFit(w, out var a, out var r)) Grid.TryPlace(w, a, r);
                    else PutInFreeWheelbarrowSlot(w);
                }
            }
        }

        VocabEntry ResolveStartWord(int i, HashSet<string> used)
        {
            var e = Vocab.Get(Cfg.startWordIds[i]);
            if (e != null && !used.Contains(e.Id)) return e;
            if (i >= Cfg.startWordTypes.Length) return null;
            var type = Cfg.startWordTypes[i];
            return Vocab.Entries.Where(x => x.Type == type && !used.Contains(x.Id))
                .OrderBy(x => Complexity.ComputeK(x)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        void BeginRound()
        {
            ActionsLeft = MaxActions;
            RerollPrice = Cfg.rerollStartPrice;
            CurrentRound = new RoundStats { GoldAtStart = Gold };
            RefillShop();
        }

        public double Target => Scoring.Target(Round);

        // ---------------------------------------------------------------- words

        public WordInstance CreateWord(VocabEntry entry, ShapeFamily family, int pricePaid)
        {
            var stats = Complexity.Compute(entry);
            return new WordInstance
            {
                Id = nextWordId++,
                Entry = entry,
                Stats = stats,
                Family = family,
                Size = stats.UnmoddedSize,
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

        public double RoundScorePreview => Scoring.RoundScore(Grid.Words, Upgrades);
        public ScoreBreakdown ScoreOf(WordInstance w) => Scoring.Score(w, Grid.Words, Upgrades);
        public int IncomePreview => Scoring.GoldIncome(Grid.Words);

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

        public bool PlaceOnGrid(WordInstance w, Vector2Int anchor, int rotation) => PlaceIn(Grid, w, anchor, rotation);

        public bool PlaceIn(GridModel g, WordInstance w, Vector2Int anchor, int rotation)
        {
            if (!g.CanPlace(w, anchor, rotation)) return false;
            Detach(w);
            return g.TryPlace(w, anchor, rotation);
        }

        /// <summary>The single word a placement would swap out, or null if no swap is possible.</summary>
        public WordInstance SwapCandidate(GridModel g, WordInstance held, Vector2Int anchor, int rotation)
        {
            if (!g.AllInside(held, anchor, rotation)) return null;
            var overlaps = g.Overlaps(held, anchor, rotation);
            if (overlaps.Count != 1) return null;
            return g.CanPlace(held, anchor, rotation, null, overlaps[0]) ? overlaps[0] : null;
        }

        /// <summary>Hot swap: plants <paramref name="held"/> and returns the displaced word, which is now held.</summary>
        public WordInstance PlaceWithSwap(GridModel g, WordInstance held, Vector2Int anchor, int rotation)
        {
            var other = SwapCandidate(g, held, anchor, rotation);
            if (other == null) return null;
            g.Remove(other);
            other.Location = WordLocation.Held;
            Detach(held);
            g.TryPlace(held, anchor, rotation);
            return other;
        }

        /// <summary>Removes a word from grid/wheelbarrow and marks it held by the cursor.</summary>
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

        public int CompostRefund(WordInstance w) => (int)Math.Floor(Math.Round(BalanceConfig.D(Cfg.discardRefund) * w.PricePaid, 6));

        /// <summary>Discards a word and refunds part of its price. Returns the refund.</summary>
        public int Compost(WordInstance w, bool refund = true)
        {
            Detach(w);
            int r = refund ? CompostRefund(w) : 0;
            Gold += r;
            return r;
        }

        // ---------------------------------------------------------------- shop

        public void RefillShop()
        {
            Shop = new ShopState();
            Shop.Words.AddRange(ShopGen.MakeWordOffers(Vocab.Entries, OwnedIds(), Round, Rng));
            Shop.Joker = ShopGen.MakeJoker(Rng);
            Shop.Upgrades.AddRange(ShopGen.MakeUpgradeOffers(Round, Grid, Rng));
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

        /// <summary>Purchase quiz succeeded: pay and create the held word.</summary>
        public WordInstance CompleteWordPurchase(WordOffer offer, string typedGerman)
        {
            if (!offer.Available || !CanAfford(offer.Stats.Price)) return null;
            Gold -= offer.Stats.Price;
            offer.Sold = true;
            var w = CreateWord(offer.Entry, offer.Family, offer.Stats.Price);
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
            var matches = Vocab.Entries.Where(e => Checker.MatchesSpanish(input, e.Es)).ToList();
            if (matches.Count == 0) { reason = UiText.JokerUnknown; return null; }
            var ofType = matches.Where(e => e.Type == type).ToList();
            if (ofType.Count == 0) { reason = UiText.JokerWrongType(type); return null; }
            var owned = OwnedIds(held);
            var free = ofType.Where(e => !owned.Contains(e.Id)).ToList();
            if (free.Count == 0) { reason = UiText.JokerOwned; return null; }
            // Prefer an exact-spelling match.
            return free.FirstOrDefault(e => Checker.CheckSpanish(input, e.Es).Result == AnswerResult.Correct) ?? free[0];
        }

        public WordInstance BuyJoker(VocabEntry e)
        {
            int price = JokerPrice(e);
            if (Shop.Joker == null || !Shop.Joker.Available || !CanAfford(price)) return null;
            Gold -= price;
            Shop.Joker.Used = true;
            var w = CreateWord(e, ShopGen.RollFamily(Rng), price);
            w.Location = WordLocation.Held;
            WordsPlanted++;
            CurrentRound.WordsBought++;
            return w;
        }

        public int PriceOf(UpgradeOffer o) => ShopGen.OfferPrice(o, Upgrades);

        public bool IsValidModTarget(OfferKind kind, WordInstance w)
        {
            if (w == null || w.Location != WordLocation.Grid) return false;
            if (kind == OfferKind.ArticleMod) return w.Type == WordType.Noun && !w.HasArticleMod;
            if (kind == OfferKind.ConjugationMod) return w.Type == WordType.Verb && !w.AllFormsCorrect;
            return false;
        }

        public bool HasModTarget(OfferKind kind) => Grid.Words.Any(w => IsValidModTarget(kind, w));

        /// <summary>Null if the offer can be bought now, else a German reason.</summary>
        public string WhyCannotBuy(UpgradeOffer o)
        {
            if (o.Sold) return UiText.Sold;
            if (!CanAfford(PriceOf(o))) return UiText.NotEnoughGold;
            if (o.IsMod)
            {
                if (ActionsLeft <= 0) return UiText.NoActionLeft;
                if (!HasModTarget(o.Kind)) return o.Kind == OfferKind.ArticleMod ? UiText.NoNounTarget : UiText.NoVerbTarget;
            }
            if (o.Kind == OfferKind.FieldExpansion && !Grid.CanExpand) return UiText.FieldMaxed;
            return null;
        }

        /// <summary>Pays for a mod and returns the price (for refund on cancel).</summary>
        public bool BeginModPurchase(UpgradeOffer o, out int price)
        {
            price = PriceOf(o);
            if (WhyCannotBuy(o) != null) return false;
            Gold -= price;
            o.Sold = true;
            return true;
        }

        public void CancelModPurchase(UpgradeOffer o, int price)
        {
            Gold += price;
            o.Sold = false;
        }

        public void ApplyArticleModResult(WordInstance w, bool success)
        {
            ActionsLeft = Math.Max(0, ActionsLeft - 1);
            CurrentRound.ModsApplied++;
            if (success)
            {
                w.HasArticleMod = true;
                ModsLearned++;
            }
            else CurrentRound.QuizFailures++;
        }

        /// <summary>correct[i] true = form i known after this quiz (includes previously locked forms).</summary>
        public void ApplyConjugationModResult(WordInstance w, bool[] correct)
        {
            ActionsLeft = Math.Max(0, ActionsLeft - 1);
            CurrentRound.ModsApplied++;
            int newly = 0;
            for (int i = 0; i < 6; i++)
            {
                if (correct[i] && !w.ConjugationCorrect[i]) newly++;
                w.ConjugationCorrect[i] |= correct[i];
            }
            if (newly == 0) CurrentRound.QuizFailures++;
            ModsLearned += newly;
            w.HasConjugationMod = true;
            w.Size = ConjugatedSize(w);
        }

        public int ConjugatedSize(WordInstance w)
        {
            int shrink = w.CorrectForms / Math.Max(1, Cfg.formsPerShrink);
            return Math.Max(Cfg.minWordSize, w.Stats.UnmoddedSize - shrink);
        }

        public bool BuyUpgrade(UpgradeOffer o)
        {
            if (o.IsMod || o.Kind == OfferKind.FieldExpansion || WhyCannotBuy(o) != null) return false;
            Gold -= PriceOf(o);
            o.Sold = true;
            switch (o.Kind)
            {
                case OfferKind.PlusMult: Upgrades.PlusMult[(int)o.Type]++; break;
                case OfferKind.XMult: Upgrades.XMult[(int)o.Type]++; break;
                case OfferKind.ExtraAction:
                    Upgrades.ExtraActions++;
                    MaxActions++;
                    ActionsLeft++;
                    break;
            }
            return true;
        }

        public bool BuyFieldExpansion(UpgradeOffer o, bool column)
        {
            if (o.Kind != OfferKind.FieldExpansion || WhyCannotBuy(o) != null) return false;
            if (column ? !Grid.CanAddColumn : !Grid.CanAddRow) return false;
            Gold -= PriceOf(o);
            o.Sold = true;
            Upgrades.FieldExpansions++;
            Grid.Expand(column);
            return true;
        }

        // ---------------------------------------------------------------- harvest

        public HarvestResult Harvest(ForcedOutcome forced = ForcedOutcome.None)
        {
            var res = new HarvestResult { Round = Round, Target = Target };
            foreach (var w in Grid.Words)
                res.WordScores.Add(new HarvestWordScore { Word = w, Score = ScoreOf(w).Total, Anchor = w.Anchor });
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

        string ApplyScarecrowPenalty()
        {
            var verb = Grid.Words.Where(w => w.Type == WordType.Verb && w.HasConjugationMod)
                .OrderByDescending(w => ScoreOf(w).Total).FirstOrDefault();
            if (verb != null)
            {
                verb.HasConjugationMod = false;
                Array.Clear(verb.ConjugationCorrect, 0, 6);
                return UiText.ScarecrowVerb(verb.Entry.Es) + " " + Regrow(verb, verb.Stats.UnmoddedSize);
            }
            var noun = Grid.Words.Where(w => w.Type == WordType.Noun && w.HasArticleMod)
                .OrderByDescending(w => ScoreOf(w).Total).FirstOrDefault();
            if (noun != null)
            {
                noun.HasArticleMod = false;
                return UiText.ScarecrowNoun(noun.Entry.Es);
            }
            return UiText.ScarecrowNoPenalty;
        }

        /// <summary>Grows a word in place; moves it to the wheelbarrow or composts it if it no longer fits.</summary>
        public string Regrow(WordInstance w, int newSize)
        {
            if (w.Location != WordLocation.Grid)
            {
                w.Size = newSize;
                return "";
            }
            if (Grid.CanPlace(w, w.Anchor, w.Rotation, newSize))
            {
                w.Size = newSize;
                return UiText.RegrowInPlace;
            }
            w.Size = newSize;
            if (PutInFreeWheelbarrowSlot(w)) return UiText.RegrowToWheelbarrow;
            if (Shed != null && Shed.FindFirstFit(w, out var a, out var r) && PlaceIn(Shed, w, a, r)) return UiText.RegrowToShed;
            Compost(w, refund: false);
            return UiText.RegrowComposted;
        }

        // ---------------------------------------------------------------- debug

        public void DebugSetRound(int round)
        {
            Round = Math.Max(1, round);
            RefillShop();
        }
    }
}
