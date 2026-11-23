using System;
using System.Collections.Generic;
using System.Linq;

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
        readonly Random r;
        public SystemRng(int? seed = null) { r = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public double NextDouble() => r.NextDouble();
        public int Range(int min, int max) => r.Next(min, max);
    }

    public enum OfferKind { ArticleMod, ConjugationMod, PlusMult, XMult, FieldExpansion, ExtraAction }

    public class WordOffer
    {
        public VocabEntry Entry;
        public WordStats Stats;
        public bool SpanishToGerman;
        /// <summary>Shape family, pre-rolled so the shop can preview the full shape.</summary>
        public ShapeFamily Family;
        /// <summary>German prompt shown when the direction is German → Spanish (first entry).</summary>
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

    public class UpgradeOffer
    {
        public OfferKind Kind;
        public WordType Type;   // for PlusMult / XMult
        public bool Sold;
        public bool IsMod => Kind == OfferKind.ArticleMod || Kind == OfferKind.ConjugationMod;
    }

    public class ShopState
    {
        public readonly List<WordOffer> Words = new List<WordOffer>();
        public JokerOffer Joker;
        public readonly List<UpgradeOffer> Upgrades = new List<UpgradeOffer>();
    }

    public class ShopGenerator
    {
        static readonly WordType[] MultTypes = { WordType.Noun, WordType.Verb, WordType.Adjective };
        static readonly WordType[] AllTypes = { WordType.Noun, WordType.Verb, WordType.Adjective, WordType.Other, WordType.Phrase };

        readonly BalanceConfig cfg;
        readonly ComplexityCalculator complexity;

        public ShopGenerator(BalanceConfig cfg, ComplexityCalculator complexity)
        {
            this.cfg = cfg;
            this.complexity = complexity;
        }

        public int ComplexityGate(int round) => cfg.complexityGateBase + (int)Math.Floor(round / (double)cfg.complexityGateDivisor);

        /// <summary>
        /// Draws word offers: eligible = K &lt;= gate, shuffled. If fewer than <paramref name="count"/>
        /// are eligible, fill up with the lowest-K remaining words.
        /// </summary>
        public List<VocabEntry> DrawWords(IEnumerable<VocabEntry> vocab, ICollection<string> excludedIds, int round, int count, IRng rng)
        {
            var pool = vocab.Where(e => !excludedIds.Contains(e.Id)).ToList();
            int gate = ComplexityGate(round);
            var eligible = pool.Where(e => complexity.ComputeK(e) <= gate).ToList();
            Shuffle(eligible, rng);
            var result = eligible.Take(count).ToList();
            if (result.Count < count)
            {
                var rest = pool.Except(result).OrderBy(e => complexity.ComputeK(e)).ThenBy(e => e.Id, StringComparer.Ordinal);
                result.AddRange(rest.Take(count - result.Count));
            }
            return result;
        }

        public List<WordOffer> MakeWordOffers(IEnumerable<VocabEntry> vocab, ICollection<string> excludedIds, int round, IRng rng)
        {
            return DrawWords(vocab, excludedIds, round, cfg.wordOfferCount, rng)
                .Select(e => new WordOffer
                {
                    Entry = e,
                    Stats = complexity.Compute(e),
                    SpanishToGerman = rng.NextDouble() < cfg.spanishToGermanChance,
                    Family = RollFamily(rng)
                }).ToList();
        }

        public JokerOffer MakeJoker(IRng rng) => new JokerOffer
        {
            Type = AllTypes[rng.Range(0, AllTypes.Length)],
            AttemptsLeft = cfg.jokerAttempts
        };

        public List<UpgradeOffer> MakeUpgradeOffers(int round, GridModel grid, IRng rng)
        {
            var common = new List<OfferKind> { OfferKind.ArticleMod, OfferKind.ConjugationMod, OfferKind.PlusMult };
            var rare = new List<OfferKind>();
            if (round >= cfg.xMultMinRound) rare.Add(OfferKind.XMult);
            if (grid == null || grid.CanExpand) rare.Add(OfferKind.FieldExpansion);
            rare.Add(OfferKind.ExtraAction);

            var list = new List<UpgradeOffer>();
            for (int i = 0; i < cfg.upgradeOfferCount; i++)
            {
                UpgradeOffer offer = null;
                for (int tries = 0; tries < 20; tries++)
                {
                    offer = RollOffer(common, rare, rng);
                    var o = offer;
                    if (!list.Any(x => x.Kind == o.Kind && x.Type == o.Type)) break;
                }
                list.Add(offer);
            }
            return list;
        }

        UpgradeOffer RollOffer(List<OfferKind> common, List<OfferKind> rare, IRng rng)
        {
            double total = (common.Count > 0 ? cfg.commonWeight : 0) + (rare.Count > 0 ? cfg.rareWeight : 0);
            double roll = rng.NextDouble() * total;
            var group = (common.Count > 0 && roll < cfg.commonWeight) || rare.Count == 0 ? common : rare;
            var kind = group[rng.Range(0, group.Count)];
            var offer = new UpgradeOffer { Kind = kind, Type = WordType.Other };
            if (kind == OfferKind.PlusMult || kind == OfferKind.XMult)
                offer.Type = MultTypes[rng.Range(0, MultTypes.Length)];
            return offer;
        }

        public int OfferPrice(UpgradeOffer o, UpgradeState up)
        {
            double p;
            switch (o.Kind)
            {
                case OfferKind.ArticleMod: return cfg.articleModPrice;
                case OfferKind.ConjugationMod: return cfg.conjugationModPrice;
                case OfferKind.PlusMult: p = BalanceConfig.D(cfg.plusMultBasePrice) * Math.Pow(BalanceConfig.D(cfg.plusMultPriceGrowth), up.PlusMult[(int)o.Type]); break;
                case OfferKind.XMult: p = BalanceConfig.D(cfg.xMultBasePrice) * Math.Pow(BalanceConfig.D(cfg.xMultPriceGrowth), up.XMult[(int)o.Type]); break;
                case OfferKind.FieldExpansion: p = BalanceConfig.D(cfg.fieldExpansionBasePrice) * Math.Pow(BalanceConfig.D(cfg.fieldExpansionPriceGrowth), up.FieldExpansions); break;
                case OfferKind.ExtraAction: p = BalanceConfig.D(cfg.extraActionBasePrice) * Math.Pow(BalanceConfig.D(cfg.extraActionPriceGrowth), up.ExtraActions); break;
                default: p = 0; break;
            }
            return (int)Math.Round(Math.Round(p, 6), MidpointRounding.AwayFromZero);
        }

        public ShapeFamily RollFamily(IRng rng)
        {
            double total = cfg.lineWeight + cfg.cornerWeight + cfg.blockWeight;
            double roll = rng.NextDouble() * total;
            if (roll < cfg.lineWeight) return ShapeFamily.Line;
            if (roll < cfg.lineWeight + cfg.cornerWeight) return ShapeFamily.Corner;
            return ShapeFamily.Block;
        }

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
