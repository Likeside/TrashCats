using DG.Tweening;
using UnityEngine;

namespace TrashCats.Presentation.Audio
{
    /// <summary>
    /// Plays sound effects from a small pool (random variant, slight pitch jitter) and one music track at a time with
    /// a crossfade. Volumes come from the player's settings. Without a library every call is a no-op (tests).
    /// </summary>
    public sealed class GameAudio
    {
        private const int Voices = 8;

        private readonly AudioLibrary _library;
        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private readonly AudioSource[] _music = new AudioSource[2];
        private int _nextVoice;
        private int _currentMusic;
        private string _musicKey;
        private float _sfxVolume = 1f;
        private float _musicVolume = 0.7f;

        public GameAudio(AudioLibrary library, Transform parent)
        {
            _library = library;
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            // the scenes are built from code and have no listener: without one nothing is heard (all sounds are 2D)
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                go.AddComponent<AudioListener>();
            for (var i = 0; i < Voices; i++)
                _voices[i] = go.AddComponent<AudioSource>();
            for (var i = 0; i < 2; i++)
            {
                _music[i] = go.AddComponent<AudioSource>();
                _music[i].loop = true;
                _music[i].volume = 0f;
            }
            foreach (var source in go.GetComponents<AudioSource>())
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                var current = _music[_currentMusic];
                current.DOKill();
                current.volume = MusicLevel(_musicKey);
            }
        }

        public void Play(string key, float volume = 1f, float pitchJitter = 0.06f)
        {
            var entry = _library != null ? _library.Find(key) : null;
            if (entry == null || entry.clips.Length == 0 || _sfxVolume <= 0f)
                return;
            var source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(entry.clips[Random.Range(0, entry.clips.Length)], entry.volume * volume * _sfxVolume);
        }

        /// <summary>Crossfades to another looping track (no-op if it is already playing).</summary>
        public void PlayMusic(string key, float fade = 1f)
        {
            if (key == _musicKey)
                return;
            _musicKey = key;
            var entry = _library != null ? _library.Find(key) : null;
            var old = _music[_currentMusic];
            old.DOKill();
            old.DOFade(0f, fade).OnComplete(old.Stop).SetLink(old.gameObject);
            if (entry == null || entry.clips.Length == 0)
                return;
            _currentMusic = 1 - _currentMusic;
            var next = _music[_currentMusic];
            next.DOKill();
            next.clip = entry.clips[0];
            next.volume = 0f;
            next.Play();
            next.DOFade(MusicLevel(key), fade).SetLink(next.gameObject);
        }

        private float MusicLevel(string key)
        {
            var entry = _library != null && key != null ? _library.Find(key) : null;
            return (entry?.volume ?? 1f) * _musicVolume;
        }
    }
}
