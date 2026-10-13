using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashCats.Presentation.Audio
{
    /// <summary>Sound effects (with variants) and music by key; built from Assets/_Project/Audio by the editor builder.</summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "TrashCats/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public List<SoundEntry> sounds = new List<SoundEntry>();

        public SoundEntry Find(string key)
        {
            foreach (var entry in sounds)
                if (entry != null && entry.key == key)
                    return entry;
            return null;
        }
    }

    [Serializable]
    public sealed class SoundEntry
    {
        public string key;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
    }
}
