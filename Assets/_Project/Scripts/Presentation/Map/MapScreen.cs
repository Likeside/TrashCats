using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Map
{
    /// <summary>Vertical path of territories from the dumpster (bottom) to the rooftop (top), Docs/GDD/UI_UX.md §6.</summary>
    public sealed class MapScreen
    {
        private readonly GameSession _session;
        private readonly Action<TerritoryId> _onOpen;
        private readonly List<(TerritoryDefinition Territory, UiButton Button, TMP_Text Status)> _nodes = new List<(TerritoryDefinition, UiButton, TMP_Text)>();

        public GameObject Root { get; }

        public MapScreen(GameSession session, RectTransform parent, Action<TerritoryId> onOpen, Sprite background = null)
        {
            _session = session;
            _onOpen = onOpen;
            var root = Ui.Rect(parent, "MapScreen").Fill();
            Root = root.gameObject;

            Ui.Panel(root, "Back", Palette.Background).rectTransform.Fill();
            if (background != null)
                Ui.Cover(root, "MapArt", background, new Color(0.75f, 0.75f, 0.8f));
            var title = Ui.Label(root, "Район", 52, TextAnchor.MiddleCenter, Palette.Paper);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero);
            title.Outlined();

            var area = Ui.Rect(root, "Path").Place(Vector2.zero, Vector2.one, new Vector2(60, 150), new Vector2(-60, -120));
            var territories = session.Config.Territories;
            var steps = territories.Count + 1;

            var home = Ui.Card(area, "Node_Home", CardStyle.Tin);
            home.rectTransform.Place(new Vector2(0.15f, 0), new Vector2(0.85f, 1f / steps), new Vector2(0, 10), new Vector2(0, -10));
            Ui.Label(home.transform, "Мусорка ресторана\nнаше убежище", 34, TextAnchor.MiddleCenter, Palette.Paper).rectTransform.Fill(10);

            for (var i = 0; i < territories.Count; i++)
            {
                var territory = territories[i];
                var y0 = (i + 1f) / steps;
                var y1 = (i + 2f) / steps;
                var line = Ui.Panel(area, "Line", Palette.Disabled);
                line.rectTransform.Place(new Vector2(0.5f, y0 - 0.02f), new Vector2(0.5f, y0 + 0.02f), new Vector2(-4, 0), new Vector2(4, 0));

                var id = territory.Id;
                var button = Ui.Button(area, "Node_" + territory.Id, "", Palette.Secondary, () => _onOpen(id), 36);
                button.Rect.Place(new Vector2(0.08f, y0), new Vector2(0.92f, y1), new Vector2(0, 14), new Vector2(0, -14));
                button.Text.rectTransform.offsetMax = new Vector2(-60, -12);
                var status = Ui.Label(button.Rect, "", 28, TextAnchor.LowerCenter, Palette.Ink);
                status.rectTransform.Fill(14);
                status.gameObject.SetActive(false);
                _nodes.Add((territory, button, status));
            }

            Refresh();
        }

        public void Refresh()
        {
            var world = _session.World;
            foreach (var (territory, button, status) in _nodes)
            {
                var state = world.Territories.Get(territory.Id);
                var enemies = string.Join(", ", territory.EnemyQueue.Select(e => Texts.EnemyName(e.Value)));
                var name = Texts.TerritoryName(territory.Id);
                switch (state.Status)
                {
                    case TerritoryStatus.Locked:
                        button.SetColor(new Color(Palette.Disabled.r, Palette.Disabled.g, Palette.Disabled.b, 0.92f));
                        button.Interactable = false;
                        status.text = "Закрыто";
                        break;
                    case TerritoryStatus.Available:
                        button.SetColor(Palette.Primary);
                        button.Interactable = true;
                        status.text = "Можно напасть! Награда: " + Reward(territory, true);
                        break;
                    default:
                        button.SetColor(new Color(0.5f, 0.69f, 0.41f, 0.85f));
                        button.Interactable = true;
                        status.text = $"Захвачено · {Texts.Bonus(territory)} · повтор: {Reward(territory, false)}";
                        break;
                }

                button.Text.text = $"{name}\n<size=24>{enemies}</size>\n<size=24>{status.text}</size>";
            }
        }

        public bool HasNewTarget => _session.Config.Territories.Any(t => _session.World.Territories.Get(t.Id).Status == TerritoryStatus.Available);

        private static string Reward(TerritoryDefinition territory, bool first)
        {
            var (food, junk) = TerritoryProgress.Reward(territory, first);
            var recruit = first && territory.RecruitCat is CatId cat ? $", +{Texts.CatName(cat)}" : "";
            return $"{food} еды, {junk} хлама{recruit}";
        }
    }
}
