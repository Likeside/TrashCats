using System.Linq;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// The loading screen's picture, logo and skin on the Boot scene's GameBootstrap, and the app icon (Art/AppIcon/app_icon)
    /// as the default icon of every platform (Android takes it as its legacy icon).
    /// </summary>
    public static class LoadingAndIconSetup
    {
        private const string BootScene = "Assets/_Project/Scenes/Boot.unity";
        private const string LoadingBackground = "Assets/_Project/Art/UI/loading_bg.png";
        private const string Logo = "Assets/_Project/Art/UI/logo.png";
        private const string AppIcon = "Assets/_Project/Art/AppIcon/app_icon.png";

        [MenuItem("TrashCats/Art/Apply Loading Screen And App Icon")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("loadingBackground").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingBackground);
            so.FindProperty("logo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Logo);
            so.FindProperty("uiSkin").objectReferenceValue = AssetDatabase.FindAssets("t:UiSkin").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UiSkin>).FirstOrDefault();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // the icon is read at full size, uncompressed
            if (AssetImporter.GetAtPath(AppIcon) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LoadingAndIcon] background={so.FindProperty("loadingBackground").objectReferenceValue} skin={so.FindProperty("uiSkin").objectReferenceValue} icon={icon}");
        }
    }
}
