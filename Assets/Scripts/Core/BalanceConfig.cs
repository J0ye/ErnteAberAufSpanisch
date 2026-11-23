using UnityEngine;

namespace Cosecha
{
    public enum AccentMode { Lenient, Strict }

    /// <summary>
    /// Every balancing number of the game. Edit Assets/Config/BalanceConfig.asset to tune.
    /// Field initializers are the defaults from the design document.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Cosecha/Balance Config")]
    public class BalanceConfig : ScriptableObject
    {
        /// <summary>Float config value as a clean double (0.1f -> 0.1, not 0.100000001).</summary>
        public static double D(float f) => (double)(decimal)f;

        [Header("Run start")]
        public int startGold = 5;
        public int startActions = 2;
        public int startGridWidth = 8;
        public int startGridHeight = 8;
        public int maxGridWidth = 15;
        public int maxGridHeight = 11;
        [Tooltip("0 = no wheelbarrow (currently removed from the UI).")]
        public int wheelbarrowSlots = 0;
        [Tooltip("Storage shed grid next to the field (words stored there do not score). Layout reserves 5x3.")]
        public int shedWidth = 5;
        public int shedHeight = 3;
        [Tooltip("Ids of the starting words. Missing ids fall back to the lowest-K entry of the same type.")]
        public string[] startWordIds = { "comer", "casa", "grande", "dos" };
        [Tooltip("Types of the starting words, used for the fallback when an id is missing.")]
        public WordType[] startWordTypes = { WordType.Verb, WordType.Noun, WordType.Adjective, WordType.Other };

        [Header("Complexity K")]
        public int accentedVowelBonus = 2;
        public int enyeOrDiaeresisBonus = 3;
        public int irregularVerbBonus = 3;

        [Header("Bands (K upper limits; band 4 is everything above band 3)")]
        public int band1MaxK = 4;
        public int band2MaxK = 8;
        public int band3MaxK = 12;
        [Tooltip("Size per band 1..4")]
        public int[] sizeByBand = { 4, 3, 2, 1 };
        [Tooltip("Gold per band 1..4")]
        public int[] goldByBand = { 4, 2, 1, 0 };
        public int verbInfinitiveSurcharge = 2;

        [Header("Base points = round(K^exponent / divisor)")]
        public float pointsExponent = 2f;
        public float pointsDivisor = 10f;

        [Header("Shapes")]
        public float lineWeight = 1f;
        public float cornerWeight = 1f;
        public float blockWeight = 1f;

        [Header("Scoring")]
        public float synergyPerWord = 0.1f;
        public float articleMultiplier = 1.5f;
        public float plusMultStep = 0.5f;
        public float xMultFactor = 1.5f;

        [Header("Target curve: base * growth^(round-1)")]
        public float targetBase = 8f;
        public float targetGrowth = 1.25f;

        [Header("Economy")]
        public int baseIncome = 3;
        public int priceBase = 2;
        public int priceGoldFactor = 3;
        public int pricePointsFactor = 1;
        [Range(0f, 1f)] public float discardRefund = 0.5f;
        [Range(0f, 1f)] public float jokerDiscount = 0.6f;

        [Header("Shop")]
        public int wordOfferCount = 3;
        public int upgradeOfferCount = 2;
        [Tooltip("Round r allows K <= gateBase + floor(r / gateDivisor)")]
        public int complexityGateBase = 6;
        public int complexityGateDivisor = 2;
        public int rerollStartPrice = 1;
        public int rerollPriceStep = 1;
        [Range(0f, 1f)] public float spanishToGermanChance = 0.5f;

        [Header("Upgrade and mod prices")]
        public int articleModPrice = 6;
        public int conjugationModPrice = 8;
        public float plusMultBasePrice = 10f;
        public float plusMultPriceGrowth = 1.6f;
        public float xMultBasePrice = 40f;
        public float xMultPriceGrowth = 2f;
        public int xMultMinRound = 10;
        public float fieldExpansionBasePrice = 15f;
        public float fieldExpansionPriceGrowth = 2f;
        public float extraActionBasePrice = 20f;
        public float extraActionPriceGrowth = 2f;

        [Header("Offer weights (split evenly inside each group)")]
        public float commonWeight = 0.7f;
        public float rareWeight = 0.3f;

        [Header("Mods")]
        [Tooltip("Size shrink = floor(correctForms / formsPerShrink)")]
        public int formsPerShrink = 2;
        public int minWordSize = 1;

        [Header("Quizzes")]
        public int quizAttempts = 3;
        public int jokerAttempts = 3;
        public AccentMode accentMode = AccentMode.Lenient;

        [Header("Presentation")]
        public float cloudSpawnMinSeconds = 8f;
        public float cloudSpawnMaxSeconds = 20f;
        public float cloudSpeedMin = 0.35f;
        public float cloudSpeedMax = 0.6f;
        public float harvestAnimationSeconds = 1.8f;
        public int leaderboardSize = 10;
    }
}
