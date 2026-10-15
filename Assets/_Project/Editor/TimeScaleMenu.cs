using UnityEditor;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// TrashCats/Time Scale: sets Time.timeScale in Play mode (animations, tweens and battle replays slow down or speed
    /// up; the domain clock is not affected). The current value is ticked.
    /// </summary>
    public static class TimeScaleMenu
    {
        private const string Root = "TrashCats/Time Scale/";

        [MenuItem(Root + "×0.1", false, 0)] private static void S01() => Set(0.1f);
        [MenuItem(Root + "×0.3", false, 1)] private static void S03() => Set(0.3f);
        [MenuItem(Root + "×0.5", false, 2)] private static void S05() => Set(0.5f);
        [MenuItem(Root + "×1", false, 3)] private static void S1() => Set(1f);
        [MenuItem(Root + "×1.5", false, 4)] private static void S15() => Set(1.5f);
        [MenuItem(Root + "×2", false, 5)] private static void S2() => Set(2f);

        [MenuItem(Root + "×0.1", true)] private static bool V01() => Check("×0.1", 0.1f);
        [MenuItem(Root + "×0.3", true)] private static bool V03() => Check("×0.3", 0.3f);
        [MenuItem(Root + "×0.5", true)] private static bool V05() => Check("×0.5", 0.5f);
        [MenuItem(Root + "×1", true)] private static bool V1() => Check("×1", 1f);
        [MenuItem(Root + "×1.5", true)] private static bool V15() => Check("×1.5", 1.5f);
        [MenuItem(Root + "×2", true)] private static bool V2() => Check("×2", 2f);

        private static void Set(float scale)
        {
            Time.timeScale = scale;
            Debug.Log($"[TimeScale] ×{scale}");
        }

        /// <summary>Ticks the current value; available in Play mode only (outside it the value would be reset anyway).</summary>
        private static bool Check(string item, float scale)
        {
            Menu.SetChecked(Root + item, Application.isPlaying && Mathf.Approximately(Time.timeScale, scale));
            return Application.isPlaying;
        }
    }
}
