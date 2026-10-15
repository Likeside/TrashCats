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

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Bottom sheet for one cat (Docs/GDD/UI_UX.md §4.1): a header with the cat's avatar, name, status and needs; six
    /// activities with what they cost and bring as icons (or what is missing), the current one with its cycle bar and the
    /// sensible next step gently pulsing; Feed with the food it takes and the satiety it gives, and Stop while the cat works.
    /// </summary>
    public sealed class ActivitySheet
    {
        private const float ChipHeight = 34f;
        private const int ChipFont = 26;

        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly Characters.CharacterViewFactory _art;
        private readonly GameObject _root;
        private readonly Image _avatar;
        private readonly TMP_Text _title;
        private readonly TMP_Text _status;
        private readonly CycleBar _statusCycle;
        private readonly UiBar _energy;
        private readonly UiBar _satiety;
        private readonly List<ActivityButton> _buttons = new List<ActivityButton>();
        private readonly UiButton _feed;
        private readonly RectTransform _feedRow;
        private readonly TMP_Text _feedSpend;
        private readonly TMP_Text _feedGain;
        private readonly UiButton _stop;
        private CatId? _catId;
        private bool _fitFeed;
        private bool _feedAdvised;

        public bool IsOpen => _catId != null;

        public ActivitySheet(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult, Action<CatId> onDetails,
            Characters.CharacterViewFactory art = null)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;
            _art = art;

            var dim = Ui.Panel(parent, "ActivitySheet", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            _root = dim.gameObject;

            var panel = Ui.Card(dim.transform, "Sheet", CardStyle.Paper, raycast: true);
            panel.rectTransform.Place(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0, 980));

            void OpenDetails()
            {
                if (_catId is CatId id) onDetails(id);
            }

            // header: avatar (tap = skills and stats), name, what the cat is doing, energy and satiety
            _avatar = Ui.Panel(panel.transform, "Avatar", Color.white, raycast: true);
            _avatar.preserveAspect = true;
            _avatar.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -226), new Vector2(224, -46));
            _avatar.gameObject.AddComponent<Button>().onClick.AddListener(OpenDetails);

            _title = Ui.Label(panel.transform, "", 46, TextAnchor.MiddleLeft, Palette.Ink);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(248, -98), new Vector2(-290, -44));
            _status = Ui.Label(panel.transform, "", 28, TextAnchor.MiddleLeft, Palette.Ink, heading: false);
            _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(248, -134), new Vector2(-290, -98));
            _status.enableAutoSizing = true;
            _status.fontSizeMin = 20;
            _status.fontSizeMax = 28;
            _statusCycle = new CycleBar(panel.transform, "StatusCycle");
            _statusCycle.Root.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(250, -146), new Vector2(-292, -136));

            _energy = Ui.Bar(panel.transform, "Energy", Palette.Energy, "energy");
            _energy.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(298, -190), new Vector2(-292, -154));
            _satiety = Ui.Bar(panel.transform, "Satiety", Palette.Satiety, "satiety");
            _satiety.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(298, -232), new Vector2(-292, -196));

            // skills and stats: a compact light button with its icon, it no longer outshouts the activities
            var details = Ui.Button(panel.transform, "Sheet_Details", Loc.T("activity_sheet.01"), Palette.Paper, OpenDetails, 28);
            details.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-262, -232), new Vector2(-40, -44));
            details.Text.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(30, 18), new Vector2(-30, 0));
            details.Text.fontSizeMax = 26;
            details.Text.fontSizeMin = 18;
            if (Ui.Icon(details.Rect, "skill") is Image skillIcon)
                skillIcon.rectTransform.Place(new Vector2(0.5f, 0.42f), new Vector2(0.5f, 1f), new Vector2(-56, 0), new Vector2(56, -16));

            var activities = OrderedActivities(session.Config);
            for (var i = 0; i < activities.Count; i++)
            {
                var col = i % 2;
                var row = i / 2;
                var activity = activities[i];
                var button = new ActivityButton(panel.transform, activity, () => Assign(activity.Id));
                button.Button.Rect.Place(new Vector2(col * 0.5f, 1f), new Vector2((col + 1) * 0.5f, 1f),
                    new Vector2(col == 0 ? 30 : 15, -250 - row * 186 - 166), new Vector2(col == 0 ? -15 : -30, -250 - row * 186));
                _buttons.Add(button);
            }

            // Feed: "Feed  [food] −4  →  [satiety] +40"; the gang's food is spent, so it lives here and not in the header
            _feed = Ui.Button(panel.transform, "Sheet_Feed", "", Palette.Primary, Feed, 32);
            _feedRow = Info.Row(_feed.Rect, "FeedRow", 10f, TextAnchor.MiddleCenter);
            _feedRow.Fill();
            Info.Text(_feedRow, Loc.T("activity_sheet.03"), 32, Palette.Ink);
            _feedSpend = Info.Chip(_feedRow, "food", "", 46f, 30, Palette.Ink).GetComponentInChildren<TMP_Text>();
            Info.Text(_feedRow, "→", 30, Palette.Ink);
            _feedGain = Info.Chip(_feedRow, "satiety", "", 46f, 30, Palette.Ink).GetComponentInChildren<TMP_Text>();

            _stop = Ui.Button(panel.transform, "Stop", Loc.T("activity_sheet.02"), Palette.Disabled, Stop, 32);
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
            var config = _session.Config;
            var working = cat.Activity.IsWorking;
            var viewKey = config.GetCat(id).ViewKey;
            var avatar = viewKey != null ? _art?.Avatar(viewKey) ?? _art?.Portrait(viewKey) : null;
            _avatar.sprite = avatar;
            _avatar.enabled = avatar != null;

            _title.text = Texts.CatName(id);
            _status.text = working ? Texts.Doing(config.GetActivity(cat.Activity.ActivityId)) : Texts.IdleStatus(Texts.CurrentIdle(cat, config));
            _statusCycle.Root.gameObject.SetActive(working);
            var max = (float)config.Needs.Max;
            _energy.Set(cat.Energy / max, cat.Energy.ToString());
            _satiety.Set(cat.Satiety / max, cat.Satiety.ToString());

            var tired = CatRules.IsTired(cat, config);
            foreach (var button in _buttons)
            {
                var activity = button.Activity;
                var error = _session.CanAssign(id, activity.Id);
                var current = working && cat.Activity.ActivityId == activity.Id;
                if (error == CommandError.None)
                    button.ShowAvailable(_session.PreviewDuration(id, activity.Id), _session.PreviewOutput(id, activity.Id));
                else
                    button.ShowUnavailable(error == CommandError.Tired ? Loc.T("activity_sheet.04", config.Needs.TiredThreshold) : Texts.Error(error));
                button.SetCurrent(current);
                // a tired cat should sleep: say so
                button.SetRecommended(activity.Kind == ActivityKind.Sleep && tired && !current && error == CommandError.None);
            }

            var canFeed = _session.CanFeed(id);
            _feed.Interactable = canFeed == CommandError.None;
            _feedRow.gameObject.SetActive(canFeed == CommandError.None);
            _feed.Text.text = canFeed == CommandError.None ? "" : Texts.Error(canFeed);
            if (canFeed == CommandError.None)
            {
                var spend = _session.FoodToFeed(id);
                _feedSpend.text = "−" + spend;
                _feedGain.text = "+" + Mathf.Min(spend * config.Needs.SatietyPerFood, config.Needs.Max - cat.Satiety);
            }
            Recommend(_feed.Rect, ref _feedAdvised, canFeed == CommandError.None && CatRules.IsHungry(cat, config));

            // Stop only while the cat works; Feed takes the whole row otherwise
            _stop.Rect.gameObject.SetActive(working);
            _feed.Rect.Place(new Vector2(0, 0), new Vector2(working ? 0.5f : 1f, 0), new Vector2(30, 40), new Vector2(working ? -15 : -30, 150));
            // text sizes are known only once the sheet is shown: fit on the next tick
            _fitFeed = true;

            Tick();
        }

        /// <summary>Shrinks the feed line to the button (half the width while Stop is shown).</summary>
        private void FitFeedRow()
        {
            _feedRow.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_feedRow);
            var need = LayoutUtility.GetPreferredWidth(_feedRow);
            var room = _feed.Rect.rect.width - 60f;
            var scale = need > room && need > 0f ? room / need : 1f;
            _feedRow.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Cycle bars of the current activity; call every frame while open.</summary>
        public void Tick()
        {
            if (_fitFeed && _root.activeInHierarchy)
            {
                _fitFeed = false;
                FitFeedRow();
            }
            if (_catId == null || !_session.World.TryGetCat(_catId.Value, out var cat) || !cat.Activity.IsWorking)
                return;
            var progress = ActivitySystem.GetCycleProgress(cat, _session.World.GameTimeMs);
            _statusCycle.Set(progress);
            foreach (var button in _buttons)
                button.SetProgress(progress);
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

        /// <summary>Gently pulses the button while it is the sensible next step.</summary>
        private static void Recommend(RectTransform button, ref bool state, bool on)
        {
            if (state == on)
                return;
            state = on;
            button.DOKill();
            button.localScale = Vector3.one;
            if (on)
                button.DOScale(1.015f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(button.gameObject);
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

        /// <summary>Thin cycle progress bar: a dark track with a blue fill.</summary>
        private sealed class CycleBar
        {
            private readonly RectTransform _fill;

            public RectTransform Root { get; }

            public CycleBar(Transform parent, string name)
            {
                var track = Ui.Panel(parent, name, new Color(0f, 0f, 0f, 0.35f));
                Root = track.rectTransform;
                var fill = Ui.Panel(track.transform, "Fill", Palette.Progress);
                _fill = fill.rectTransform;
                _fill.Place(Vector2.zero, new Vector2(0f, 1f));
            }

            public void Set(float progress01) => _fill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
        }

        /// <summary>
        /// One activity: what it brings as a big icon with "+N" on it, the name, and below either its time and costs as
        /// icon chips or what is missing to start it.
        /// </summary>
        private sealed class ActivityButton
        {
            private readonly TMP_Text _gain;
            private readonly RectTransform _chips;
            private readonly TMP_Text _time;
            private readonly Color _timeColor;
            private readonly TMP_Text _reason;
            private readonly CycleBar _cycle;
            private bool _current;
            private bool _advised;

            public ActivityDefinition Activity { get; }
            public UiButton Button { get; }

            public ActivityButton(Transform parent, ActivityDefinition activity, Action onClick)
            {
                Activity = activity;
                Button = Ui.Button(parent, "Activity_" + activity.Id, Texts.ActivityName(activity), Palette.Secondary, onClick, 30);
                var rect = Button.Rect;

                var left = 0.04f;
                var outcome = Ui.Icon(rect, Texts.OutcomeIcon(activity));
                if (outcome != null)
                {
                    outcome.rectTransform.Place(new Vector2(0.03f, 0.12f), new Vector2(0.25f, 0.88f));
                    left = 0.26f;
                }
                _gain = Ui.Label(rect, "", 32, TextAnchor.LowerRight, Palette.Paper, heading: true);
                _gain.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0.25f, 0.6f), new Vector2(20, 16), Vector2.zero);
                _gain.Outlined();

                Button.Text.rectTransform.Place(new Vector2(left, 0.46f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(-24, -14));
                Button.Text.fontSizeMin = 22;

                _chips = Info.Row(rect, "Chips", 14f, TextAnchor.MiddleCenter);
                _chips.Place(new Vector2(left, 0.1f), new Vector2(1f, 0.46f), Vector2.zero, new Vector2(-24, 0));
                _time = Info.Chip(_chips, "clock", "", ChipHeight, ChipFont, Palette.Ink).GetComponentInChildren<TMP_Text>();
                _timeColor = _time.color;
                if (activity.EnergyCost > 0)
                    Info.Chip(_chips, "energy", "−" + activity.EnergyCost, ChipHeight, ChipFont, Palette.Ink);
                if (activity.SatietyCost > 0)
                    Info.Chip(_chips, "satiety", "−" + activity.SatietyCost, ChipHeight, ChipFont, Palette.Ink);

                _reason = Ui.Label(rect, "", 26, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
                _reason.rectTransform.Place(new Vector2(left, 0.1f), new Vector2(1f, 0.46f), Vector2.zero, new Vector2(-24, 0));

                _cycle = new CycleBar(rect, "Cycle");
                _cycle.Root.Place(new Vector2(0.06f, 0f), new Vector2(0.94f, 0f), new Vector2(0, 14), new Vector2(0, 24));
            }

            public void ShowAvailable(long durationMs, int output)
            {
                Button.Interactable = true;
                _chips.gameObject.SetActive(true);
                _reason.gameObject.SetActive(false);
                _gain.text = "+" + output;
                // a hungry cat works twice as slowly: the time turns red
                var slow = durationMs > Activity.DurationMs;
                _time.text = Texts.Seconds(durationMs) + (slow ? " ×2" : "");
                _time.color = slow ? Palette.Warning : _timeColor;
            }

            public void ShowUnavailable(string reason)
            {
                Button.Interactable = false;
                _chips.gameObject.SetActive(false);
                _reason.gameObject.SetActive(true);
                _reason.text = reason;
                _gain.text = "";
            }

            public void SetCurrent(bool current)
            {
                _current = current;
                Button.SetColor(current ? Palette.Primary : Palette.Secondary);
                _cycle.Root.gameObject.SetActive(current);
            }

            public void SetProgress(float progress01)
            {
                if (_current) _cycle.Set(progress01);
            }

            public void SetRecommended(bool on) => Recommend(Button.Rect, ref _advised, on);
        }
    }
}
