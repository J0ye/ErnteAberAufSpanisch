using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosecha
{
    public struct ScoreBreakdown
    {
        public int BasePoints;
        /// <summary>Sum of the bonuses of all structures the word is part of.</summary>
        public double Bonus;
        public double Total;
        public List<Structure> Structures;
    }

    /// <summary>wordScore = basePoints * (1 + sum of bonuses). Gold income and the target curve.</summary>
    public class ScoreCalculator
    {
        readonly BalanceConfig cfg;

        public ScoreCalculator(BalanceConfig cfg) { this.cfg = cfg; }

        public ScoreBreakdown Score(WordInstance w, SentenceAnalysis a)
        {
            double bonus = a?.Bonus(w.Id) ?? 0;
            return new ScoreBreakdown
            {
                BasePoints = w.BasePoints,
                Bonus = bonus,
                Total = w.BasePoints * (1 + bonus),
                Structures = a?.StructuresOf(w.Id).ToList() ?? new List<Structure>()
            };
        }

        public double RoundScore(IEnumerable<WordInstance> fieldWords, SentenceAnalysis a) =>
            fieldWords.Sum(w => Score(w, a).Total);

        public int GoldIncome(IEnumerable<WordInstance> fieldWords) =>
            cfg.baseIncome + fieldWords.Sum(w => w.GoldYield);

        public double Target(int round) => BalanceConfig.D(cfg.targetBase) * Math.Pow(BalanceConfig.D(cfg.targetGrowth), round - 1);

        /// <summary>Score comparison as float, with a tiny tolerance against rounding noise.</summary>
        public static bool Reaches(double score, double target) => score >= target - 1e-6;
    }
}
