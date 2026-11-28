using System;
using UnityEngine;

namespace Cosecha
{
    public enum AccentMode { Lenient, Strict }

    /// <summary>Which dimension the field grows in.</summary>
    public enum GrowthAxis { Row, Column }

    /// <summary>One sentence pattern as data. See <see cref="SentencePattern"/> for the syntax.</summary>
    [Serializable]
    public class SentencePatternDef
    {
        public StructureCategory category;
        public string sequence;

        public SentencePatternDef() { }
        public SentencePatternDef(StructureCategory c, string s) { category = c; sequence = s; }
    }

    /// <summary>
    /// Every balancing number of the game. Edit Assets/Config/BalanceConfig.asset to tune.
    /// Field initializers are the defaults from the design documents.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Cosecha/Balance Config")]
    public class BalanceConfig : ScriptableObject
    {
        /// <summary>Float config value as a clean double (0.1f -> 0.1, not 0.100000001).</summary>
        public static double D(float f) => (double)(decimal)f;

        [Header("Run start")]
        public int startGold = 5;
        [Tooltip("Free grammar actions (conjugate a verb / inflect an adjective) per round. Unused ones do not carry over.")]
        public int freeGrammarActionsPerRound = 1;
        public int startGridWidth = 6;
        public int startGridHeight = 2;
        public int maxGridWidth = 12;
        public int maxGridHeight = 7;
        [Tooltip("0 = no wheelbarrow (removed from the UI).")]
        public int wheelbarrowSlots = 0;
        [Tooltip("Storage shed grid next to the field (words stored there do not score). Layout reserves 5x3.")]
        public int shedWidth = 5;
        public int shedHeight = 3;
        [Tooltip("Ids of the starting words: yo (field 0,0), comer (field 1,0), pan (shed). Missing ids fall back to the lowest-K entry of the same type.")]
        public string[] startWordIds = { "yo", "comer", "pan" };
        [Tooltip("Types of the starting words, used for the fallback when an id is missing.")]
        public WordType[] startWordTypes = { WordType.Pronoun, WordType.Verb, WordType.Noun };
        [Tooltip("Replace the starting verb and noun with random picks each run (the pronoun stays fixed).")]
        public bool randomizeStartVerbAndNoun = true;
        [Tooltip("Random start words are drawn from this many lowest-K entries of their type (verbs: transitive only).")]
        [Min(1)] public int startWordPoolSize = 8;

        [Header("Field growth (at the start of a round)")]
        [Tooltip("Rounds between growths, cycled: 3, 4, 3, 4 … → rounds 4, 8, 11, 15 …")]
        public int[] growthIntervals = { 3, 4 };
        [Tooltip("Growth order, cycled. When one dimension is at its maximum the other one grows.")]
        public GrowthAxis[] growthOrder = { GrowthAxis.Row, GrowthAxis.Column };

        [Header("Complexity K")]
        public int accentedVowelBonus = 2;
        public int enyeOrDiaeresisBonus = 3;
        public int irregularVerbBonus = 3;

        [Header("Bands (K upper limits; band 4 is everything above band 3)")]
        public int band1MaxK = 4;
        public int band2MaxK = 8;
        public int band3MaxK = 12;
        [Tooltip("Size per band 1..4")]
        public int[] sizeByBand = { 3, 2, 1, 1 };
        [Tooltip("Gold per band 1..4")]
        public int[] goldByBand = { 4, 2, 1, 0 };
        [Tooltip("Extra cells of a verb in the infinitive (removed by the first conjugation)")]
        public int verbInfinitiveSurcharge = 1;
        public int minWordSize = 1;

        [Header("Function words (pronoun, article, conjunction, question word): fixed stats")]
        public int functionWordSize = 1;
        public int functionWordPoints = 1;
        public int functionWordGold = 1;

        [Header("Base points = round(K^exponent / divisor)")]
        public float pointsExponent = 2f;
        public float pointsDivisor = 10f;

        [Header("Sentence bonuses: wordScore = basePoints * (1 + sum of bonuses)")]
        public float satzteilBonus = 0.5f;
        public float satzBonus = 1.5f;
        public float frageBonus = 2f;
        [Tooltip("Sentence patterns as role sequences. Symbols: Subject, Object, NP, NP2, Pronoun, Noun, " +
                 "V[:transitive|:intransitive|:copula|:3sg], Adjective, Q[:subject|:object|:adverb], Y; '?' = optional.")]
        public SentencePatternDef[] sentencePatterns =
        {
            new SentencePatternDef(StructureCategory.Frage, "Q:object V:transitive Subject?"),
            new SentencePatternDef(StructureCategory.Frage, "Q:subject V:3sg Object?"),
            new SentencePatternDef(StructureCategory.Frage, "Q:adverb V Subject?"),
            new SentencePatternDef(StructureCategory.Satz, "Subject V:transitive Object"),
            new SentencePatternDef(StructureCategory.Satz, "Subject V:intransitive"),
            new SentencePatternDef(StructureCategory.Satz, "Subject V:copula Adjective"),
            new SentencePatternDef(StructureCategory.Satzteil, "Subject V:transitive"),
            new SentencePatternDef(StructureCategory.Satzteil, "V:transitive Object"),
            new SentencePatternDef(StructureCategory.Satzteil, "NP2"),
        };

        [Header("Target curve: base * growth^(round-1)")]
        public float targetBase = 10f;
        public float targetGrowth = 1.22f;

        [Header("Economy")]
        public int baseIncome = 3;
        public int priceBase = 2;
        public int priceGoldFactor = 3;
        public int pricePointsFactor = 1;
        [Range(0f, 1f)] public float discardRefund = 0.5f;
        [Range(0f, 1f)] public float jokerDiscount = 0.6f;

        [Header("Shop")]
        public int wordOfferCount = 5;
        [Tooltip("Round r allows K <= gateBase + floor(r / gateDivisor) (content words and phrases only)")]
        public int complexityGateBase = 6;
        public int complexityGateDivisor = 2;
        public int rerollStartPrice = 1;
        public int rerollPriceStep = 1;
        [Range(0f, 1f)] public float spanishToGermanChance = 0.5f;

        [Header("Shop need weighting (target ratio of owned words per role)")]
        public float ratioPronoun = 1f;
        public float ratioVerb = 1f;
        public float ratioNoun = 1.5f;
        [Tooltip("article, conjunction, question word")]
        public float ratioFunction = 1f;
        [Tooltip("adjective, other, phrase")]
        public float ratioRest = 0.75f;
        public float largestDeficitWeight = 3f;
        public float secondDeficitWeight = 2f;
        [Tooltip("Extra weight for candidates that complete an almost-structure on the field")]
        public float almostStructureWeight = 3f;

        [Header("Quizzes")]
        public int quizAttempts = 3;
        public int jokerAttempts = 3;
        public AccentMode accentMode = AccentMode.Lenient;

        [Header("Presentation")]
        public float cloudSpawnMinSeconds = 8f;
        public float cloudSpawnMaxSeconds = 20f;
        public float cloudSpeedMin = 0.35f;
        public float cloudSpeedMax = 0.6f;
        [Tooltip("Total harvest animation (structures light up, then word points pop)")]
        public float harvestAnimationSeconds = 2.5f;
        public float structurePopSeconds = 0.4f;
        public int leaderboardSize = 10;

        public double BonusOf(StructureCategory c) =>
            D(c == StructureCategory.Frage ? frageBonus : c == StructureCategory.Satz ? satzBonus : satzteilBonus);
    }
}
