using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Bottom sheet for one cat: six activities with duration/result previews or the reason they are unavailable,
    /// plus Feed and Stop (Docs/GDD/UI_UX.md §4.1).
    /// </summary>
    public sealed class ActivitySheet
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly GameObject _root;
        private readonly TMP_Text _title;
        private readonly TMP_Text _needs;
        private readonly List<(ActivityDefinition Activity, UiButton Button)> _buttons = new List<(ActivityDefinition, UiButton)>();
        private readonly UiButton _feed;
        private readonly UiButton _stop;
        private CatId? _catId;

        public bool IsOpen => _catId != null;

        public ActivitySheet(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult, Action<CatId> onDetails)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;

            var dim = Ui.Panel(parent, "ActivitySheet", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            _root = dim.gameObject;

            var panel = Ui.Card(dim.transform, "Sheet", CardStyle.Paper, raycast: true);
            panel.rectTransform.Place(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0, 980));

            _title = Ui.Label(panel.transform, "", 44, TextAnchor.MiddleLeft, Palette.Ink);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -105), new Vector2(-40, -45));
            _needs = Ui.Label(panel.transform, "", 30, TextAnchor.MiddleLeft, Palette.Ink);
            _needs.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -150), new Vector2(-40, -105));
            var details = Ui.Button(panel.transform, "Sheet_Details", "Умения и статы", Palette.Paper, () =>
            {
                if (_catId is CatId id) onDetails(id);
            }, 28);
            details.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-400, -100), new Vector2(-100, -30));

            var activities = OrderedActivities(session.Config);
            for (var i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                var col = i % 2;
                var row = i / 2;
                var button = Ui.Button(panel.transform, "Activity_" + activity.Id, "", Palette.Secondary, () => Assign(activity.Id), 30);
                button.Rect.Place(new Vector2(col * 0.5f, 1f), new Vector2((col + 1) * 0.5f, 1f),
                    new Vector2(30, -160 - (row + 1) * 180), new Vector2(-30, -160 - row * 180 - 20));
                _buttons.Add((activity, button));
            }

            _feed = Ui.Button(panel.transform, "Feed", "", Palette.Primary, Feed, 32);
            _feed.Rect.Place(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(30, 40), new Vector2(-15, 150));
            _stop = Ui.Button(panel.transform, "Stop", "Стоп", Palette.Disabled, Stop, 32);
            _stop.Rect.Place(new Vector2(0.5f, 0), new Vector2(1f, 0), new Vector2(15, 40), new Vector2(-30, 150));

            _root.SetActive(false);
        }

        public void Open(CatId catId)
        {
            _catId = catId;
            Refresh();
            _overlays.Push(_root, () => _catId = null);
        }

        public void Close() => _overlays.Close(_root);

        public void Refresh()
        {
            if (_catId == null || !_session.World.TryGetCat(_catId.Value, out var cat))
                return;

            var id = _catId.Value;
            _title.text = Texts.CatName(id);
            _needs.text = $"Энергия {cat.Energy} · Сытость {cat.Satiety} · Еда {_session.World.Food}";

            foreach (var (activity, button) in _buttons)
            {
                var error = _session.CanAssign(id, activity.Id);
                var current = cat.Activity.IsWorking && cat.Activity.ActivityId == activity.Id;
                button.Interactable = error == CommandError.None;
                button.SetColor(current ? Palette.Primary : Palette.Secondary);

                string details;
                if (error != CommandError.None)
                {
                    details = Texts.Error(error);
                }
                else
                {
                    var duration = _session.PreviewDuration(id, activity.Id);
                    var output = _session.PreviewOutput(id, activity.Id);
                    var slow = duration > activity.DurationMs ? " · ×2 время" : "";
                    details = $"{Texts.Seconds(duration)} · {Texts.Output(activity, output)}{slow}";
                }

                button.Text.text = $"{Texts.ActivityName(activity)}\n<size=26>{details}</size>";
            }

            var canFeed = _session.CanFeed(id);
            _feed.Interactable = canFeed == CommandError.None;
            _feed.Text.text = canFeed == CommandError.None ? $"Покормить (−{_session.FoodToFeed(id)} еды)" : Texts.Error(canFeed);
            _stop.Interactable = cat.Activity.IsWorking;
        }

        private void Assign(ActivityId activityId)
        {
            if (_catId == null) return;
            var result = _session.AssignActivity(_catId.Value, activityId);
            _onResult(result);
            if (result.IsOk) Close();
        }

        private void Feed()
        {
            if (_catId == null) return;
            _onResult(_session.FeedCat(_catId.Value));
            Refresh();
        }

        private void Stop()
        {
            if (_catId == null) return;
            _onResult(_session.StopActivity(_catId.Value));
            Refresh();
        }

        private static List<ActivityDefinition> OrderedActivities(GameConfig config)
        {
            int Order(ActivityDefinition a) => a.Kind switch
            {
                ActivityKind.Scavenge => 0,
                ActivityKind.Rummage => 1,
                ActivityKind.Train => 2 + (int)a.TrainedAttribute,
                _ => 9,
            };

            return config.Activities.OrderBy(Order).ToList();
        }
    }
}
