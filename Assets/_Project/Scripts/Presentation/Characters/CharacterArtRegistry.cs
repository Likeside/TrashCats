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

        [Tooltip("Environment and UI pictures by key: bg_hideout, bg_map, arena_<territory view key>, prop_<upgrade id>, icon_<name>")]
        public List<NamedSprite> sprites = new List<NamedSprite>();

        public Sprite GetSprite(string key)
        {
            foreach (var entry in sprites)
                if (entry != null && entry.key == key)
                    return entry.sprite;
            return null;
        }

        public CharacterArt Find(string viewKey)
        {
            foreach (var art in characters)
                if (art != null && art.viewKey == viewKey)
                    return art;
            return null;
        }
    }

    [Serializable]
    public sealed class NamedSprite
    {
        public string key;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CharacterArt
    {
        public string viewKey;
        [Tooltip("World scale of the battle sprites")] public float battleScale = 1f;
        [Tooltip("Colour multiplier (variants such as the crow boss reuse another character's frames)")] public Color tint = Color.white;
        public FlipbookClip idle = new FlipbookClip();
        public FlipbookClip attack = new FlipbookClip();
        public FlipbookClip heavyAttack = new FlipbookClip();
        public FlipbookClip hit = new FlipbookClip();
        public FlipbookClip knockOut = new FlipbookClip();
        [Tooltip("Hideout: cocky idle standing on the hind legs (loop)")] public FlipbookClip hideoutIdle = new FlipbookClip();
        [Tooltip("Hideout: skipping rope (loop)")] public FlipbookClip rope = new FlipbookClip();
        [Tooltip("Hideout: head first in the dumpster, rump and tail out (loop)")] public FlipbookClip dig = new FlipbookClip();
        [Tooltip("Hideout: running in the wheel (loop; the wheel was replaced by the bench press)")] public FlipbookClip run = new FlipbookClip();
        [Tooltip("Hideout: bench press on the mat, a whole up-and-down rep in frame order (loop)")] public FlipbookClip bench = new FlipbookClip();
        [Tooltip("Hideout: punching the tin can (loop)")] public FlipbookClip canPunch = new FlipbookClip();
        [Tooltip("Optional separately animated tail; attach points come from the body clips")] public FlipbookClip tail = new FlipbookClip();
        [Tooltip("Static picture for UI (hideout tokens, cards)")] public Sprite portrait;
        [Tooltip("Hideout cat card: portrait on its coloured backdrop, cut from the concept (fills the card window)")] public Sprite card;
        [Tooltip("Hideout (four-legged) pictures: sitting, sleeping, eating, walking")] public Sprite[] hideoutPoses = Array.Empty<Sprite>();

        public Sprite HideoutPose(int index) => hideoutPoses != null && index < hideoutPoses.Length ? hideoutPoses[index] : null;

        public bool HasBattleArt => idle != null && idle.frames != null && idle.frames.Length > 0;
    }
}
