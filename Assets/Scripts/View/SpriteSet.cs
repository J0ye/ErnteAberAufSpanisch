using TMPro;
using UnityEngine;

namespace Cosecha.View
{
    /// <summary>Every sprite the game uses. Replace entries with hand-drawn art without code changes.</summary>
    [CreateAssetMenu(fileName = "SpriteSet", menuName = "Cosecha/Sprite Set")]
    public class SpriteSet : ScriptableObject
    {
        [Header("Generic")]
        public Sprite pixel;
        public Sprite panel;
        public Sprite button;
        public TMP_FontAsset font;

        [Header("Scenery")]
        public Sprite sky;
        [Tooltip("Full-screen cliff/mesa with the reserved ground field")]
        public Sprite mesa;
        public Sprite house;
        public Sprite cloudSmall;
        public Sprite cloudMedium;
        public Sprite cloudLarge;
        public Sprite scarecrow;

        [Header("Sky boards (flat, top-down)")]
        [Tooltip("Board cell, white – tinted at runtime")]
        public Sprite boardCell;
        public Sprite boardHighlight;
        [Tooltip("Flat 2D crop icon for nouns, verbs, adjectives and other words")]
        public Sprite iconWheat;
        [Tooltip("Flat 2D crop icon for phrases")]
        public Sprite iconLavender;
        [Tooltip("Roof drawn above the shed board")]
        public Sprite shedRoof;
        public Sprite compost;
        public Sprite sign;
        public Sprite articleIcon;
        public Sprite starIcon;

        [Header("Ground field (isometric, decoration)")]
        public Sprite soilTile;
        public Sprite wheat;
        public Sprite lavender;

        /// <summary>Isometric crop on the ground.</summary>
        public Sprite Crop(WordType t) => t == WordType.Phrase && lavender != null ? lavender : wheat;
        /// <summary>Flat crop icon on the sky boards and in the shop.</summary>
        public Sprite Icon(WordType t) => t == WordType.Phrase && iconLavender != null ? iconLavender : iconWheat;

        public Sprite Cloud(int i) => i <= 0 ? cloudSmall : i == 1 ? cloudMedium : cloudLarge;

        /// <summary>Badge color on the sign so word types stay distinguishable (synergy ring).</summary>
        public static Color TypeColor(WordType t)
        {
            switch (t)
            {
                case WordType.Noun: return new Color32(232, 178, 40, 255);
                case WordType.Verb: return new Color32(82, 168, 72, 255);
                case WordType.Adjective: return new Color32(230, 112, 40, 255);
                case WordType.Phrase: return new Color32(150, 110, 200, 255);
                default: return new Color32(150, 160, 175, 255);
            }
        }
    }
}
