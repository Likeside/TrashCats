using System.Linq;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>Builds the animation review scene from the frames in Assets/_Project/Art/Characters/Ryzhy.</summary>
    public static class AnimationTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/AnimationTest.unity";
        private const string FramesRoot = "Assets/_Project/Art/Characters/Ryzhy";

        [MenuItem("TrashCats/Dev/Rebuild Animation Test Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x2B, 0x2A, 0x2E, 0xFF);
            camera.transform.position = new Vector3(0, 0, -10);

            var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
            ground.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            ground.color = new Color32(0x13, 0x17, 0x1E, 0xFF);
            ground.drawMode = SpriteDrawMode.Sliced;
            ground.size = new Vector2(12f, 6f);
            ground.transform.position = new Vector3(0, -6.5f, 1);

            var cat = new GameObject("Ryzhy");
            cat.transform.position = new Vector3(-1.4f, -3.6f, 0); // pivot is the planted back foot
            cat.transform.localScale = Vector3.one * 2f;
            cat.AddComponent<SpriteRenderer>();
            var flipbook = cat.AddComponent<SpriteFlipbook>();

            // tail layer: pivot = cut base of the tail, attached where it was cut from the body (Tools/split_tail.py)
            var tail = new GameObject("Tail");
            tail.transform.SetParent(cat.transform, false);
            tail.transform.localPosition = new Vector3(0.12f, 1.49f, 0f);
            var tailRenderer = tail.AddComponent<SpriteRenderer>();
            tailRenderer.sortingOrder = -1;
            var tailFlipbook = tail.AddComponent<SpriteFlipbook>();
            var tailSo = new SerializedObject(tailFlipbook);
            tailSo.FindProperty("target").objectReferenceValue = tailRenderer;
            tailSo.ApplyModifiedPropertiesWithoutUndo();
            var attachment = tail.AddComponent<FlipbookAttachment>();
            var attachmentSo = new SerializedObject(attachment);
            attachmentSo.FindProperty("body").objectReferenceValue = flipbook;
            attachmentSo.ApplyModifiedPropertiesWithoutUndo();

            var root = new GameObject("AnimationTestScene").AddComponent<AnimationTestScene>();
            var so = new SerializedObject(root);
            so.FindProperty("character").objectReferenceValue = flipbook;
            var clips = so.FindProperty("clips");
            clips.arraySize = 2;
            FillClip(clips.GetArrayElementAtIndex(0), "idle", "Idle", 8f, true);
            clips.GetArrayElementAtIndex(0).FindPropertyRelative("breathe").floatValue = 0.012f;
            clips.GetArrayElementAtIndex(0).FindPropertyRelative("breathePeriod").floatValue = 2.4f;
            FillClip(clips.GetArrayElementAtIndex(1), "punch", "Punch", 12f, true);
            var attach = AttachPoints.Load($"{FramesRoot}/attach_points.json");
            SetAttachPoints(clips.GetArrayElementAtIndex(0), attach.idle);
            SetAttachPoints(clips.GetArrayElementAtIndex(1), attach.punch);
            so.FindProperty("tail").objectReferenceValue = tailFlipbook;
            FillClip(so.FindProperty("tailClip"), "tail", "Tail", 9f, true); // full swish cycle, loops on its own
            var labels = so.FindProperty("buttonLabels");
            labels.arraySize = 2;
            labels.GetArrayElementAtIndex(0).stringValue = "Стойка";
            labels.GetArrayElementAtIndex(1).stringValue = "Удар";
            so.ApplyModifiedPropertiesWithoutUndo();

            var catSo = new SerializedObject(flipbook);
            catSo.FindProperty("target").objectReferenceValue = cat.GetComponent<SpriteRenderer>();
            catSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AnimationTest] scene saved: " + ScenePath);
        }

        private static void SetAttachPoints(SerializedProperty clip, Vector2[] points)
        {
            var array = clip.FindPropertyRelative("attachPoints");
            array.arraySize = points.Length;
            for (var i = 0; i < points.Length; i++)
                array.GetArrayElementAtIndex(i).vector2Value = points[i];
        }

        /// <summary>attach_points.json: {"idle": [[x, y]], "punch": [[x, y], ...]} in local units from the body pivot.</summary>
        private sealed class AttachPoints
        {
            public Vector2[] idle;
            public Vector2[] punch;

            public static AttachPoints Load(string path)
            {
                var json = System.IO.File.ReadAllText(path);
                return new AttachPoints { idle = Parse(json, "idle"), punch = Parse(json, "punch") };
            }

            private static Vector2[] Parse(string json, string key)
            {
                var start = json.IndexOf('"' + key + '"');
                var open = json.IndexOf('[', start);
                var depth = 0;
                var end = open;
                for (; end < json.Length; end++)
                {
                    if (json[end] == '[') depth++;
                    else if (json[end] == ']' && --depth == 0) break;
                }

                var numbers = System.Text.RegularExpressions.Regex.Matches(json.Substring(open, end - open), @"-?\d+(\.\d+)?")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                return Enumerable.Range(0, numbers.Length / 2).Select(i => new Vector2(numbers[i * 2], numbers[i * 2 + 1])).ToArray();
            }
        }

        private static void FillClip(SerializedProperty clip, string name, string folder, float fps, bool loop)
        {
            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { $"{FramesRoot}/{folder}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .ToArray();

            clip.FindPropertyRelative("name").stringValue = name;
            clip.FindPropertyRelative("fps").floatValue = fps;
            clip.FindPropertyRelative("loop").boolValue = loop;
            var array = clip.FindPropertyRelative("frames");
            array.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
