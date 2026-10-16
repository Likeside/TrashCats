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
        private bool _tutorial;
        private bool _punch;
        private bool _gym;

        public static void AttachIfRequested(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var flag in new[] { "-autoplayCapture", "-autoplayBattles", "-autoplayTutorial", "-autoplayPunch", "-autoplayGym" })
            {
                var index = Array.IndexOf(args, flag);
                if (index < 0 || index + 1 >= args.Length) continue;
                Application.runInBackground = true; // the automated run must not pause when its window loses focus
                var capture = host.AddComponent<AutoplayCapture>();
                capture._dir = args[index + 1];
                capture._battles = flag == "-autoplayBattles";
                capture._tutorial = flag == "-autoplayTutorial";
                capture._punch = flag == "-autoplayPunch";
                capture._gym = flag == "-autoplayGym";
                return;
            }
        }

        private IEnumerator Start() => _gym ? Gym() : _punch ? PunchFrames() : _tutorial ? TutorialRun() : _battles ? Battles() : Tour();

        /// <summary>Starts a battle and freezes Ryzhy on each frame of his attack clip, one screenshot per frame (tail check).</summary>
        private IEnumerator PunchFrames()
        {
            Directory.CreateDirectory(_dir);
            yield return new WaitForSeconds(1f);
            Dismiss();
            GetComponent<GameSceneRoot>().DevSkipTutorial();
            var debug = GameBootstrap.Instance.Services.Debug;
            debug.UnlockAllTerritories();
            Click("Tab_Map");
            yield return null;
            Click("Node_ter_backyard");
            yield return null;
            Click("Prep_Fight");
            yield return new WaitForSeconds(0.6f);
            var view = GetComponent<GameSceneRoot>().DevPlayerFighter;
            if (view == null) { Application.Quit(); yield break; }
            Time.timeScale = 0f;
            var count = view.DevShowAttackFrame(0);
            for (var f = 0; f < count; f++)
            {
                view.DevShowAttackFrame(f);
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"p{f:00}.png"));
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
            }
            Application.Quit();
        }

        /// <summary>
        /// New game: follows the guided tutorial like a player (taps whatever it points at), skipping game time while
        /// nothing is asked, and captures every state.
        /// </summary>
        private IEnumerator TutorialRun()
        {
            Directory.CreateDirectory(_dir);
            var root = GetComponent<GameSceneRoot>();
            var debug = GameBootstrap.Instance.Services.Debug;
            yield return new WaitForSeconds(1f);
            for (var i = 0; i < 24; i++)
            {
                yield return new WaitForSeconds(0.5f);
                var target = root.TutorialTargetName;
                yield return Shot($"t{i:00}_{target ?? "free"}");
                if (target != null)
                    Click(target);
                else if (GameObject.Find("Message_Ok") != null || GameObject.Find("Results_Continue") != null)
                {
                    Dismiss();
                    Click("Results_Continue", required: false);
                }
                else
                    debug.SkipGameTime(2 * 60_000L);
            }

            Application.Quit();
        }

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
            GetComponent<GameSceneRoot>().DevSkipTutorial();
            Dismiss();
            yield return null;
            yield return Shot("01_hideout");
            Click("CatCard0");
            yield return Shot("02_activity_sheet");
            Click("Activity_act_train_str");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("03_hideout_training");
            yield return new WaitForSeconds(0.17f);
            yield return Shot("03b_punch_frame");
            // the activity sheet of a working cat (cycle bars, Stop), then of a tired and hungry one (Sleep and Feed advised)
            var services = GameBootstrap.Instance.Services;
            Click("CatCard0");
            yield return Shot("02b_sheet_working");
            Click("ActivitySheet");
            services.Debug.SkipGameTime(10 * 60 * 1000);
            yield return null;
            Dismiss(); Dismiss();
            Click("CatCard0");
            yield return Shot("02c_sheet_tired");
            Click("ActivitySheet");
            // the whole gang at the stations: bench mat, bed, dumpster
            services.Debug.RecruitAllAndRest();
            yield return null;
            Dismiss(); Dismiss();
            foreach (var cat in services.Session.World.Cats) services.Session.StopActivity(cat.Id);
            yield return new WaitForSeconds(0.8f);
            Dismiss(); Dismiss();
            yield return Shot("03a_idle_three");
            // the cat details and the battle prep with the whole gang rested (win chance bar)
            Click("CatCard0");
            Click("Sheet_Details");
            yield return Shot("03h_cat_details");
            Click("Details_Close", required: false);
            GameObject.Find("ActivitySheet")?.SetActive(false);
            Click("Tab_Map");
            Click("Node_ter_backyard");
            yield return Shot("03i_prep_rested");
            Click("Prep_Back");
            Click("Tab_Hideout", required: false);
            yield return new WaitForSeconds(0.3f);
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_shnyr"), new TrashCats.Domain.ActivityId("act_train_tou"));
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_ponchik"), new TrashCats.Domain.ActivityId("act_sleep"));
            yield return new WaitForSeconds(0.8f);
            Dismiss(); Dismiss();
            yield return Shot("03c_stations");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("03d_stations_later");
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_ponchik"), new TrashCats.Domain.ActivityId("act_train_str"));
            services.Debug.SkipGameTime(20 * 60_000L);
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_ponchik"), new TrashCats.Domain.ActivityId("act_sleep"));
            yield return new WaitForSeconds(0.8f);
            for (var i = 0; i < 4; i++) Dismiss();
            yield return Shot("03s_sleep");
            // each cat on the bench, then rested again for the dumpster and the street
            foreach (var who in new[] { "cat_ryzhy", "cat_shnyr", "cat_ponchik" })
            {
                foreach (var cat in services.Session.World.Cats) services.Session.StopActivity(cat.Id);
                services.Debug.RecruitAllAndRest();
                services.Session.AssignActivity(new TrashCats.Domain.CatId(who), new TrashCats.Domain.ActivityId("act_train_str"));
                yield return new WaitForSeconds(0.8f);
                for (var i = 0; i < 4; i++) Dismiss();
                yield return Shot("03b_bench_" + who);
                // the push-up variants change after every rep
                for (var rep = 1; rep <= 3 && who != "cat_ponchik"; rep++)
                {
                    yield return new WaitForSeconds(1.45f);
                    yield return Shot($"03b_bench_{who}_{rep}");
                }
            }
            foreach (var cat in services.Session.World.Cats) services.Session.StopActivity(cat.Id);
            services.Debug.RecruitAllAndRest();
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_ryzhy"), new TrashCats.Domain.ActivityId("act_train_agi"));
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_shnyr"), new TrashCats.Domain.ActivityId("act_rummage"));
            services.Session.AssignActivity(new TrashCats.Domain.CatId("cat_ponchik"), new TrashCats.Domain.ActivityId("act_scavenge"));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("03f_rope_dig_away");
            yield return new WaitForSeconds(0.3f);
            yield return Shot("03g_rope_dig_away");
            services.Debug.SimulateAbsence(25 * 60_000L);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("03e_offline");
            Click("Ok", required: false);
            for (var i = 0; i < 4; i++) Dismiss();
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
            Click("CatCard0");
            Click("Sheet_Details");
            yield return Shot("11_cat_details");
            Click("Details_Close", required: false);
            GameObject.Find("CatDetails")?.SetActive(false);
            yield return new WaitForSeconds(0.3f);
            Click("Hud_Settings");
            yield return Shot("12_settings");
            Click("Settings_Reset");
            yield return Shot("13_reset_confirm");
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }

        /// <summary>Ponchik on the bench, Ryzhy and Shnyr on the skipping rope: a burst of screenshots (the press, landing dust).</summary>
        private IEnumerator Gym()
        {
            Directory.CreateDirectory(_dir);
            yield return new WaitForSeconds(1f);
            Dismiss();
            GetComponent<GameSceneRoot>().DevSkipTutorial();
            var services = GameBootstrap.Instance.Services;
            services.Debug.RecruitAllAndRest();
            yield return null;
            Dismiss(); Dismiss();
            var session = services.Session;
            foreach (var cat in session.World.Cats)
            {
                var activity = cat.Id.Value == "cat_ponchik" ? "act_train_str" : "act_train_tou";
                session.AssignActivity(cat.Id, new Domain.ActivityId(activity));
            }
            yield return new WaitForSeconds(1.5f);
            Dismiss(); Dismiss();
            for (var i = 0; i < 40; i++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"gym_{i:00}.png"));
                yield return new WaitForSeconds(0.05f);
            }
            // the street's own upgrade (the EXIT sign), apart from the dumpster's
            services.Debug.AddResources(0, 100);
            yield return null;
            Click("Station_upg_street");
            yield return Shot("gym_street_upgrade");
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
