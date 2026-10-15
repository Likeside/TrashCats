using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// Builds Assets/_Project/Configs/CharacterArt.asset from folder conventions and assigns it to the Game scene:
    /// Art/Characters/&lt;viewKey&gt;/{Idle,Punch|Attack,Heavy,Hit,KO,Tail,Hideout}/*.png (+ attach_points.json),
    /// Art/Environment/&lt;key&gt;.png (bg_hideout, bg_map, arena_&lt;territory&gt;, prop_&lt;upgrade&gt;, icon_&lt;name&gt;).
    /// </summary>
    public static class CharacterArtBuilder
    {
        private const string CharactersRoot = "Assets/_Project/Art/Characters";
        private const string EnvironmentRoot = "Assets/_Project/Art/Environment";
        private const string RegistryPath = "Assets/_Project/Configs/CharacterArt.asset";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // battle scale per character; variants reuse another character's frames with a tint
        private static readonly Dictionary<string, float> Scales = new Dictionary<string, float>
        {
            { "ryzhy", 1.25f }, { "shnyr", 1.25f }, { "ponchik", 1.3f }, { "rat", 1.0f }, { "gull", 1.05f }, { "shopcat", 1.25f },
            { "crow", 1.05f }, { "raccoon", 1.25f }, { "pug", 1.15f }, { "admiral", 1.55f },
        };

        private static readonly (string Key, string Source, Color Tint, float Scale)[] Variants =
        {
            ("crow_boss", "crow", new Color(0.8f, 0.8f, 1f), 1.2f),
            ("bigrat", "rat", new Color(1f, 0.78f, 0.72f), 1.3f),
        };

        [MenuItem("TrashCats/Art/Rebuild Character Art Registry")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var registry = AssetDatabase.LoadAssetAtPath<CharacterArtRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<CharacterArtRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            registry.characters = new List<CharacterArt>();
            if (Directory.Exists(CharactersRoot))
            {
                foreach (var dir in Directory.GetDirectories(CharactersRoot).OrderBy(d => d))
                {
                    var key = Path.GetFileName(dir).ToLowerInvariant();
                    var art = BuildCharacter(dir.Replace('\\', '/'), key);
                    if (art != null) registry.characters.Add(art);
                }
            }

            foreach (var (key, source, tint, scale) in Variants)
            {
                var baseArt = registry.characters.FirstOrDefault(c => c.viewKey == source);
                if (baseArt == null) continue;
                var copy = JsonUtility.FromJson<CharacterArt>(JsonUtility.ToJson(baseArt));
                // JsonUtility does not keep object references; copy them explicitly
                copy.idle = baseArt.idle; copy.attack = baseArt.attack; copy.heavyAttack = baseArt.heavyAttack;
                copy.hit = baseArt.hit; copy.knockOut = baseArt.knockOut; copy.tail = baseArt.tail; copy.portrait = baseArt.portrait;
                copy.hideoutPoses = baseArt.hideoutPoses;
                copy.run = baseArt.run; copy.canPunch = baseArt.canPunch; copy.hideoutIdle = baseArt.hideoutIdle; copy.rope = baseArt.rope; copy.dig = baseArt.dig; copy.bench = baseArt.bench;
                copy.viewKey = key;
                copy.tint = tint;
                copy.battleScale = scale;
                registry.characters.Add(copy);
            }

            registry.sprites = new List<NamedSprite>();
            if (Directory.Exists(EnvironmentRoot))
            {
                foreach (var file in Directory.GetFiles(EnvironmentRoot, "*.png").OrderBy(f => f))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
                    if (sprite != null)
                        registry.sprites.Add(new NamedSprite { key = Path.GetFileNameWithoutExtension(file), sprite = sprite });
                }
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            WriteManifest(registry);
            AssignToGameScene(registry);
            Debug.Log($"[ArtBuilder] {registry.characters.Count} characters, {registry.sprites.Count} sprites: "
                      + string.Join(", ", registry.characters.Select(c => $"{c.viewKey}({Count(c)})")));
        }

        /// <summary>
        /// TestResults/clips.json: how every clip actually plays (frame files, fps, loop, ping-pong, drawn key frames),
        /// read by Tools/anim_qa.py to check the animations the way the game shows them.
        /// </summary>
        private static void WriteManifest(CharacterArtRegistry registry)
        {
            var sb = new System.Text.StringBuilder("[\n");
            var first = true;
            foreach (var art in registry.characters)
            {
                if (art.tint != Color.white) continue; // variants reuse another character's frames
                foreach (var (role, clip) in new[] { ("idle", art.idle), ("attack", art.attack), ("heavy", art.heavyAttack), ("hit", art.hit), ("ko", art.knockOut),
                             ("tail", art.tail), ("hideoutIdle", art.hideoutIdle), ("run", art.run), ("canPunch", art.canPunch), ("rope", art.rope), ("dig", art.dig), ("bench", art.bench) })
                {
                    if (clip?.frames == null || clip.frames.Length == 0) continue;
                    if (role == "heavy" && clip == art.attack) continue;
                    var files = string.Join(",", clip.frames.Select(f => $"\"{AssetDatabase.GetAssetPath(f)}\""));
                    sb.Append(first ? "" : ",\n").Append($"{{\"character\":\"{art.viewKey}\",\"role\":\"{role}\",\"fps\":{clip.fps.ToString(CultureInfo.InvariantCulture)},\"loop\":{(clip.loop ? "true" : "false")},\"pingPong\":{(clip.pingPong ? "true" : "false")},\"keys\":[{string.Join(",", clip.keys ?? new int[0])}],\"frames\":[{files}]}}");
                    first = false;
                }
            }

            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/clips.json", sb.Append("\n]\n").ToString());
        }

        private static string Count(CharacterArt c) =>
            $"i{c.idle.frames.Length} a{c.attack.frames.Length} h{c.heavyAttack.frames.Length} x{c.hit.frames.Length} k{c.knockOut.frames.Length} t{c.tail.frames.Length} p{c.hideoutPoses.Length}";

        private static CharacterArt BuildCharacter(string dir, string key)
        {
            var art = new CharacterArt
            {
                viewKey = key,
                battleScale = Scales.TryGetValue(key, out var scale) ? scale : 1.2f,
                idle = Clip(dir, "Idle", 6f, true),
                attack = Clip(dir, "Attack", 12f, false) ?? Clip(dir, "Punch", 12f, false),
                heavyAttack = Clip(dir, "Heavy", 12f, false),
                hit = Clip(dir, "Hit", 12f, false),
                knockOut = Clip(dir, "KO", 8f, false),
                tail = Clip(dir, "Tail", 5f, true),
                run = Clip(dir, "Run", 9f, true),
                hideoutIdle = Clip(dir, "HideIdle", 6f, true),
                rope = Clip(dir, "Rope", 9f, true),
                dig = Clip(dir, "Dig", 8f, true),
                canPunch = Clip(dir, "CanPunch", 10f, true),
                bench = Clip(dir, "Bench", 1f, true),
                hideoutPoses = Sprites(dir + "/Hideout"),
            };
            art.attack ??= new FlipbookClip();
            art.heavyAttack ??= art.attack;
            art.hit ??= new FlipbookClip();
            art.knockOut ??= new FlipbookClip();
            art.tail ??= new FlipbookClip();
            art.run ??= new FlipbookClip();
            art.hideoutIdle ??= new FlipbookClip();
            art.rope ??= new FlipbookClip();
            art.dig ??= new FlipbookClip();
            art.canPunch ??= new FlipbookClip();
            art.bench ??= new FlipbookClip();
            if (art.idle == null)
                return null;
            // idles sway there and back: ping-pong has no loop seam (no "saw")
            art.idle.pingPong = key != "wheel" && art.idle.frames.Length > 2;
            art.hideoutIdle.pingPong = art.hideoutIdle.frames.Length > 2;
            art.dig.pingPong = art.dig.frames.Length > 2; // the kicking loop is not drawn to close
            // the frames hold a whole rep (press up, faster lowering) and play one rep per second, as in the previews
            art.bench.fps = art.bench.frames.Length;
            art.idle.breathe = key == "wheel" ? 0f : art.idle.frames.Length <= 2 ? 0.012f : 0.008f;
            if (key == "wheel") art.idle.fps = art.idle.frames.Length >= 15 ? 24f : 12f;
            // card portrait cut from the concept (Tools/import_ui.py), else the first stance frame
            art.portrait = Sprites(dir + "/Portrait").FirstOrDefault() ?? art.idle.frames.FirstOrDefault();
            // hideout card picture with its own backdrop (Tools/cut_card_portraits.py)
            art.card = Sprites(dir + "/Card").FirstOrDefault();

            var attach = dir + "/attach_points.json";
            if (File.Exists(attach))
            {
                var json = File.ReadAllText(attach);
                art.idle.attachPoints = Points(json, "idle");
                if (art.attack.frames.Length > 0) art.attack.attachPoints = Points(json, "punch");
            }

            return art;
        }

        private static FlipbookClip Clip(string dir, string folder, float fps, bool loop)
        {
            var frames = Sprites($"{dir}/{folder}");
            // doubled clips play faster to keep the tempo: by keys.json (adaptive in-betweens) or 8 -> 15/16 frames
            var keys = new int[0];
            var keysPath = $"{dir}/{folder}/keys.json";
            if (File.Exists(keysPath))
                keys = Regex.Matches(File.ReadAllText(keysPath), @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            if (keys.Length > 0) fps *= (float)frames.Length / keys.Length;
            else if (frames.Length >= 15) fps *= 2f;
            return frames.Length == 0 ? null : new FlipbookClip { name = folder.ToLowerInvariant(), frames = frames, fps = fps, loop = loop, keys = keys };
        }

        private static Sprite[] Sprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder)
                .OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        private static Vector2[] Points(string json, string key)
        {
            var start = json.IndexOf('"' + key + '"');
            if (start < 0) return new Vector2[0];
            var open = json.IndexOf('[', start);
            var depth = 0;
            var end = open;
            for (; end < json.Length; end++)
            {
                if (json[end] == '[') depth++;
                else if (json[end] == ']' && --depth == 0) break;
            }

            var numbers = Regex.Matches(json.Substring(open, end - open), @"-?\d+(\.\d+)?").Cast<Match>()
                .Select(m => float.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
            return Enumerable.Range(0, numbers.Length / 2).Select(i => new Vector2(numbers[i * 2], numbers[i * 2 + 1])).ToArray();
        }

        private static void AssignToGameScene(CharacterArtRegistry registry)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<GameSceneRoot>();
            var so = new SerializedObject(root);
            var property = so.FindProperty("characterArt");
            var asset = AssetDatabase.LoadAssetAtPath<CharacterArtRegistry>(RegistryPath);
            property.objectReferenceValue = asset;
            var applied = so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ArtBuilder] scene root={(root != null)} asset={(asset != null)} persistent={EditorUtility.IsPersistent(asset)} applied={applied} saved={saved} value={property.objectReferenceValue}");
        }
    }
}
