using System.Collections.Generic;
using TMPro;
using TrashCats.Presentation.Characters;
using UnityEngine;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Comic cardboard UI skin (Docs/ArtDirection/ART_BIBLE.md §6): fonts and 9-sliced sprites used by <see cref="Ui"/>.
    /// Without a skin every widget stays graybox (tests, empty scenes).
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "TrashCats/UI Skin")]
    public sealed class UiSkin : ScriptableObject
    {
        [Header("Fonts")]
        [Tooltip("Headings, buttons, numbers (Russo One)")] public TMP_FontAsset headingFont;
        [Tooltip("Body text (Balsamiq Sans)")] public TMP_FontAsset bodyFont;
        [Tooltip("Heading font material with an ink outline, for text drawn over pictures")] public Material headingOutline;
        [Tooltip("Body font material with an ink outline")] public Material bodyOutline;

        [Header("Buttons")]
        [Tooltip("Neutral button tinted by any colour")] public Sprite button;
        [Tooltip("Hand-coloured buttons used for the two main palette colours")] public Sprite buttonPrimary;
        public Sprite buttonSecondary;

        [Header("Panels (border scale: source pixels per canvas unit)")]
        [Tooltip("Light cardboard sheet with paper inside")] public Sprite paper;
        public float paperBorderScale = 1.6f;
        [Tooltip("Dark painted tin (HUD, navigation)")] public Sprite tin;
        public float tinBorderScale = 2f;

        [Header("Bars and decorations")]
        public Sprite barFrame;
        [Tooltip("Bars with a badge on the left by key: energy, satiety, hp, grit")] public List<NamedSprite> badgeBars = new List<NamedSprite>();
        public Sprite ribbon;
        public Sprite burst;
        public Sprite bubble;
        public Sprite tape;
        public Sprite pointer;

        [Tooltip("Icons by key: food, junk, energy, satiety, strength, agility, toughness, xp, lock, clock, home, map, sleep, upgrade, speed, skip, settings, crown")]
        public List<NamedSprite> icons = new List<NamedSprite>();

        public Sprite Icon(string key) => Find(icons, key);

        public Sprite BadgeBar(string key) => Find(badgeBars, key);

        private static Sprite Find(List<NamedSprite> list, string key)
        {
            foreach (var entry in list)
                if (entry != null && entry.key == key)
                    return entry.sprite;
            return null;
        }
    }
}
