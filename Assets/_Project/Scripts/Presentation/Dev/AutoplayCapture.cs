using System;
using System.Collections;
using System.IO;
using TrashCats.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Dev
{
    /// <summary>
    /// Development helper for headless review of builds: with "-autoplayCapture &lt;dir&gt;" it walks through the main
    /// screens by pressing buttons (like a player) and saves a screenshot of each, then quits.
    /// </summary>
    public sealed class AutoplayCapture : MonoBehaviour
    {
        private string _dir;
        private bool _battles;

        public static void AttachIfRequested(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var (flag, battles) in new[] { ("-autoplayCapture", false), ("-autoplayBattles", true) })
            {
                var index = Array.IndexOf(args, flag);
                if (index < 0 || index + 1 >= args.Length) continue;
                var capture = host.AddComponent<AutoplayCapture>();
                capture._dir = args[index + 1];
                capture._battles = battles;
                return;
            }
        }

        private IEnumerator Start() => _battles ? Battles() : Tour();

        /// <summary>Unlocks everything and fights at every territory with the whole gang, capturing each battle.</summary>
        private IEnumerator Battles()
        {
            Directory.CreateDirectory(_dir);
            yield return new WaitForSeconds(1f);
            Dismiss();
            var debug = GameBootstrap.Instance.Services.Debug;
            debug.RecruitAllAndRest();
            debug.UnlockAllTerritories();
            yield return new WaitForSeconds(0.5f);
            for (var i = 0; i < 3; i++) Dismiss();
            yield return Shot("b00_hideout_gang");
            foreach (var territory in new[] { "ter_backyard", "ter_fishshop", "ter_alley", "ter_park", "ter_rooftop" })
            {
                debug.RecruitAllAndRest();
                Click("Tab_Map");
                yield return null;
                Click("Node_" + territory);
                yield return null;
                yield return Shot($"b_{territory}_prep");
                Click("Prep_Fight");
                yield return new WaitForSeconds(1.2f);
                yield return Shot($"b_{territory}_1");
                yield return new WaitForSeconds(2.5f);
                yield return Shot($"b_{territory}_2");
                Click("Combat_Skip");
                yield return new WaitForSeconds(0.4f);
                yield return Shot($"b_{territory}_result");
                Click("Results_Continue");
                yield return new WaitForSeconds(0.4f);
                for (var i = 0; i < 4; i++) Dismiss();
            }

            Application.Quit();
        }

        private IEnumerator Tour()
        {
            Directory.CreateDirectory(_dir);
            yield return new WaitForSeconds(1f);
            Dismiss();
            yield return Shot("01_hideout");
            Click("Cat_cat_ryzhy");
            yield return Shot("02_activity_sheet");
            Click("Activity_act_train_str");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("03_hideout_training");
            Click("Station_upg_can");
            yield return Shot("04_upgrade_panel");
            Click("UpgradePanel");
            Click("Tab_Map");
            yield return Shot("05_map");
            Click("Node_ter_backyard");
            yield return Shot("06_prep");
            Click("Prep_Fight");
            yield return new WaitForSeconds(2.2f);
            yield return Shot("07_combat");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("08_combat_later");
            Click("Combat_Skip");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("09_results");
            Click("Results_Continue");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("10_after_battle");
            Dismiss();
            Click("Cat_cat_ryzhy");
            Click("Sheet_Details");
            yield return Shot("11_cat_details");
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }

        private void Dismiss()
        {
            foreach (var name in new[] { "Ok", "Message_Ok" })
                Click(name, required: false);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return null;
        }

        private static void Click(string objectName, bool required = true)
        {
            var go = GameObject.Find(objectName);
            var button = go != null ? go.GetComponent<Button>() : null;
            if (button != null && button.interactable)
                button.onClick.Invoke();
            else if (required)
                Debug.LogWarning($"[Autoplay] cannot click {objectName}");
        }
    }
}
