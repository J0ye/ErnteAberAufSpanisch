using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosecha
{
    /// <summary>Upgrades bought in the shop during a run.</summary>
    public class UpgradeState
    {
        public readonly int[] PlusMult = new int[3];  // indexed by WordType (Noun, Verb, Adjective)
        public readonly int[] XMult = new int[3];
        public int FieldExpansions;
        public int ExtraActions;

        public void Reset()
        {
            Array.Clear(PlusMult, 0, 3);
            Array.Clear(XMult, 0, 3);
            FieldExpansions = 0;
            ExtraActions = 0;
        }
    }

    public struct ScoreBreakdown
    {
        public WordType Type;
        public int BasePoints;
        public double Synergy;
        public double Article;
        public double TypeMult;
        public double TypeXMult;
        public double Total;
        public bool Flat;
    }

    public class ScoreCalculator
    {
        readonly BalanceConfig cfg;

        public ScoreCalculator(BalanceConfig cfg) { this.cfg = cfg; }

        public static WordType? BoostedBy(WordType t)
        {
            // adjectives boost verbs, verbs boost nouns, nouns boost adjectives
            switch (t)
            {
                case WordType.Verb: return WordType.Adjective;
                case WordType.Noun: return WordType.Verb;
                case WordType.Adjective: return WordType.Noun;
                default: return null;
            }
        }

        public double Synergy(WordType type, IEnumerable<WordInstance> gridWords)
        {
            var src = BoostedBy(type);
            if (src == null) return 1.0;
            int count = gridWords.Count(w => w.Type == src.Value);
            return 1.0 + BalanceConfig.D(cfg.synergyPerWord) * count;
        }

        public double TypeMult(WordType type, UpgradeState up) =>
            type.IsFlat() ? 1.0 : 1.0 + BalanceConfig.D(cfg.plusMultStep) * up.PlusMult[(int)type];

        public double TypeXMult(WordType type, UpgradeState up) =>
            type.IsFlat() ? 1.0 : Math.Pow(BalanceConfig.D(cfg.xMultFactor), up.XMult[(int)type]);

        public ScoreBreakdown Score(WordInstance w, IReadOnlyList<WordInstance> gridWords, UpgradeState up) =>
            Score(w.Type, w.Stats.BasePoints, w.HasArticleMod, gridWords, up);

        public ScoreBreakdown Score(WordType type, int basePoints, bool articleMod, IReadOnlyList<WordInstance> gridWords, UpgradeState up)
        {
            var b = new ScoreBreakdown { Type = type, BasePoints = basePoints, Synergy = 1, Article = 1, TypeMult = 1, TypeXMult = 1 };
            if (type.IsFlat())
            {
                b.Flat = true;
                b.Total = basePoints;
                return b;
            }
            b.Synergy = Synergy(type, gridWords);
            b.Article = articleMod && type == WordType.Noun ? BalanceConfig.D(cfg.articleMultiplier) : 1.0;
            b.TypeMult = TypeMult(type, up);
            b.TypeXMult = TypeXMult(type, up);
            b.Total = basePoints * b.Synergy * b.Article * b.TypeMult * b.TypeXMult;
            return b;
        }

        public double RoundScore(IReadOnlyList<WordInstance> gridWords, UpgradeState up) =>
            gridWords.Sum(w => Score(w, gridWords, up).Total);

        public int GoldIncome(IReadOnlyList<WordInstance> gridWords) =>
            cfg.baseIncome + gridWords.Sum(w => w.Stats.Gold);

        public double Target(int round) => BalanceConfig.D(cfg.targetBase) * Math.Pow(BalanceConfig.D(cfg.targetGrowth), round - 1);

        /// <summary>Score comparison as float, with a tiny tolerance against rounding noise.</summary>
        public static bool Reaches(double score, double target) => score >= target - 1e-6;
    }
}
