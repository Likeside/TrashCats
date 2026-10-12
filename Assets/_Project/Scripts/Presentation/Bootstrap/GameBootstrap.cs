using TrashCats.App;
using TrashCats.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>
    /// Composition Root. Lives in the Boot scene, survives scene loads, builds and owns all game services and drives
    /// the per-frame tick. Time starts flowing only when the Game scene has subscribed to events
    /// (<see cref="StartTicking"/>), so the offline summary is not lost.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string BootSceneName = "Boot";
        public const string GameSceneName = "Game";

        /// <summary>Tests point saves to a temporary folder instead of the real persistent data path.</summary>
        public static string SaveDirectoryOverride { get; set; }

        [SerializeField] private GameConfigAsset gameConfig;

        private SaveService _saveService;
        private AppLifecycleListener _lifecycle;
        private bool _ticking;
        private bool _resetting;

        public static GameBootstrap Instance { get; private set; }

        public GameServices Services { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Services = Compose();
        }

        private void Start()
        {
            SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        }

        private void Update()
        {
            if (!_ticking)
                return;

            Services.Session.Tick();
            Services.SaveScheduler.Update();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            if (!_resetting)
                Services?.SaveScheduler.SaveNow();
            Instance = null;
        }

        /// <summary>Called by the Game scene once its views are subscribed. Performs the offline catch-up.</summary>
        public void StartTicking()
        {
            _ticking = true;
            Services.Session.Tick();
        }

        /// <summary>Deletes the save and restarts from the Boot scene (debug menu).</summary>
        public void ResetProgress()
        {
            _resetting = true;
            _saveService.DeleteAll();
            Destroy(gameObject);
            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Single);
        }

        private GameServices Compose()
        {
            var config = ConfigLoader.Load(gameConfig);
            var clock = new SystemClock();
            var bus = new EventBus { OnHandlerException = Debug.LogException };
            var storage = new JsonFileSaveStorage(SaveDirectoryOverride ?? Application.persistentDataPath);
            _saveService = new SaveService(storage, config, clock, indented: Debug.isDebugBuild);

            var outcome = _saveService.LoadOrCreate();
            foreach (var problem in outcome.Problems)
                Debug.LogWarning($"[Save] {problem}");
            foreach (var warning in outcome.Game.Warnings)
                Debug.LogWarning($"[Save] repaired: {warning}");
            Debug.Log($"[Save] loaded from {outcome.Source}");

            var scheduler = new SaveScheduler(_saveService, clock);
            var session = new GameSession(outcome.Game.World, clock, bus, scheduler, outcome.Game.LastTickUtcMs);
            var settings = outcome.Game.Settings;
            scheduler.Attach(session, () => settings);
            scheduler.SaveFailed += Debug.LogException;
            if (outcome.Source == SaveSource.NewGame)
                scheduler.MarkDirty(SaveUrgency.Immediate);

            _lifecycle = gameObject.AddComponent<AppLifecycleListener>();
            _lifecycle.Paused += () => { if (!_resetting) scheduler.SaveNow(); };
            _lifecycle.Quitting += () => { if (!_resetting) scheduler.SaveNow(); };
            _lifecycle.Resumed += () => { if (_ticking) session.Tick(); };

            return new GameServices(config, clock, bus, session, scheduler, settings, new DebugCommands(session, bus, scheduler));
        }
    }
}
