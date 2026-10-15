using System.Collections.Generic;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Battle;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.Map;
using TrashCats.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Entry point of the Game scene: builds screens and overlays from the services created by <see cref="GameBootstrap"/>,
    /// wires the game flow (hideout ⇄ map → preparation → battle → results) and lets time flow.
    /// When the Game scene is started directly in the editor, redirects to the Boot scene.
    /// </summary>
    public sealed class GameSceneRoot : MonoBehaviour
    {
        [SerializeField] private CharacterArtRegistry characterArt;
        [SerializeField] private Sprite arenaBackground;
        [SerializeField] private UiSkin uiSkin;
        [SerializeField] private Audio.AudioLibrary audioLibrary;

        private GameServices _services;
        private ScreenRouter _router;
        private OverlayStack _overlays;
        private HideoutScreen _hideout;
        private MapScreen _map;
        private BattlePrepScreen _prep;
        private CombatScreen _combat;
        private CatDetailsSheet _details;
        private UpgradePanel _upgrades;
        private MessageQueue _messages;
        private TutorialService _tutorial;
        private Audio.GameAudio _audio;
        private ConfirmModal _confirm;
        private System.Action _onButtonClicked;
        private Tutorial.TutorialOverlay _tutorialOverlay;

        /// <summary>Widget the tutorial points at right now (dev autoplay).</summary>
        public string TutorialTargetName => _tutorialOverlay?.TargetName;

        /// <summary>Dev autoplay: ends the guided tutorial so screenshots show the scene.</summary>
        public void DevSkipTutorial() => _tutorial?.Skip();

        /// <summary>Dev: the cat currently in the battle ring.</summary>
        public Characters.CharacterView DevPlayerFighter => _combat?.DevPlayerView;
        private OfflineSummaryModal _offline;
        private GameObject _background;
        private readonly List<System.IDisposable> _subscriptions = new List<System.IDisposable>();

        public bool IsBooted { get; private set; }

        private void Awake()
        {
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
                return;
            }

            _services = bootstrap.Services;
            Build(bootstrap);
            Subscribe();

            IsBooted = true;
            if (_services.Session.World.PendingBattle != null)
            {
                // the battle was decided before the app closed: show its results right away
                _router.Show(ScreenId.Combat);
                _messages.Paused = true;
                _combat.Play(resultsOnly: true);
            }
            else
            {
                _router.Show(ScreenId.Hideout);
                var world = _services.Session.World;
                // a fresh game (the new-game save in the boot scene already ticks a few milliseconds)
                if (world.GameTimeMs < 60_000 && !world.Tutorial.Skipped && !world.Tutorial.Completed.Any()
                    && world.Territories.All.Values.All(t => t.Attempts == 0))
                    _messages.Enqueue(Loc.T("game_scene_root.01"),
                        Loc.T("game_scene_root.02"), "home");
            }

            _tutorial.Evaluate();
            bootstrap.StartTicking();
            Dev.AutoplayCapture.AttachIfRequested(gameObject);
        }

        private void Build(GameBootstrap bootstrap)
        {
            var session = _services.Session;
            Ui.Skin = uiSkin;
            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas("UI", 0);
            canvas.transform.SetParent(transform, false);
            var background = Ui.Panel(canvas.transform, "Background", Palette.Background);
            background.rectTransform.Fill();
            _background = background.gameObject;

            var safe = Ui.Rect(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var screens = Ui.Rect(safe, "Screens").Fill();
            var navRoot = Ui.Rect(safe, "Nav").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, HideoutScreen.NavHeight));
            var overlayRoot = Ui.Rect(safe, "Overlays").Fill();

            _overlays = new OverlayStack();
            _router = new ScreenRouter(navRoot);
            var factory = new CharacterViewFactory(characterArt, arenaBackground);

            // overlays (created in the overlay layer, shown above every screen and the nav)
            _details = new CatDetailsSheet(session, overlayRoot, _overlays, ShowResult, factory);
            _upgrades = new UpgradePanel(session, overlayRoot, _overlays, ShowResult, id => factory.GetSprite(HideoutLayout.UpgradeSprite(id)));
            _audio = new Audio.GameAudio(audioLibrary, transform)
            {
                SfxVolume = _services.Settings.SfxVolume,
                MusicVolume = _services.Settings.MusicVolume,
            };
            _confirm = new ConfirmModal(overlayRoot, _overlays);
            var settings = new SettingsPanel(overlayRoot, _overlays, _services.Settings, _audio,
                () => _services.SaveScheduler.MarkDirty(SaveUrgency.Deferred), _confirm, bootstrap.ResetProgress);
            var debugTools = Debug.isDebugBuild || Application.isEditor;
            var debugPanel = debugTools ? new DebugPanel(overlayRoot, _overlays, session, _services.Debug, bootstrap.ResetProgress) : null;
            var results = new ResultsModal(session, overlayRoot, _overlays);
            _offline = new OfflineSummaryModal(overlayRoot, _overlays, id => factory.Portrait(session.Config.GetCat(id).ViewKey));
            _messages = new MessageQueue(overlayRoot, _overlays);

            // screens
            _hideout = new HideoutScreen(_services, screens, overlayRoot, _overlays, _details, _upgrades, debugPanel, ShowResult, factory, settings.Open);
            _map = new MapScreen(session, screens, OpenPrep, factory.GetSprite("bg_map"), factory.GetSprite);
            _prep = new BattlePrepScreen(session, screens, () => _router.Show(ScreenId.Map), _ => StartCombat(), _details.Open, factory);
            _combat = new CombatScreen(session, screens, transform, factory, results);
            _combat.SetSpeed(_services.Settings.BattleSpeed);
            _combat.SpeedChanged += speed =>
            {
                _services.Settings.BattleSpeed = speed;
                _services.SaveScheduler.MarkDirty(SaveUrgency.Deferred);
            };
            results.SetHandler(FinishBattle);

            // the arena is drawn by the world camera behind the UI: the opaque UI background must not cover it
            _router.Changed += id => _background.SetActive(id != ScreenId.Combat);
            _router.Register(ScreenId.Hideout, _hideout.Root, withNav: true, () => _hideout.RefreshTexts());
            _router.Register(ScreenId.Map, _map.Root, withNav: true, () => _map.Refresh());
            _router.Register(ScreenId.BattlePrep, _prep.Root, withNav: false);
            _router.Register(ScreenId.Combat, _combat.Root, withNav: false);
            foreach (var overlay in new[] { "CatDetails", "UpgradePanel", "Results", "OfflineSummary", "Message", "DebugPanel", "ActivitySheet", "SettingsPanel", "Confirm" })
            {
                var t = overlayRoot.Find(overlay);
                if (t != null) t.SetAsLastSibling();
            }

            // onboarding: guided steps on their own top canvas, one-shot hints through the message queue
            _tutorial = new TutorialService(session, _services.Bus);
            _tutorialOverlay = new Tutorial.TutorialOverlay(_tutorial, session, _overlays, transform);
            _tutorial.HintRequested += id =>
            {
                var hint = Tutorial.TutorialOverlay.Hint(id);
                if (hint != null) _messages.Enqueue(hint.Value.Title, hint.Value.Body, Tutorial.TutorialOverlay.HintIcon(id));
            };

            // sound: clicks, opened sheets/modals, music per screen (CONTENT_SPEC §10)
            _onButtonClicked = () => _audio.Play("ui_tap");
            Ui.ButtonClicked += _onButtonClicked;
            // bottom sheets rustle like paper as they slide up
            _overlays.Pushed += root => _audio.Play(root.name == "Message" ? "ui_popup" : OverlayAnimator.IsBottomSheet(root) ? "ui_paper" : "ui_open");
            _router.Changed += id => _audio.PlayMusic(id == ScreenId.Combat ? "music_battle" : "music_hideout");
            _combat.Audio = _audio;
        }

        private void PlayHideoutSound(ActivityCycleCompleted e)
        {
            if (_router.Current != ScreenId.Hideout)
                return;
            var activity = _services.Config.GetActivity(e.ActivityId);
            var key = activity.Kind switch
            {
                ActivityKind.Rummage => "hid_dumpster",
                ActivityKind.Sleep => "hid_sleep",
                ActivityKind.Scavenge => "ui_reward",
                _ => activity.TrainedAttribute switch
                {
                    AttributeType.Technique => "hid_can",
                    AttributeType.Endurance => "hid_mouse",
                    _ => "hid_wheel",
                },
            };
            _audio.Play(key, activity.Kind == ActivityKind.Sleep ? 0.5f : 1f);
        }

        private void Subscribe()
        {
            var bus = _services.Bus;
            var config = _services.Config;
            _subscriptions.Add(bus.Subscribe<DomainEvent>(_ => RefreshAll()));
            _subscriptions.Add(bus.Subscribe<OfflineProgressApplied>(e => _offline.Show(e.Summary)));
            _subscriptions.Add(bus.Subscribe<ActivityCycleCompleted>(PlayHideoutSound));
            _subscriptions.Add(bus.Subscribe<ResourcesChanged>(e => { if (e.FoodDelta < 0) _audio.Play("hid_eat"); }));
            _subscriptions.Add(bus.Subscribe<UpgradePurchased>(_ => _audio.Play("ui_buy")));
            _subscriptions.Add(bus.Subscribe<AttributeLevelUp>(_ => { if (_router.Current == ScreenId.Hideout) _audio.Play("hid_levelup", 0.8f); }));
            _subscriptions.Add(bus.Subscribe<CatRecruited>(e =>
            {
                _hideout.RebuildCats();
                _audio.Play("hid_meow");
                var cat = config.GetCat(e.CatId);
                _messages.Enqueue(Loc.T("game_scene_root.03"), Loc.T("game_scene_root.04", Texts.CatName(e.CatId), Texts.Attribute(cat.Aptitude)), Texts.NewCatIcon(cat));
            }));
            _subscriptions.Add(bus.Subscribe<TerritoryCaptured>(e =>
            {
                if (e.Unlocked is TerritoryId next)
                    _messages.Enqueue(Loc.T("game_scene_root.05"), Loc.T("game_scene_root.06", Texts.TerritoryName(next)), "flag");
            }));
            _subscriptions.Add(bus.Subscribe<SkillUnlocked>(e =>
            {
                var skill = config.GetSkill(e.SkillId);
                _messages.Enqueue(Loc.T("game_scene_root.07"), Loc.T("game_scene_root.08", Texts.CatName(e.CatId), Texts.SkillName(e.SkillId), Texts.SkillDescription(skill)), "skill");
            }));
            _subscriptions.Add(bus.Subscribe<FinalReached>(_ =>
                _messages.Enqueue(Loc.T("game_scene_root.09"), Loc.T("game_scene_root.10"), "final")));
            _subscriptions.Add(bus.Subscribe<AttributeLevelUp>(e =>
            {
                if (_router.Current == ScreenId.Hideout)
                    _hideout.Toast($"{Texts.CatName(e.CatId)}: {Texts.Attribute(e.Attribute)} {e.NewLevel}!", Texts.AttributeIcon(e.Attribute));
            }));
        }

        private void RefreshAll()
        {
            _hideout.RefreshTexts();
            if (_router.Current == ScreenId.Map) _map.Refresh();
            if (_router.Current == ScreenId.BattlePrep) _prep.Refresh();
            if (_details.IsOpen) _details.Refresh();
            _upgrades.Refresh();
            _router.SetMapBadge(_map.HasNewTarget && _router.Current != ScreenId.Map);
        }

        private void OpenPrep(TerritoryId territory)
        {
            _prep.Open(territory);
            _router.Show(ScreenId.BattlePrep);
            var min = _services.Config.Needs.MinEnergyForBattle;
            _tutorial.OnPrepOpened(_services.Session.World.Cats.Count(c => c.Energy >= min));
        }

        private void StartCombat()
        {
            _router.Show(ScreenId.Combat);
            _messages.Paused = true;
            _combat.Play(resultsOnly: false);
        }

        private void FinishBattle(TerritoryId territory, bool retry)
        {
            _services.Session.CompleteBattle();
            _combat.Hide();
            if (retry)
                OpenPrep(territory);
            else
                _router.Show(ScreenId.Hideout);
            _messages.Paused = false;
        }

        private void ShowResult(CommandResult result)
        {
            if (!result.IsOk)
            {
                _hideout.Toast(Texts.Error(result.Error));
                _audio.Play("ui_error");
            }
        }

        private void Update()
        {
            if (!IsBooted)
                return;

            _hideout.Tick();
            _combat.Tick(Time.deltaTime);
            var keyboard = Keyboard.current;
            // Android "Back" arrives as Escape: close the top sheet/modal, else step back a screen, else offer to quit
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_overlays.Back())
            {
                if (_router.Current == ScreenId.BattlePrep) _router.Show(ScreenId.Map);
                else if (_router.Current == ScreenId.Map) _router.Show(ScreenId.Hideout);
                else if (_router.Current == ScreenId.Hideout)
                    _confirm.Ask(Loc.T("quit.title"), Loc.T("quit.body"), Loc.T("quit.yes"), Application.Quit);
            }
        }

        private void LateUpdate()
        {
            if (IsBooted)
                _tutorialOverlay.LateTick();
        }

        private void OnDestroy()
        {
            _tutorialOverlay?.Dispose();
            if (_onButtonClicked != null) Ui.ButtonClicked -= _onButtonClicked;
            _tutorial?.Dispose();
            foreach (var s in _subscriptions) s.Dispose();
            _hideout?.Dispose();
        }
    }
}
