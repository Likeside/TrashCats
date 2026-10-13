using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Hideout scene: the background picture with station props and cats placed in the picture's own 0..1 space
    /// (<see cref="HideoutLayout"/>), so everything stays on its spot on any screen. Cats walk to the station of their
    /// activity, show a cycle progress bar, an idle-reason bubble and floating results; nearer things draw on top.
    /// Without art the props and cats are graybox boxes on the same spots.
    /// </summary>
    public sealed class HideoutSceneView : IDisposable
    {
        /// <summary>Width / height of a cat's box (hideout poses keep their aspect inside).</summary>
        private const float CatBoxAspect = 1.25f;

        private readonly GameSession _session;
        private readonly RectTransform _root;
        private readonly RectTransform _frame;
        private readonly List<(Transform Item, float BaseY)> _depthItems = new List<(Transform, float)>();
        private readonly Action<CatId> _onCatTapped;
        private readonly Action<UpgradeId> _onStationTapped;
        private readonly CharacterViewFactory _art;
        private readonly Dictionary<CatId, CatToken> _tokens = new Dictionary<CatId, CatToken>();
        private readonly Dictionary<UpgradeId, TMP_Text> _upgradeLabels = new Dictionary<UpgradeId, TMP_Text>();
        private readonly Dictionary<UpgradeId, RectTransform> _stations = new Dictionary<UpgradeId, RectTransform>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public HideoutSceneView(GameSession session, EventBus bus, RectTransform root, Action<CatId> onCatTapped, Action<UpgradeId> onStationTapped,
            CharacterViewFactory art)
        {
            _art = art;
            _session = session;
            _root = root;
            _onCatTapped = onCatTapped;
            _onStationTapped = onStationTapped;

            Ui.Panel(root, "Floor", Palette.Scene).rectTransform.Fill();
            root.gameObject.AddComponent<RectMask2D>();
            _frame = Ui.Rect(root, "Picture");
            var fit = _frame.gameObject.AddComponent<CoverFit>();
            fit.aspect = HideoutLayout.PictureAspect;
            fit.focus = HideoutLayout.Focus;
            fit.Apply();
            var background = art?.GetSprite("bg_hideout");
            if (background != null)
            {
                var picture = Ui.Panel(_frame, "BackgroundArt", new Color(0.92f, 0.92f, 0.92f));
                picture.sprite = background;
                picture.rectTransform.Fill();
            }
            BuildStations();
            foreach (var cat in session.World.Cats)
                AddToken(cat);
            LayoutTokens(animate: false);

            _subscriptions.Add(bus.Subscribe<ActivityStarted>(e => { if (!e.IsRepeat) LayoutTokens(true); }));
            _subscriptions.Add(bus.Subscribe<ActivityStopped>(_ => LayoutTokens(true)));
            _subscriptions.Add(bus.Subscribe<ActivityCycleCompleted>(OnCycleCompleted));
            _subscriptions.Add(bus.Subscribe<UpgradePurchased>(e =>
            {
                RefreshUpgrade(e.UpgradeId);
                Sparkle(e.UpgradeId);
            }));
        }

        /// <summary>Adds tokens for newly recruited cats.</summary>
        public void SyncCats()
        {
            var added = false;
            foreach (var cat in _session.World.Cats)
            {
                if (_tokens.ContainsKey(cat.Id)) continue;
                AddToken(cat);
                added = true;
            }

            if (added) LayoutTokens(animate: false);
        }

        public void Tick()
        {
            var world = _session.World;
            foreach (var cat in world.Cats)
            {
                if (!_tokens.TryGetValue(cat.Id, out var token))
                    continue;
                var working = cat.Activity.IsWorking;
                token.Progress.Rect.gameObject.SetActive(working);
                if (working)
                    token.Progress.Set(ActivitySystem.GetCycleProgress(cat, world.GameTimeMs));
                token.Bubble.text = working ? "" : Texts.IdleBubble(cat.Activity.IdleReason);
            }
        }

        public void Dispose()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            foreach (var token in _tokens.Values)
                token.Root.DOKill();
        }

        private void BuildStations()
        {
            var config = _session.Config;
            foreach (var activity in config.Activities)
            {
                var (label, upgrade) = StationInfo(activity);
                var prop = upgrade != null ? _art?.GetSprite("prop_" + upgrade.Value) : null;
                var (min, max) = HideoutLayout.Station(activity);
                var scavenge = activity.Kind == ActivityKind.Scavenge;
                var box = Ui.Panel(_frame, "Station_" + activity.Id, scavenge || prop != null ? new Color(0, 0, 0, 0) : Palette.Station);
                box.rectTransform.Place(min, max);
                if (prop != null)
                    Ui.Picture(box.transform, "Prop", prop);
                _depthItems.Add((box.transform, min.y));

                var text = Ui.Label(box.transform, label, 26, scavenge ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter, Palette.Paper);
                // the caption sits just under the prop's base (in the street passage for the exit)
                text.rectTransform.Place(new Vector2(-0.3f, 0), new Vector2(1.3f, 0), new Vector2(0, -34), new Vector2(0, 0));
                if (scavenge)
                    text.rectTransform.Fill();
                text.Outlined();
                if (upgrade != null)
                {
                    _upgradeLabels[upgrade.Value] = text;
                    RefreshUpgrade(upgrade.Value);
                    var id = upgrade.Value;
                    _stations[id] = box.rectTransform;
                    box.raycastTarget = true;
                    box.gameObject.AddComponent<Button>().onClick.AddListener(() => _onStationTapped(id));
                    box.name = "Station_" + id;
                }
            }
        }

        private (string Label, UpgradeId? Upgrade) StationInfo(ActivityDefinition activity)
        {
            var config = _session.Config;
            UpgradeId? upgrade = activity.Kind switch
            {
                ActivityKind.Sleep => config.Upgrades.FirstOrDefault(u => u.Effect is SleepEnergyBonusEffect)?.Id,
                ActivityKind.Rummage => config.Upgrades.FirstOrDefault(u => u.Effect is ResourceOutputBonusEffect)?.Id,
                ActivityKind.Train => config.Upgrades.FirstOrDefault(u => u.Effect is TrainingXpBonusEffect t && t.Attribute == activity.TrainedAttribute)?.Id,
                _ => null,
            };
            var label = activity.Kind == ActivityKind.Scavenge ? "Улица →" : upgrade != null ? Texts.UpgradeName(upgrade.Value) : Texts.ActivityShort(activity);
            return (label, upgrade);
        }

        /// <summary>Sparkles over a station that was just upgraded (CONTENT_SPEC §9).</summary>
        private void Sparkle(UpgradeId id)
        {
            var sprite = Ui.Skin?.fxSparkle;
            if (sprite == null || !_stations.TryGetValue(id, out var station))
                return;
            station.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6, 0.5f);
            for (var i = 0; i < 3; i++)
            {
                var image = Ui.Panel(station, "Sparkle", Color.white);
                image.sprite = sprite;
                image.preserveAspect = true;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.2f + 0.3f * i, 0.6f + 0.15f * (i % 2));
                rt.sizeDelta = new Vector2(120, 140);
                rt.localScale = Vector3.zero;
                DOTween.Sequence()
                    .AppendInterval(0.12f * i)
                    .Append(rt.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                    .Join(rt.DOAnchorPosY(70f, 0.8f).SetRelative())
                    .Append(image.DOFade(0f, 0.35f))
                    .OnComplete(() => UnityEngine.Object.Destroy(image.gameObject))
                    .SetLink(image.gameObject);
            }
        }

        private void RefreshUpgrade(UpgradeId id)
        {
            if (_upgradeLabels.TryGetValue(id, out var text))
                text.text = $"{Texts.UpgradeShort(id)} · ур. {_session.World.GetUpgradeLevel(id)}{(CanAfford(id) ? " (+)" : "")}";
        }

        private bool CanAfford(UpgradeId id)
        {
            var definition = _session.Config.GetUpgrade(id);
            var level = _session.World.GetUpgradeLevel(id);
            return level < definition.MaxLevel && _session.World.Junk >= definition.Costs[level];
        }

        public void RefreshUpgrades()
        {
            foreach (var id in _upgradeLabels.Keys)
                RefreshUpgrade(id);
        }

        private void AddToken(IReadOnlyCat cat)
        {
            var color = CatColors.For(cat.Id);
            var catArt = _art?.Find(_session.Config.GetCat(cat.Id).ViewKey);
            var hasArt = catArt?.HideoutPose(0) != null;
            var image = Ui.Panel(_frame, "Cat_" + cat.Id, hasArt ? Color.white : color, raycast: true);
            var rt = image.rectTransform;
            if (hasArt)
            {
                image.sprite = catArt.HideoutPose(0);
                image.preserveAspect = true;
            }
            var button = image.gameObject.AddComponent<Button>();
            var id = cat.Id;
            button.onClick.AddListener(() => _onCatTapped(id));

            var name = Ui.Label(rt, Texts.CatName(cat.Id), 26, hasArt ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter, hasArt ? Palette.Paper : CatColors.TextOn(color));
            name.rectTransform.Fill(4);
            if (hasArt)
            {
                name.rectTransform.Place(new Vector2(-0.3f, 0), new Vector2(1.3f, 0), new Vector2(0, -30), new Vector2(0, 4));
                name.Outlined();
            }

            var bubble = Ui.Label(rt, "", 26, TextAnchor.LowerCenter, Palette.Primary);
            bubble.rectTransform.Place(new Vector2(-0.5f, 1f), new Vector2(1.5f, 1f), new Vector2(0, 4), new Vector2(0, 44));

            var progress = Ui.Bar(rt, "Progress", Palette.Progress);
            // under the name at the cat's feet: above the head it ran into the station captions
            progress.Rect.Place(new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0, -56), new Vector2(0, -34));
            bubble.rectTransform.Place(new Vector2(-0.5f, 1f), new Vector2(1.5f, 1f), new Vector2(0, 28), new Vector2(0, 68));
            bubble.Outlined();

            _tokens[cat.Id] = new CatToken(rt, bubble, progress) { Art = hasArt ? catArt : null, Image = image, BaseColor = hasArt ? Color.white : color };
        }

        private void LayoutTokens(bool animate)
        {
            var world = _session.World;
            var config = _session.Config;
            var groups = new Dictionary<string, int>();
            var idle = 0;
            for (var i = 0; i < world.Cats.Count; i++)
            {
                var cat = world.Cats[i];
                if (!_tokens.TryGetValue(cat.Id, out var token))
                    continue;

                Vector2 foot;
                var away = false;
                if (cat.Activity.IsWorking)
                {
                    var activity = config.GetActivity(cat.Activity.ActivityId);
                    groups.TryGetValue(activity.Id.Value, out var index);
                    groups[activity.Id.Value] = index + 1;
                    foot = HideoutLayout.CatSpot(activity, index);
                    away = activity.Kind == ActivityKind.Scavenge;
                }
                else
                    foot = HideoutLayout.IdleSpot(idle++);

                token.Root.DOKill();
                if (animate && token.Placed)
                {
                    var from = token.Foot;
                    DOVirtual.Float(0f, 1f, 0.45f, t => PlaceCat(token, Vector2.Lerp(from, foot, t))).SetEase(Ease.OutQuad).SetTarget(token.Root).SetLink(token.Root.gameObject);
                }
                else
                    PlaceCat(token, foot);
                token.Foot = foot;
                token.Placed = true;

                var color = token.BaseColor;
                token.Image.color = away ? new Color(color.r, color.g, color.b, 0.45f) : color;
                if (token.Art != null)
                {
                    var kind = cat.Activity.IsWorking ? config.GetActivity(cat.Activity.ActivityId).Kind : (ActivityKind?)null;
                    var pose = kind == ActivityKind.Sleep ? 1 : kind == null ? 0 : 3;
                    token.Image.sprite = token.Art.HideoutPose(pose) ?? token.Art.HideoutPose(0);
                }
            }

            SortByDepth();
        }

        /// <summary>Cat box standing on <paramref name="foot"/>, smaller when farther away.</summary>
        private static void PlaceCat(CatToken token, Vector2 foot)
        {
            var h = HideoutLayout.CatHeight * HideoutLayout.DepthScale(foot.y);
            var w = h * CatBoxAspect * HideoutLayout.PictureAspect;
            token.Root.Place(new Vector2(foot.x - w * 0.5f, foot.y), new Vector2(foot.x + w * 0.5f, foot.y + h));
        }

        /// <summary>Nearer things (lower base line) draw on top.</summary>
        private void SortByDepth()
        {
            var items = new List<(Transform Item, float BaseY)>(_depthItems);
            foreach (var token in _tokens.Values)
                items.Add((token.Root, token.Foot.y - 0.001f));
            items.Sort((a, b) => b.BaseY.CompareTo(a.BaseY));
            var first = _frame.Find("BackgroundArt") != null ? 1 : 0;
            for (var i = 0; i < items.Count; i++)
                items[i].Item.SetSiblingIndex(first + i);
        }

        private void OnCycleCompleted(ActivityCycleCompleted e)
        {
            if (!_tokens.TryGetValue(e.CatId, out var token))
                return;

            token.Root.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
            var text = e.Kind == ActivityKind.Train
                ? $"+{e.Output} {Texts.AttributeGenitive(e.TrainedAttribute).ToLowerInvariant()}"
                : Texts.Output(_session.Config.GetActivity(e.ActivityId), e.Output);
            FloatingText.Spawn(_root, token.Root, text);
        }

        private sealed class CatToken
        {
            public RectTransform Root { get; }
            public TMP_Text Bubble { get; }
            public UiBar Progress { get; }
            public CharacterArt Art;
            public Image Image;
            public Color BaseColor;
            public Vector2 Foot;
            public bool Placed;

            public CatToken(RectTransform root, TMP_Text bubble, UiBar progress)
            {
                Root = root;
                Bubble = bubble;
                Progress = progress;
            }
        }
    }

    public static class CatColors
    {
        public static Color For(CatId id) => id.Value switch
        {
            "cat_ryzhy" => Palette.Hex("#DC811D"),
            "cat_shnyr" => Palette.Hex("#7D7873"),
            "cat_ponchik" => Palette.Hex("#24272C"),
            _ => Color.gray,
        };

        public static Color TextOn(Color background) =>
            background.r * 0.299f + background.g * 0.587f + background.b * 0.114f > 0.5f ? Palette.Ink : Palette.Paper;
    }

    /// <summary>Rising, fading text above a target (cycle results).</summary>
    public static class FloatingText
    {
        public static void Spawn(RectTransform parent, RectTransform target, string text)
        {
            var label = Ui.Label(parent, text, 32, TextAnchor.MiddleCenter, Palette.Primary);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 50);
            // above the target's head, wherever the target lives in the hierarchy
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            rt.position = (corners[1] + corners[2]) * 0.5f;
            rt.anchoredPosition += new Vector2(0, 60);
            label.Outlined();

            DOTween.Sequence()
                .Append(rt.DOAnchorPosY(rt.anchoredPosition.y + 90, 1.1f).SetEase(Ease.OutCubic))
                .Join(label.DOFade(0f, 1.1f).SetEase(Ease.InQuad))
                .OnComplete(() => UnityEngine.Object.Destroy(label.gameObject))
                .SetLink(label.gameObject);
        }
    }
}
