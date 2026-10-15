using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Map
{
    /// <summary>
    /// The neighbourhood as in the map concept (Docs/ArtDirection/Concepts/round2/map_S4.png): round vignettes of each
    /// territory's arena on a zig-zag path from the dumpster (bottom) to the rooftop (top), each with a plate: name,
    /// enemies, reward or status. Locked places are dark with a padlock, the next target has a pulsing golden ring,
    /// captured ones carry a paw badge. Docs/GDD/UI_UX.md §6.
    /// </summary>
    public sealed class MapScreen
    {
        private const float Vignette = 260f;

        /// <summary>Bottom navigation height (the path stays above it).</summary>
        private const float NavHeight = Hideout.HideoutScreen.NavHeight;

        private readonly GameSession _session;
        private readonly Action<TerritoryId> _onOpen;
        private readonly List<Node> _nodes = new List<Node>();

        public GameObject Root { get; }

        public MapScreen(GameSession session, RectTransform parent, Action<TerritoryId> onOpen, Sprite background = null, Func<string, Sprite> sprites = null)
        {
            _session = session;
            _onOpen = onOpen;
            var root = Ui.Rect(parent, "MapScreen").Fill();
            Root = root.gameObject;

            Ui.Panel(root, "Back", Palette.Background).rectTransform.Fill();
            if (background != null)
                Ui.Cover(root, "MapArt", background, new Color(0.7f, 0.7f, 0.78f));
            var title = Ui.Label(root, Loc.T("map_screen.01"), 56, TextAnchor.MiddleCenter, Palette.Paper);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -110), new Vector2(0, -10));
            title.Outlined();

            var area = Ui.Rect(root, "Path").Place(Vector2.zero, Vector2.one, new Vector2(40, NavHeight + 40), new Vector2(-40, -130));
            var territories = session.Config.Territories;
            var steps = territories.Count + 1;
            var points = Enumerable.Range(0, steps).Select(i => new Vector2(i % 2 == 0 ? 0.3f : 0.7f, Mathf.Lerp(0.07f, 0.93f, i / (steps - 1f)))).ToList();

            // dotted path under the vignettes
            for (var i = 0; i + 1 < steps; i++)
                for (var d = 1; d < 8; d++)
                {
                    var dot = Ui.Panel(area, "Dot", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.75f));
                    dot.sprite = Ui.Skin?.circle;
                    dot.rectTransform.At(Vector2.Lerp(points[i], points[i + 1], d / 8f), new Vector2(14, 14));
                }

            // home: the dumpster yard
            Plate(area, points[0], Loc.T("map_screen.02"), Loc.T("map_screen.03"), "");
            var home = new RoundPicture(area, "Node_Home", Palette.PanelDark);
            home.Root.At(points[0], new Vector2(Vignette, Vignette) * 0.9f);
            home.SetPicture(sprites?.Invoke("bg_hideout"));

            for (var i = 0; i < territories.Count; i++)
            {
                var territory = territories[i];
                var id = territory.Id;
                var point = points[i + 1];
                var (name, enemies, status) = Plate(area, point, "", "", "");

                var picture = new RoundPicture(area, "Node_" + territory.Id, Palette.PanelDark);
                picture.Root.At(point, new Vector2(Vignette, Vignette));
                picture.SetPicture(sprites?.Invoke("arena_" + territory.ViewKey));
                var hit = picture.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var button = picture.Root.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => _onOpen(id));

                var lockIcon = Ui.Icon(picture.Root, "lock");
                lockIcon?.rectTransform.Place(new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
                var paw = Ui.Icon(picture.Root, "paw");
                paw?.rectTransform.Place(new Vector2(0.68f, 0.68f), new Vector2(1.02f, 1.02f));

                _nodes.Add(new Node(territory, picture, button, lockIcon, paw, name, enemies, status));
            }

            Refresh();
        }

        /// <summary>Dark plate beside a vignette (towards the middle of the screen): name, enemies, status.</summary>
        private static (TMP_Text Name, TMP_Text Enemies, TMP_Text Status) Plate(RectTransform area, Vector2 point, string name, string enemies, string status)
        {
            var right = point.x < 0.5f;
            var plate = Ui.Card(area, "Plate", CardStyle.Tin);
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = point;
            rt.pivot = new Vector2(right ? 0f : 1f, 0.5f);
            rt.sizeDelta = new Vector2(430, 186);
            rt.anchoredPosition = new Vector2(right ? Vignette * 0.42f : -Vignette * 0.42f, 0);

            var title = Ui.Label(plate.transform, name, 34, TextAnchor.UpperCenter, Palette.Paper, heading: true);
            // text keeps clear of the frame; the circle overlaps the plate on one side
            var near = right ? 72 : 34;
            var far = right ? -34 : -72;
            title.rectTransform.Place(new Vector2(0, 0.62f), Vector2.one, new Vector2(near, 0), new Vector2(far, -22));
            var who = Ui.Label(plate.transform, enemies, 24, TextAnchor.MiddleCenter, Palette.Disabled);
            who.rectTransform.Place(new Vector2(0, 0.4f), new Vector2(1, 0.64f), new Vector2(near, 0), new Vector2(far, 0));
            var what = Ui.Label(plate.transform, status, 24, TextAnchor.MiddleCenter, Palette.Primary);
            what.rectTransform.Place(Vector2.zero, new Vector2(1, 0.42f), new Vector2(near, 22), new Vector2(far, 0));
            foreach (var label in new[] { title, who, what })
            {
                label.enableAutoSizing = true;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = 18;
            }
            return (title, who, what);
        }

        public void Refresh()
        {
            var world = _session.World;
            foreach (var node in _nodes)
            {
                var territory = node.Territory;
                var state = world.Territories.Get(territory.Id).Status;
                node.Name.text = Texts.TerritoryName(territory.Id);
                node.Enemies.text = string.Join(", ", territory.EnemyQueue.Select(e => Texts.EnemyName(e.Value)));

                var locked = state == TerritoryStatus.Locked;
                var available = state == TerritoryStatus.Available;
                node.Button.interactable = !locked;
                node.Picture.Picture.color = locked ? new Color(0.5f, 0.5f, 0.56f) : Color.white;
                node.Picture.SetGold(available);
                node.Lock?.gameObject.SetActive(locked);
                node.Paw?.gameObject.SetActive(state == TerritoryStatus.Captured);
                node.Status.color = available ? Palette.Primary : locked ? Palette.Disabled : Palette.Hex("#9BC46A");
                node.Status.text = locked ? Loc.T("map_screen.04")
                    : available ? Loc.T("map_screen.05") + Reward(territory, true)
                    : Loc.T("map_screen.06", Texts.Bonus(territory));

                node.Picture.Root.DOKill();
                node.Picture.Root.localScale = Vector3.one;
                if (available)
                    node.Picture.Root.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(node.Picture.Root.gameObject);
            }
        }

        public bool HasNewTarget => _session.Config.Territories.Any(t => _session.World.Territories.Get(t.Id).Status == TerritoryStatus.Available);

        private static string Reward(TerritoryDefinition territory, bool first)
        {
            var (food, junk) = TerritoryProgress.Reward(territory, first);
            var recruit = first && territory.RecruitCat is CatId cat ? $", +{Texts.CatName(cat)}" : "";
            return Loc.T("map_screen.07", food, junk, recruit);
        }

        private sealed class Node
        {
            public TerritoryDefinition Territory { get; }
            public RoundPicture Picture { get; }
            public Button Button { get; }
            public Image Lock { get; }
            public Image Paw { get; }
            public TMP_Text Name { get; }
            public TMP_Text Enemies { get; }
            public TMP_Text Status { get; }

            public Node(TerritoryDefinition territory, RoundPicture picture, Button button, Image lockIcon, Image paw, TMP_Text name, TMP_Text enemies, TMP_Text status)
            {
                Territory = territory;
                Picture = picture;
                Button = button;
                Lock = lockIcon;
                Paw = paw;
                Name = name;
                Enemies = enemies;
                Status = status;
            }
        }
    }
}
