using System.IO;
using TrashCats.Presentation.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>Editor shortcuts for testing: wipe the save and prefs, get the whole gang at once.</summary>
    public static class DevTools
    {
        private static readonly string[] SaveFiles = { "save.json", "save.bak.json", "save.tmp.json", "playtest_log.jsonl" };

        [MenuItem("TrashCats/Dev/Clear Saves and Prefs")]
        public static void ClearSavesAndPrefs()
        {
            if (Application.isPlaying && GameBootstrap.Instance != null)
            {
                // in play mode the running game must forget its state too: reset like the in-game button does
                PlayerPrefs.DeleteAll();
                GameBootstrap.Instance.ResetProgress();
                Debug.Log("[DevTools] progress reset in the running game, PlayerPrefs cleared");
                return;
            }

            var removed = 0;
            foreach (var name in SaveFiles)
            {
                var path = Path.Combine(Application.persistentDataPath, name);
                if (!File.Exists(path)) continue;
                File.Delete(path);
                removed++;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log($"[DevTools] removed {removed} save files from {Application.persistentDataPath}, PlayerPrefs cleared");
        }

        [MenuItem("TrashCats/Dev/Give All Three Cats")]
        public static void GiveAllCats()
        {
            if (!Application.isPlaying || GameBootstrap.Instance == null)
            {
                EditorUtility.DisplayDialog("Trash Cats", "Запусти игру (Play), затем выбери пункт ещё раз: коты добавляются в текущую сессию и сохраняются.", "OK");
                return;
            }

            GameBootstrap.Instance.Services.Debug.RecruitAllAndRest();
            Debug.Log("[DevTools] the whole gang recruited and rested");
        }

        [MenuItem("TrashCats/Dev/Give All Three Cats", true)]
        private static bool CanGiveAllCats() => Application.isPlaying;
    }
}
