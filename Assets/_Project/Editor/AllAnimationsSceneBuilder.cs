using System.Collections.Generic;
using System.Linq;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// Builds Scenes/Dev/AllAnimations.unity: every character of the art registry (cats, enemies and their variants) in
    /// a row, one object per clip it has (battle and hideout clips, the tail, the static hideout poses), each playing its
    /// own animation in a loop, with a caption. Open the scene and press Play; it is larger than the camera, look
    /// around in the Scene view.
    /// </summary>
    public static class AllAnimationsSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/AllAnimations.unity";
        private const string RegistryPath = "Assets/_Project/Configs/CharacterArt.asset";
        private const float ColumnStep = 6.5f;
        private const float RowStep = 7.5f;

        private static readonly string[] Roles =
        {
            "idle", "attack", "heavyAttack", "hit", "knockOut", "tail", "hideoutIdle", "rope", "dig", "run", "bench", "canPunch",
        };

        [MenuItem("TrashCats/Dev/Rebuild All Animations Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var registry = AssetDatabase.LoadAssetAtPath<CharacterArtRegistry>(RegistryPath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 14f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x2B, 0x2A, 0x2E, 0xFF);
            camera.transform.position = new Vector3(ColumnStep * 4f, -RowStep * 1.5f, -10);

            var root = new GameObject("AllAnimations").transform;
            var row = 0;
            var count = 0;
            foreach (var art in registry.characters.Where(a => a != null))
            {
                var y = -row * RowStep;
                var name = new GameObject(art.viewKey).transform;
                name.SetParent(root, false);
                name.localPosition = new Vector3(-ColumnStep * 0.75f, y + 2.5f, 0);
                Caption(name, art.viewKey, 0.12f, Color.yellow);

                var column = 0;
                var roles = Roles.Where(r => ClipShowcase.Clip(art, r) is FlipbookClip c && c.frames != null && c.frames.Length > 0)
                    // a role that only reuses another clip (heavy = attack when there is no own heavy clip) is shown once
                    .Where(r => r != "heavyAttack" || art.attack?.frames == null || !art.heavyAttack.frames.SequenceEqual(art.attack.frames))
                    .Concat(Enumerable.Range(0, art.hideoutPoses?.Length ?? 0).Where(i => art.hideoutPoses[i] != null).Select(i => "pose_" + i))
                    .ToList();
                foreach (var role in roles)
                {
                    var go = new GameObject($"{art.viewKey}_{role}");
                    go.transform.SetParent(name, false);
                    go.transform.position = new Vector3(column * ColumnStep, y, 0);
                    go.transform.localScale = Vector3.one * (art.battleScale > 0 ? art.battleScale : 1f);
                    go.AddComponent<SpriteRenderer>();
                    go.AddComponent<SpriteFlipbook>();
                    var showcase = go.AddComponent<ClipShowcase>();
                    showcase.registry = registry;
                    showcase.viewKey = art.viewKey;
                    showcase.role = role;
                    EditorUtility.SetDirty(showcase);
                    var first = role.StartsWith("pose_") ? art.HideoutPose(int.Parse(role.Substring(5))) : ClipShowcase.Clip(art, role).frames[0];
                    go.GetComponent<SpriteRenderer>().sprite = first; // visible in the Scene view before Play

                    var label = new GameObject("Caption").transform;
                    label.SetParent(name, false);
                    label.position = new Vector3(column * ColumnStep, y - 0.9f, 0);
                    Caption(label, role, 0.08f, Color.white);
                    column++;
                    count++;
                }
                row++;
            }

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[AllAnimations] {row} characters, {count} animated objects -> {ScenePath}");
        }

        private static void Caption(Transform at, string text, float size, Color color)
        {
            var mesh = at.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 96;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.color = color;
        }
    }
}
