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
    /// Graybox hideout: stations as labelled boxes, cats as coloured tokens that walk to the station of their activity,
    /// show a cycle progress bar, an idle-reason bubble and floating results.
    /// </summary>
    public sealed class HideoutSceneView : IDisposable
    {
        private static readonly Vector2 TokenSize = new Vector2(130, 130);

        private readonly GameSession _session;
        private readonly RectTransform _root;
        private readonly Action<CatId> _onCatTapped;
        private readonly Action<UpgradeId> _onStationTapped;
        private readonly CharacterViewFactory _art;
        private readonly Dictionary<CatId, CatToken> _tokens = new Dictionary<CatId, CatToken>();
        private readonly Dictionary<UpgradeId, TMP_Text> _upgradeLabels = new Dictionary<UpgradeId, TMP_Text>();
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
            var background = art?.GetSprite("bg_hideout");
            if (background != null)
                Ui.Cover(root, "BackgroundArt", background, new Color(0.85f, 0.85f, 0.85f));
            BuildStations();
            foreach (var cat in session.World.Cats)
                AddToken(cat);
            LayoutTokens(animate: false);

            _subscriptions.Add(bus.Subscribe<ActivityStarted>(e => { if (!e.IsRepeat) LayoutTokens(true); }));
            _subscriptions.Add(bus.Subscribe<ActivityStopped>(_ => LayoutTokens(true)));
            _subscriptions.Add(bus.Subscribe<ActivityCycleCompleted>(OnCycleCompleted));
            _subscriptions.Add(bus.Subscribe<UpgradePurchased>(e => RefreshUpgrade(e.UpgradeId)));
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
                var size = activity.Kind == ActivityKind.Scavenge ? new Vector2(150, 360) : prop != null ? new Vector2(260, 230) : new Vector2(240, 150);
                var box = Ui.Panel(_root, "Station_" + activity.Id, activity.Kind == ActivityKind.Scavenge ? new Color(0.1f, 0.1f, 0.12f, 0.35f) : Palette.Station);
                box.rectTransform.At(StationAnchor(activity), size);
                TMP_Text text;
                if (prop != null)
                {
                    box.color = new Color(0, 0, 0, 0);
                    Ui.Picture(box.transform, "Prop", prop).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(0, 40), Vector2.zero);
                    text = Ui.Label(box.transform, label, 26, TextAnchor.LowerCenter, Palette.Paper);
                    text.rectTransform.Fill(0);
                    text.Outlined();
                }
                else
                {
                    text = Ui.Label(box.transform, label, 28, TextAnchor.UpperCenter, Palette.Paper);
                    text.rectTransform.Fill(8);
                }
                if (upgrade != null)
                {
                    _upgradeLabels[upgrade.Value] = text;
                    RefreshUpgrade(upgrade.Value);
                    var id = upgrade.Value;
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

        private static Vector2 StationAnchor(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => new Vector2(0.93f, 0.48f),
            ActivityKind.Rummage => new Vector2(0.2f, 0.47f),
            ActivityKind.Sleep => new Vector2(0.7f, 0.2f),
            _ => activity.TrainedAttribute switch
            {
                AttributeType.Strength => new Vector2(0.17f, 0.84f),
                AttributeType.Agility => new Vector2(0.5f, 0.84f),
                _ => new Vector2(0.83f, 0.84f),
            },
        };

        private void AddToken(IReadOnlyCat cat)
        {
            var color = CatColors.For(cat.Id);
            var catArt = _art?.Find(_session.Config.GetCat(cat.Id).ViewKey);
            var hasArt = catArt?.HideoutPose(0) != null;
            var image = Ui.Panel(_root, "Cat_" + cat.Id, hasArt ? Color.white : color, raycast: true);
            var rt = image.rectTransform;
            rt.At(new Vector2(0.5f, 0.1f), hasArt ? new Vector2(220, 180) : TokenSize);
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
                name.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -34), new Vector2(0, 0));
                name.Outlined();
            }

            var bubble = Ui.Label(rt, "", 26, TextAnchor.LowerCenter, Palette.Primary);
            bubble.rectTransform.Place(new Vector2(-0.5f, 1f), new Vector2(1.5f, 1f), new Vector2(0, 4), new Vector2(0, 44));

            var progress = Ui.Bar(rt, "Progress", Palette.Progress);
            progress.Rect.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, -22), new Vector2(0, -8));

            _tokens[cat.Id] = new CatToken(rt, bubble, progress) { Art = hasArt ? catArt : null, Image = image, BaseColor = hasArt ? Color.white : color };
        }

        private void LayoutTokens(bool animate)
        {
            var world = _session.World;
            var config = _session.Config;
            var groups = new Dictionary<string, int>();
            for (var i = 0; i < world.Cats.Count; i++)
            {
                var cat = world.Cats[i];
                if (!_tokens.TryGetValue(cat.Id, out var token))
                    continue;

                Vector2 anchor;
                string key;
                var away = false;
                if (cat.Activity.IsWorking)
                {
                    var activity = config.GetActivity(cat.Activity.ActivityId);
                    anchor = StationAnchor(activity) + new Vector2(0f, activity.Kind == ActivityKind.Train ? -0.14f : 0.1f);
                    key = activity.Id.Value;
                    away = activity.Kind == ActivityKind.Scavenge;
                }
                else
                {
                    anchor = new Vector2(0.2f + 0.2f * i, 0.1f);
                    key = "idle" + i;
                }

                groups.TryGetValue(key, out var index);
                groups[key] = index + 1;
                var target = new Vector2(index * 90f - (index > 0 ? 45f : 0f), index * -30f);

                token.Root.DOKill();
                token.Root.anchorMin = token.Root.anchorMax = anchor;
                if (animate)
                    token.Root.DOAnchorPos(target, 0.35f).SetEase(Ease.OutQuad);
                else
                    token.Root.anchoredPosition = target;

                var color = token.BaseColor;
                token.Image.color = away ? new Color(color.r, color.g, color.b, 0.45f) : color;
                if (token.Art != null)
                {
                    var kind = cat.Activity.IsWorking ? config.GetActivity(cat.Activity.ActivityId).Kind : (ActivityKind?)null;
                    var pose = kind == ActivityKind.Sleep ? 1 : kind == null ? 0 : 3;
                    token.Image.sprite = token.Art.HideoutPose(pose) ?? token.Art.HideoutPose(0);
                }
            }
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
            rt.anchorMin = rt.anchorMax = target.anchorMin;
            rt.sizeDelta = new Vector2(320, 50);
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0, 90);
            label.Outlined();

            DOTween.Sequence()
                .Append(rt.DOAnchorPosY(rt.anchoredPosition.y + 90, 1.1f).SetEase(Ease.OutCubic))
                .Join(label.DOFade(0f, 1.1f).SetEase(Ease.InQuad))
                .OnComplete(() => UnityEngine.Object.Destroy(label.gameObject))
                .SetLink(label.gameObject);
        }
    }
}
