using System.IO;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Infrastructure;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// A static hideout screen for placing things by hand, no gameplay: the real HUD, background, station props with
    /// their plates, the cat panel and the bottom navigation, built by the game's own views in edit mode and saved as
    /// plain objects. One cat is sent out for food and one into the dumpster, so the "away" badges (portrait, caption
    /// and cycle bar) stand at the exit and over the dumpster; then every cat in the yard is removed. The canvas is in
    /// world space at the reference size (1080x1920), so what is placed does not depend on the Game view.
    /// </summary>
    public static class UiLayoutSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/UiLayout.unity";

        [MenuItem("TrashCats/Art/Build UI Layout Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = ConfigLoader.Load(Find<GameConfigAsset>());
            var bus = new EventBus { OnHandlerException = Debug.LogException };
            var clock = new SystemClock();
            var session = new GameSession(GameWorld.CreateNew(config, 1), clock, bus, NoSaveRequests.Instance, clock.UtcNowMs);
            var debug = new DebugCommands(session, bus, NoSaveRequests.Instance);
            session.SkipTutorial();
            debug.RecruitAllAndRest();
            debug.AddResources(240, 85);
            var cats = session.World.Cats;
            Assign(session, cats[0], ActivityKind.Scavenge);
            Assign(session, cats[1], ActivityKind.Rummage);
            // part of the way through the cycle, so the bars show some fill
            debug.SkipGameTime(20_000);

            var size = Ui.ReferenceResolution;
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size.y * 0.5f + 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x2B, 0x2A, 0x2E, 0xFF);
            camera.transform.position = new Vector3(0, 0, -100);

            Ui.Skin = Find<UiSkin>();
            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas("UI", 0);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = size;
            canvasRect.position = Vector3.zero;
            Ui.Panel(canvas.transform, "Background", Palette.Background).rectTransform.Fill();

            var safe = Ui.Rect(canvas.transform, "SafeArea").Fill();
            var screens = Ui.Rect(safe, "Screens").Fill();
            var navRoot = Ui.Rect(safe, "Nav").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, HideoutScreen.NavHeight));
            var overlayRoot = Ui.Rect(safe, "Overlays").Fill();

            var overlays = new OverlayStack();
            var router = new ScreenRouter(navRoot);
            var art = new CharacterViewFactory(Find<CharacterArtRegistry>(), null);
            var details = new CatDetailsSheet(session, overlayRoot, overlays, _ => { }, art);
            var upgrades = new UpgradePanel(session, overlayRoot, overlays, _ => { }, id => art.GetSprite(HideoutLayout.UpgradeSprite(id)));
            var hideout = new HideoutScreen(new GameServices(config, clock, bus, session, null, null, debug), screens, overlayRoot, overlays, details, upgrades,
                null, _ => { }, art, () => { });
            router.Register(ScreenId.Hideout, hideout.Root, withNav: true, hideout.RefreshTexts);
            router.Show(ScreenId.Hideout);

            // plates, bars and badges follow their props in Tick, measured on the laid-out canvas
            for (var i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                hideout.Tick();
            }
            hideout.Dispose();

            // no cats in the yard; the sheets and modals stay out of the way
            foreach (var t in screens.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Cat_")).ToArray())
                Object.DestroyImmediate(t.gameObject);
            for (var i = overlayRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(overlayRoot.GetChild(i).gameObject);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[UiLayout] scene {ScenePath}");
        }

        /// <summary>Batch mode: renders the saved scene to the PNG given after -capturePath (a look without opening the editor).</summary>
        public static void Capture()
        {
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-capturePath");
            var path = at >= 0 && at + 1 < args.Length ? args[at + 1] : "ui_layout.png";
            EditorSceneManager.OpenScene(ScenePath);
            Canvas.ForceUpdateCanvases();
            var camera = Camera.main;
            var target = new RenderTexture(540, 980, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            File.WriteAllBytes(path, picture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
        }

        private static void Assign(GameSession session, IReadOnlyCat cat, ActivityKind kind)
        {
            var activity = session.Config.Activities.First(a => a.Kind == kind);
            var result = session.AssignActivity(cat.Id, activity.Id);
            if (!result.IsOk)
                Debug.LogWarning($"[UiLayout] {cat.Id} -> {activity.Id}: {result.Error}");
        }

        private static T Find<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).FirstOrDefault(a => a != null);
    }
}
