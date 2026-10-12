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
            cat.transform.position = new Vector3(0, -3.6f, 0);
            cat.transform.localScale = Vector3.one * 2f;
            cat.AddComponent<SpriteRenderer>();
            var flipbook = cat.AddComponent<SpriteFlipbook>();

            var root = new GameObject("AnimationTestScene").AddComponent<AnimationTestScene>();
            var so = new SerializedObject(root);
            so.FindProperty("character").objectReferenceValue = flipbook;
            var clips = so.FindProperty("clips");
            clips.arraySize = 2;
            FillClip(clips.GetArrayElementAtIndex(0), "idle", "Idle", 10f, true);
            FillClip(clips.GetArrayElementAtIndex(1), "punch", "Punch", 12f, true);
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
