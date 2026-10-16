using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TrashCats.Domain;
using TrashCats.Presentation.Hideout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// A static hideout composition for placing things by hand, no gameplay: the background, the station props and the
    /// cats (one frame each) at the spots of <see cref="HideoutLayout"/>, as sprites in world space where one unit is
    /// 100 pixels of bg_hideout (the picture spans x 0..10.24, y 0..15.36; sprite pivots are at the bottom centre, so a
    /// cat's position is its foot point). Prefabs of every cat in every pose go to <see cref="PrefabRoot"/>.
    /// "Export Hideout Layout" writes what is in the scene back as picture-space numbers for HideoutLayout.
    /// </summary>
    public static class HideoutLayoutSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/HideoutLayout.unity";
        private const string PrefabRoot = "Assets/_Project/Prefabs/HideoutLayout";
        private const string ExportPath = "Docs/ArtDirection/hideout_layout_export.json";
        private const string Env = "Assets/_Project/Art/Environment/";
        private const string Chars = "Assets/_Project/Art/Characters/";
        private const float W = 10.24f, H = 15.36f;

        private static readonly (string Key, string Folder)[] Cats = { ("ryzhy", "Ryzhy"), ("shnyr", "shnyr"), ("ponchik", "ponchik") };

        private static ActivityDefinition Train(AttributeType a) => new ActivityDefinition { Kind = ActivityKind.Train, TrainedAttribute = a };
        private static readonly ActivityDefinition Punch = Train(AttributeType.Technique), Rope = Train(AttributeType.Endurance), Bench = Train(AttributeType.Strength);
        private static readonly ActivityDefinition Sleep = new ActivityDefinition { Kind = ActivityKind.Sleep };

        [MenuItem("TrashCats/Art/Build Hideout Layout Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(PrefabRoot);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = H * 0.5f + 0.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x2B, 0x2A, 0x2E, 0xFF);
            camera.transform.position = new Vector3(W * 0.5f, H * 0.5f, -10);

            var root = new GameObject("HideoutLayout").transform;
            var background = Put(root, "Background", Load(Env + "bg_hideout.png"), new Vector2(W * 0.5f, 0), 1f, -10000);
            background.GetComponent<SpriteRenderer>().color = new Color(0.92f, 0.92f, 0.92f);
            VisibleArea(root);

            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            Prop(props, "Dumpster", HideoutLayout.Dumpster);
            Prop(props, "Can", HideoutLayout.Can);
            Prop(props, "Rope", HideoutLayout.RopeProp);
            Prop(props, "BenchMat", HideoutLayout.Bench);
            Prop(props, "Bed", HideoutLayout.Bed);
            Prop(props, "Bracket", HideoutLayout.Bracket);

            var idle = new GameObject("Cats_Idle").transform;
            idle.SetParent(root, false);
            var stations = new GameObject("Cats_Stations").transform;
            stations.SetParent(root, false);
            foreach (var (key, folder) in Cats)
            {
                foreach (var pose in Poses(key, folder))
                {
                    var prefab = new GameObject($"{key}_{pose.Name}");
                    var sr = prefab.AddComponent<SpriteRenderer>();
                    sr.sprite = pose.Sprite;
                    prefab.transform.localScale = new Vector3(pose.FaceLeft ? -pose.Scale : pose.Scale, pose.Scale, 1);
                    PrefabUtility.SaveAsPrefabAsset(prefab, $"{PrefabRoot}/{prefab.name}.prefab");
                    Object.DestroyImmediate(prefab);
                }

                // the composition: the idle trio, and one cat at each station
                var poses = Poses(key, folder).ToDictionary(p => p.Name);
                Place(idle, key, poses["Idle"]);
                var station = key switch { "ryzhy" => "Punch", "shnyr" => "Rope", _ => "Bench" };
                if (poses.TryGetValue(station, out var p)) Place(stations, key, p);
                if (key == "ryzhy" && poses.TryGetValue("Sleep", out var sleep)) Place(stations, key, sleep);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HideoutLayout] scene {ScenePath}, prefabs in {PrefabRoot}");
        }

        /// <summary>Writes every sprite under the HideoutLayout root as picture-space numbers (0..1, y up) to <see cref="ExportPath"/>.</summary>
        [MenuItem("TrashCats/Art/Export Hideout Layout")]
        public static void Export()
        {
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("HideoutLayout");
            if (root == null)
            {
                EditorUtility.DisplayDialog("Trash Cats", $"Открой сцену {ScenePath}", "OK");
                return;
            }

            string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            var lines = new List<string>();
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var t = sr.transform;
                var b = sr.bounds;
                lines.Add($"  {{\"path\":\"{PathOf(t, root.transform)}\",\"sprite\":\"{(sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : "")}\",\"active\":{(sr.gameObject.activeInHierarchy ? "true" : "false")}," +
                          $"\"foot\":[{F(t.position.x / W)},{F(t.position.y / H)}],\"min\":[{F(b.min.x / W)},{F(b.min.y / H)}],\"max\":[{F(b.max.x / W)},{F(b.max.y / H)}]," +
                          $"\"height\":{F(b.size.y / H)},\"flip\":{(t.lossyScale.x < 0 != sr.flipX ? "true" : "false")},\"flipY\":{(t.lossyScale.y < 0 != sr.flipY ? "true" : "false")},\"order\":{sr.sortingOrder}," +
                          $"\"rotation\":{F(t.eulerAngles.z)},\"scale\":[{F(Mathf.Abs(t.lossyScale.x))},{F(Mathf.Abs(t.lossyScale.y))}],\"pixels\":[{F(sr.sprite != null ? sr.sprite.rect.width : 0)},{F(sr.sprite != null ? sr.sprite.rect.height : 0)}]}}");
            }

            File.WriteAllText(ExportPath, "[\n" + string.Join(",\n", lines) + "\n]\n", new UTF8Encoding(false));
            Debug.Log($"[HideoutLayout] {lines.Count} sprites -> {ExportPath}");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Trash Cats", $"Раскладка сохранена: {ExportPath} ({lines.Count} спрайтов)", "OK");
        }

        private readonly struct Pose
        {
            public readonly string Name;
            public readonly Sprite Sprite;
            public readonly Vector2 Foot;
            public readonly float Scale;
            public readonly bool FaceLeft;
            public readonly int Order;

            public Pose(string name, Sprite sprite, Vector2 foot, float scale, bool faceLeft, int order)
            {
                Name = name; Sprite = sprite; Foot = foot; Scale = scale; FaceLeft = faceLeft; Order = order;
            }
        }

        /// <summary>One frame per pose, sized and placed as the game shows it at the pose's spot.</summary>
        private static IEnumerable<Pose> Poses(string key, string folder)
        {
            var dir = Chars + folder + "/";
            var hideout = Frames(dir + "Hideout");
            var list = new List<(string Name, Sprite Sprite, HideoutLayout.CatSpot Spot)>
            {
                ("Idle", Frames(dir + "HideIdle").FirstOrDefault() ?? hideout.FirstOrDefault(), HideoutLayout.Idle(key, 0)),
                ("Punch", Frames(dir + "CanPunch").FirstOrDefault(), HideoutLayout.Spot(Punch, 0, key)),
                ("Rope", Frames(dir + "Rope").FirstOrDefault(), HideoutLayout.Spot(Rope, 0, key)),
                ("Sleep", hideout.ElementAtOrDefault(1), HideoutLayout.Spot(Sleep, 0, key)),
                ("Sit", hideout.ElementAtOrDefault(0), HideoutLayout.Idle(key, 0)),
                ("Eat", hideout.ElementAtOrDefault(2), HideoutLayout.Idle(key, 0)),
                ("Walk", hideout.ElementAtOrDefault(3), HideoutLayout.Idle(key, 0)),
            };
            foreach (var (name, sprite, spot) in list)
            {
                if (sprite == null) continue;
                var h = spot.Height > 0f ? spot.Height : sprite.rect.height * HideoutLayout.HeightPerPixel * HideoutLayout.DepthScale(spot.Foot.y) * spot.Scale;
                var depth = float.IsNaN(spot.Depth) ? spot.Foot.y - 0.001f : spot.Depth;
                yield return new Pose(name, sprite, spot.Foot, h * H / (sprite.rect.height / 100f), spot.FaceLeft, Order(depth));
            }

            var bench = Frames(dir + "Bench").FirstOrDefault();
            if (bench != null && HideoutLayout.BenchFit(key) is var (benchFoot, benchHeight, benchCanvas))
            {
                var (min, max) = (HideoutLayout.Bench.Min, HideoutLayout.Bench.Max);
                var size = max - min;
                var spot = HideoutLayout.Spot(Bench, 0, key);
                yield return new Pose("Bench", bench, min + Vector2.Scale(benchFoot, size), benchHeight * size.y * (bench.rect.height / benchCanvas) * H / (bench.rect.height / 100f), spot.FaceLeft,
                    Order(spot.Depth));
            }
        }

        private static void Place(Transform parent, string key, Pose pose)
        {
            var go = Put(parent, "", pose.Sprite, new Vector2(pose.Foot.x * W, pose.Foot.y * H), pose.Scale, pose.Order);
            go.transform.localScale = new Vector3(pose.FaceLeft ? -pose.Scale : pose.Scale, pose.Scale, 1);
            go.name = $"{key}_{pose.Name}";
        }

        /// <summary>A prop as the game places it: bottom centre, height, rotation about that point, mirroring, order.</summary>
        private static void Prop(Transform parent, string name, HideoutLayout.PropPlace place)
        {
            var sprite = Load(Env + place.Sprite + ".png");
            if (sprite == null) return;
            var scale = place.Size.y * H / (sprite.rect.height / 100f);
            var go = Put(parent, name, sprite, new Vector2(place.Foot.x * W, place.Foot.y * H), scale, Order(place.Depth));
            go.transform.localScale = new Vector3(place.Flip ? -scale : scale, scale, 1);
            go.transform.localEulerAngles = new Vector3(0, 0, place.Rotation);
        }

        private static GameObject Put(Transform parent, string name, Sprite sprite, Vector2 position, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>Nearer (lower) things draw on top, as in the game's depth sort.</summary>
        private static int Order(float depthY) => Mathf.RoundToInt(-depthY * 10000f);

        /// <summary>
        /// The part of the picture a 9:16 phone shows between the HUD and the cat cards (measured on a 540x960 capture:
        /// the picture is 540 px wide, y from 0.033 to 0.81), as a red frame.
        /// </summary>
        private static void VisibleArea(Transform root)
        {
            var line = new GameObject("VisibleArea_9x16").AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.04f;
            line.sortingOrder = 30000;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = Color.red;
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(0, 0.033f * H), new Vector3(W, 0.033f * H), new Vector3(W, 0.81f * H), new Vector3(0, 0.81f * H) });
        }

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static Sprite[] Frames(string folder) =>
            !AssetDatabase.IsValidFolder(folder)
                ? new Sprite[0]
                : AssetDatabase.FindAssets("t:Sprite", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder).OrderBy(p => p).Select(Load).Where(s => s != null).ToArray();

        private static string PathOf(Transform t, Transform root) => t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;
    }
}
