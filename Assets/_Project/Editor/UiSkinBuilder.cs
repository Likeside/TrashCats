using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TrashCats.Editor
{
    /// <summary>
    /// Builds Assets/_Project/Configs/UiSkin.asset from Art/UI (sprites cut by Tools/import_ui.py, 9-slice borders from
    /// slices.json) and Art/Fonts (SDF font assets with Cyrillic, outline materials) and assigns it to the Game scene.
    /// </summary>
    public static class UiSkinBuilder
    {
        private const string UiRoot = "Assets/_Project/Art/UI";
        private const string FontsRoot = "Assets/_Project/Art/Fonts";
        private const string SkinPath = "Assets/_Project/Configs/UiSkin.asset";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // everything the UI prints; anything else is added to the dynamic atlas at runtime
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
            + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя×−·→—–«»…№°";

        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.1f);

        [MenuItem("TrashCats/Art/Rebuild UI Skin")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ApplySlices();

            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }

            skin.headingFont = FontAsset("RussoOne-Regular");
            skin.bodyFont = FontAsset("BalsamiqSans-Regular");
            if (skin.headingFont != null && skin.bodyFont != null)
            {
                // Russo One has no "−" and "→": fall back to the body font
                skin.headingFont.fallbackFontAssetTable = new List<TMP_FontAsset> { skin.bodyFont };
                EditorUtility.SetDirty(skin.headingFont);
            }

            skin.headingOutline = OutlineMaterial(skin.headingFont, "RussoOne Outline", 0.28f);
            skin.bodyOutline = OutlineMaterial(skin.bodyFont, "BalsamiqSans Outline", 0.22f);

            skin.button = Sprite("button_a_white") ?? Sprite("button_white");
            skin.buttonPrimary = Sprite("button_a_yellow") ?? Sprite("button_yellow");
            skin.buttonSecondary = Sprite("button_a_blue") ?? Sprite("button_blue");
            skin.paper = Sprite("panel_a") ?? Sprite("panel_paper");
            skin.tin = Sprite("hud_a") ?? Sprite("panel_tin");
            skin.barFrame = Sprite("hud_a") ?? Sprite("bar_frame");
            // the concept's bars are plain tracks with an icon beside them (Ui.Bar), not badge bars
            skin.badgeBars = new List<NamedSprite>();
            skin.ribbon = Sprite("ribbon");
            skin.burst = Sprite("burst");
            skin.bubble = Sprite("bubble");
            skin.tape = Sprite("tape");
            skin.pointer = Sprite("pointer");
            skin.circle = Sprite("circle_mask");
            skin.ring = Sprite("map_ring");
            skin.ringGold = Sprite("map_ring_gold");
            skin.icons = Directory.GetFiles(UiRoot, "icon_*.png").OrderBy(f => f)
                .Select(f => new NamedSprite { key = Path.GetFileNameWithoutExtension(f).Substring("icon_".Length), sprite = Sprite(Path.GetFileNameWithoutExtension(f)) })
                .Where(n => n.sprite != null).ToList();

            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            AssignToGameScene();
            Debug.Log($"[UiSkin] fonts {skin.headingFont != null}/{skin.bodyFont != null}, icons {skin.icons.Count}, bars {skin.badgeBars.Count}, button {skin.button != null}");
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/{name}.png");

        private static void ApplySlices()
        {
            var json = $"{UiRoot}/slices.json";
            if (!File.Exists(json))
                return;
            foreach (Match m in Regex.Matches(File.ReadAllText(json), @"""(\w+)"":\s*\[([^\]]*)\]"))
            {
                var path = $"{UiRoot}/{m.Groups[1].Value}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    continue;
                var v = m.Groups[2].Value.Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
                var border = new Vector4(v[0], v[1], v[2], v[3]);
                if (importer.spriteBorder == border)
                    continue;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }

        private static TMP_FontAsset FontAsset(string fileName)
        {
            var assetPath = $"{FontsRoot}/{fileName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsRoot}/{fileName}.ttf");
            if (font == null)
                return null;

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = fileName + " SDF";
            asset.TryAddCharacters(Characters, out var missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.Log($"[UiSkin] {fileName} has no glyphs for: {missing}");
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = fileName + " Atlas";
            asset.material.name = fileName + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static Material OutlineMaterial(TMP_FontAsset font, string name, float width)
        {
            if (font == null)
                return null;
            var path = $"{FontsRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.6f);
            // comic drop shadow
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignToGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<GameSceneRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("uiSkin").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
