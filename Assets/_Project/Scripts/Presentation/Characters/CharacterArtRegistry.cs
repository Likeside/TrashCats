using System;
using System.Collections.Generic;
using TrashCats.Presentation.Animation;
using UnityEngine;

namespace TrashCats.Presentation.Characters
{
    /// <summary>Art per character view key (cats and enemies). Missing entries fall back to graybox placeholders.</summary>
    [CreateAssetMenu(fileName = "CharacterArt", menuName = "TrashCats/Character Art Registry")]
    public sealed class CharacterArtRegistry : ScriptableObject
    {
        public List<CharacterArt> characters = new List<CharacterArt>();

        public CharacterArt Find(string viewKey)
        {
            foreach (var art in characters)
                if (art != null && art.viewKey == viewKey)
                    return art;
            return null;
        }
    }

    [Serializable]
    public sealed class CharacterArt
    {
        public string viewKey;
        [Tooltip("World scale of the battle sprites")] public float battleScale = 1f;
        public FlipbookClip idle = new FlipbookClip();
        public FlipbookClip attack = new FlipbookClip();
        public FlipbookClip heavyAttack = new FlipbookClip();
        public FlipbookClip hit = new FlipbookClip();
        public FlipbookClip knockOut = new FlipbookClip();
        [Tooltip("Optional separately animated tail; attach points come from the body clips")] public FlipbookClip tail = new FlipbookClip();
        [Tooltip("Static picture for UI (hideout tokens, cards)")] public Sprite portrait;
        [Tooltip("Hideout (four-legged) picture")] public Sprite hideout;

        public bool HasBattleArt => idle != null && idle.frames != null && idle.frames.Length > 0;
    }
}
