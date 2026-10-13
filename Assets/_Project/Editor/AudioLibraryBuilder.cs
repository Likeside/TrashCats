using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TrashCats.Presentation.Audio;
using TrashCats.Presentation.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// Builds Assets/_Project/Configs/AudioLibrary.asset from Assets/_Project/Audio (Tools/import_audio.py):
    /// Sfx/&lt;key&gt;_NN.ogg become variants of one key, Music/&lt;key&gt;.ogg stream. Assigns it to the Game scene.
    /// </summary>
    public static class AudioLibraryBuilder
    {
        private const string AudioRoot = "Assets/_Project/Audio";
        private const string LibraryPath = "Assets/_Project/Configs/AudioLibrary.asset";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // mix: quieter for frequent or loud sounds
        private static readonly Dictionary<string, float> Volumes = new Dictionary<string, float>
        {
            { "ui_tap", 0.5f }, { "cmb_swing", 0.45f }, { "cmb_miss", 0.6f }, { "hid_sleep", 0.6f }, { "hid_wheel", 0.6f },
            { "music_hideout", 0.55f }, { "music_battle", 0.5f },
        };

        [MenuItem("TrashCats/Audio/Rebuild Audio Library")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(AudioRoot, "*.ogg", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
                Configure(path, path.Contains("/Music/"));

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.sounds = Directory.GetFiles(AudioRoot, "*.ogg", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .GroupBy(p => Regex.Replace(Path.GetFileNameWithoutExtension(p), @"_\d\d$", ""))
                .OrderBy(g => g.Key)
                .Select(g => new SoundEntry
                {
                    key = g.Key,
                    clips = g.OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray(),
                    volume = Volumes.TryGetValue(g.Key, out var v) ? v : 1f,
                })
                .ToList();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var so = new SerializedObject(Object.FindFirstObjectByType<GameSceneRoot>());
            so.FindProperty("audioLibrary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Audio] {library.sounds.Count} keys: {string.Join(", ", library.sounds.Select(s => $"{s.key}({s.clips.Length})"))}");
        }

        private static void Configure(string path, bool music)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                return;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.5f : 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
            importer.SaveAndReimport();
        }
    }
}
