using System;
using System.Text;

namespace Cosecha
{
    public struct WordStats
    {
        public int K;
        /// <summary>Size as bought (verbs: infinitive, including the infinitive cell).</summary>
        public int BaseSize;
        public int BasePoints;
        public int Gold;
        public int Price;
    }

    /// <summary>Derives size, base points, gold and price of a word from its Spanish spelling and type.</summary>
    public class ComplexityCalculator
    {
        const string AccentedVowels = "áéíóúÁÉÍÓÚ";
        const string EnyeDiaeresis = "ñÑüÜ";

        readonly BalanceConfig cfg;

        public ComplexityCalculator(BalanceConfig cfg) { this.cfg = cfg; }

        public static int CountLetters(string spanish)
        {
            int n = 0;
            foreach (char c in spanish.Normalize(NormalizationForm.FormC))
                if (char.IsLetter(c)) n++;
            return n;
        }

        public int ComputeK(string spanish, bool irregularVerb, int complexityOverride = 0)
        {
            if (complexityOverride > 0) return complexityOverride;
            string s = spanish.Normalize(NormalizationForm.FormC);
            int k = 0;
            foreach (char c in s)
            {
                if (!char.IsLetter(c)) continue;
                k++;
                if (AccentedVowels.IndexOf(c) >= 0) k += cfg.accentedVowelBonus;
                else if (EnyeDiaeresis.IndexOf(c) >= 0) k += cfg.enyeOrDiaeresisBonus;
            }
            if (irregularVerb) k += cfg.irregularVerbBonus;
            return k;
        }

        public int ComputeK(VocabEntry e) => ComputeK(e.Es, e.IsIrregularVerb, e.ComplexityOverride);

        public int Band(int k)
        {
            if (k <= cfg.band1MaxK) return 0;
            if (k <= cfg.band2MaxK) return 1;
            if (k <= cfg.band3MaxK) return 2;
            return 3;
        }

        public int Size(int k, WordType type)
        {
            if (type.IsFunctionWord()) return cfg.functionWordSize;
            int size = cfg.sizeByBand[Band(k)];
            if (type == WordType.Verb) size += cfg.verbInfinitiveSurcharge;
            return Math.Max(cfg.minWordSize, size);
        }

        /// <summary>Size of a verb after its first conjugation (the infinitive cell is gone).</summary>
        public int ConjugatedSize(WordStats s) => Math.Max(cfg.minWordSize, s.BaseSize - cfg.verbInfinitiveSurcharge);

        public int BasePoints(int k) =>
            (int)Math.Round(Math.Pow(k, BalanceConfig.D(cfg.pointsExponent)) / BalanceConfig.D(cfg.pointsDivisor), MidpointRounding.AwayFromZero);

        public int Gold(int k) => cfg.goldByBand[Band(k)];

        public int Price(int gold, int basePoints) =>
            cfg.priceBase + cfg.priceGoldFactor * gold + cfg.pricePointsFactor * basePoints;

        public WordStats Compute(VocabEntry e) => Compute(e.Es, e.Type, e.IsIrregularVerb, e.ComplexityOverride);

        public WordStats Compute(string spanish, WordType type, bool irregular, int complexityOverride = 0)
        {
            int k = ComputeK(spanish, irregular, complexityOverride);
            bool fn = type.IsFunctionWord();
            int bp = fn ? cfg.functionWordPoints : BasePoints(k);
            int gold = fn ? cfg.functionWordGold : Gold(k);
            return new WordStats
            {
                K = k,
                BaseSize = Size(k, type),
                BasePoints = bp,
                Gold = gold,
                Price = Price(gold, bp)
            };
        }

        public int JokerPrice(int normalPrice) => (int)Math.Ceiling(Math.Round(BalanceConfig.D(cfg.jokerDiscount) * normalPrice, 6));
    }
}
