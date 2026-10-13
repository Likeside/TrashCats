using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Animation;
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
        /// <summary>Box of a cat without art (graybox), in sprite pixels.</summary>
        private static readonly Vector2 GrayboxCat = new Vector2(260, 220);

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
        private readonly Dictionary<ActivityKind, float> _propBase = new Dictionary<ActivityKind, float>();
        private UiFlipbook _wheel;
        private FlipbookClip _wheelClip;
        private RectTransform _can;
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
                var reason = working ? "" : Texts.IdleBubble(cat.Activity.IdleReason);
                token.Bubble.text = reason;
                var back = token.Bubble.transform.parent.gameObject;
                if (back.activeSelf != (reason.Length > 0))
                    back.SetActive(reason.Length > 0);
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
                var wheel = activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Toughness ? _art?.Find("wheel") : null;
                if (wheel != null && wheel.HasBattleArt)
                {
                    // the wheel is an 8-frame spin clip; it turns while a cat runs in it
                    var picture = Ui.Picture(box.transform, "Prop", wheel.idle.frames[0]);
                    _wheel = picture.gameObject.AddComponent<UiFlipbook>();
                    _wheelClip = wheel.idle;
                }
                else if (prop != null)
                {
                    var picture = Ui.Picture(box.transform, "Prop", prop);
                    if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Strength)
                    {
                        // the can swings from the top of its rope when punched
                        picture.rectTransform.pivot = new Vector2(0.5f, 1f);
                        picture.rectTransform.Fill();
                        _can = picture.rectTransform;
                    }
                }
                _depthItems.Add((box.transform, min.y));
                if (activity.Kind == ActivityKind.Sleep || (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Toughness))
                    _propBase[activity.Kind == ActivityKind.Sleep ? ActivityKind.Sleep : ActivityKind.Train] = min.y;

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
            var label = activity.Kind == ActivityKind.Scavenge ? Loc.T("hideout_scene_view.01") : upgrade != null ? Texts.UpgradeName(upgrade.Value) : Texts.ActivityShort(activity);
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
                text.text = Loc.T("hideout_scene_view.02", Texts.UpgradeShort(id), _session.World.GetUpgradeLevel(id), (CanAfford(id) ? " (+)" : ""));
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
            // a transparent tap area (the box) with the picture as a child, so only the picture is mirrored
            var hit = Ui.Panel(_frame, "Cat_" + cat.Id, new Color(0, 0, 0, 0), raycast: true);
            var rt = hit.rectTransform;
            var image = Ui.Panel(rt, "Body", hasArt ? Color.white : color);
            image.rectTransform.Fill();
            var flipbook = image.gameObject.AddComponent<UiFlipbook>();
            if (hasArt)
                image.sprite = catArt.HideoutPose(0);
            var button = hit.gameObject.AddComponent<Button>();
            var id = cat.Id;
            button.onClick.AddListener(() => _onCatTapped(id));

            var name = Ui.Label(rt, Texts.CatName(cat.Id), 26, hasArt ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter, hasArt ? Palette.Paper : CatColors.TextOn(color));
            name.rectTransform.Fill(4);
            if (hasArt)
            {
                name.rectTransform.Place(new Vector2(-0.3f, 0), new Vector2(1.3f, 0), new Vector2(0, -30), new Vector2(0, 4));
                name.Outlined();
            }

            // idle reason in a comic speech bubble over the head (UI_UX §12), pulsing while the cat waits
            var bubbleBack = Ui.Panel(rt, "Bubble", Color.white);
            bubbleBack.sprite = Ui.Skin?.bubble;
            bubbleBack.color = Ui.Skin?.bubble != null ? Color.white : new Color(1, 1, 1, 0);
            bubbleBack.rectTransform.Place(new Vector2(0.45f, 1f), new Vector2(0.45f, 1f));
            bubbleBack.rectTransform.pivot = new Vector2(0.1f, 0f);
            bubbleBack.rectTransform.sizeDelta = new Vector2(190, 100);
            bubbleBack.rectTransform.anchoredPosition = new Vector2(0, -6);
            bubbleBack.gameObject.SetActive(false);
            bubbleBack.rectTransform.DOScale(1.08f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(bubbleBack.gameObject);
            var bubble = Ui.Label(bubbleBack.transform, "", 26, TextAnchor.MiddleCenter, Ui.Skin?.bubble != null ? Palette.Ink : Palette.Primary, heading: true);

            var progress = Ui.Bar(rt, "Progress", Palette.Progress);
            // under the name at the cat's feet: above the head it ran into the station captions
            progress.Rect.Place(new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0, -56), new Vector2(0, -34));
            bubble.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 26), new Vector2(-12, -10));
            if (Ui.Skin?.bubble == null) bubble.Outlined();

            var token = new CatToken(rt, bubble, progress) { Art = hasArt ? catArt : null, Image = image, Flipbook = flipbook, BaseColor = hasArt ? Color.white : color };
            flipbook.FrameChanged += frame => OnCatFrame(token, frame);
            _tokens[cat.Id] = token;
        }

        private void LayoutTokens(bool animate)
        {
            var world = _session.World;
            var config = _session.Config;
            var groups = new Dictionary<string, int>();
            var idle = 0;
            var wheelBusy = false;
            for (var i = 0; i < world.Cats.Count; i++)
            {
                var cat = world.Cats[i];
                if (!_tokens.TryGetValue(cat.Id, out var token))
                    continue;

                HideoutLayout.CatSpot spot;
                ActivityKind? kind = null;
                if (cat.Activity.IsWorking)
                {
                    var activity = config.GetActivity(cat.Activity.ActivityId);
                    kind = activity.Kind;
                    groups.TryGetValue(activity.Id.Value, out var index);
                    groups[activity.Id.Value] = index + 1;
                    spot = HideoutLayout.Spot(activity, index);
                    // one cat fits in the wheel / on the bed; others wait beside it
                    if (spot.OverProp && index > 0)
                        spot = new HideoutLayout.CatSpot(spot.Foot + new Vector2(-0.12f * index, -0.12f), HideoutLayout.CatClip.Sit);
                    wheelBusy |= spot.Clip == HideoutLayout.CatClip.Run;
                }
                else
                    spot = HideoutLayout.Idle(idle++);

                var moved = token.Placed && (spot.Foot != token.Spot.Foot || spot.Clip != token.Spot.Clip);
                token.Spot = spot;
                token.Depth = spot.OverProp && kind != null && _propBase.TryGetValue(kind == ActivityKind.Sleep ? ActivityKind.Sleep : ActivityKind.Train, out var baseY)
                    ? baseY - 0.002f
                    : spot.Foot.y - 0.001f;
                if (animate && moved)
                    Teleport(token);
                else
                    Show(token);
                token.Placed = true;
            }

            if (_wheel != null)
            {
                if (wheelBusy) _wheel.Play(_wheelClip);
                else _wheel.Stop();
            }

            SortByDepth();
        }

        /// <summary>
        /// Puts the cat on its spot in the right clip. Box size follows the sprite's pixel size (clips are imported at a
        /// common scale), smaller when farther away.
        /// </summary>
        private void Show(CatToken token)
        {
            var spot = token.Spot;
            token.Root.gameObject.SetActive(!spot.Hidden);
            var (clip, sourceFacesLeft) = ClipFor(token, spot.Clip);
            var size = clip != null && clip.frames.Length > 0 ? clip.frames[0].rect.size : GrayboxCat;
            if (clip != null)
            {
                if (clip.frames.Length > 1) token.Flipbook.Play(clip);
                else
                {
                    token.Flipbook.Stop();
                    token.Image.sprite = clip.frames[0];
                }
            }

            var h = size.y * HideoutLayout.HeightPerPixel * HideoutLayout.DepthScale(spot.Foot.y) * spot.Scale;
            var w = h * size.x / size.y / HideoutLayout.PictureAspect;
            token.Root.Place(new Vector2(spot.Foot.x - w * 0.5f, spot.Foot.y), new Vector2(spot.Foot.x + w * 0.5f, spot.Foot.y + h));
            token.Image.rectTransform.localScale = new Vector3(spot.FaceLeft != sourceFacesLeft ? -1f : 1f, 1f, 1f);
            token.Image.color = token.BaseColor;
        }

        /// <summary>Clip for a hideout pose and whether its frames face left (run frames are drawn facing left).</summary>
        private static (FlipbookClip Clip, bool FacesLeft) ClipFor(CatToken token, HideoutLayout.CatClip clip)
        {
            var art = token.Art;
            if (art == null)
                return (null, false);
            FlipbookClip Pose(int index) => art.HideoutPose(index) is Sprite sprite ? new FlipbookClip { frames = new[] { sprite } } : null;
            return clip switch
            {
                HideoutLayout.CatClip.Run when art.run?.frames.Length > 0 => (art.run, true),
                HideoutLayout.CatClip.Punch when art.canPunch?.frames.Length > 0 => (art.canPunch, false),
                HideoutLayout.CatClip.Sleep => (Pose(1) ?? Pose(0), false),
                HideoutLayout.CatClip.Walk or HideoutLayout.CatClip.Run => (Pose(3) ?? Pose(0), false),
                HideoutLayout.CatClip.Punch => (Pose(3) ?? Pose(0), false),
                _ => (Pose(0), false),
            };
        }

        /// <summary>Activity change: the cat vanishes in a puff of dust and pops up at the new station (no walking).</summary>
        private void Teleport(CatToken token)
        {
            var root = token.Root;
            root.DOKill();
            if (root.gameObject.activeSelf)
                Puff(root);
            DOTween.Sequence()
                .Append(root.DOScale(0f, 0.12f).SetEase(Ease.InBack))
                .AppendCallback(() =>
                {
                    Show(token);
                    SortByDepth();
                    if (root.gameObject.activeSelf) Puff(root);
                })
                .Append(root.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .SetTarget(root)
                .SetLink(root.gameObject);
        }

        private void Puff(RectTransform at)
        {
            var sprite = Ui.Skin?.fxDust;
            if (sprite == null)
                return;
            var dust = Ui.Panel(_frame, "Puff", Color.white);
            dust.sprite = sprite;
            dust.preserveAspect = true;
            var rt = dust.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2((at.anchorMin.x + at.anchorMax.x) * 0.5f, at.anchorMin.y);
            rt.pivot = new Vector2(0.5f, 0.2f);
            rt.sizeDelta = new Vector2(220, 200);
            rt.localScale = Vector3.one * 0.4f;
            DOTween.Sequence()
                .Append(rt.DOScale(1.1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(dust.DOFade(0f, 0.4f).SetEase(Ease.InQuad))
                .OnComplete(() => UnityEngine.Object.Destroy(dust.gameObject))
                .SetLink(dust.gameObject);
        }

        /// <summary>Punch frames (jab and hook fully extended) knock the can.</summary>
        private void OnCatFrame(CatToken token, int frame)
        {
            if (_can == null || token.Spot.Clip != HideoutLayout.CatClip.Punch || (frame != 2 && frame != 5))
                return;
            _can.DOComplete();
            _can.DOPunchRotation(new Vector3(0, 0, frame == 5 ? -16f : -10f), 0.55f, 4, 0.6f).SetLink(_can.gameObject);
            var star = Ui.Skin?.fxStar;
            if (star == null)
                return;
            var flash = Ui.Panel(_can, "Hit", Color.white);
            flash.sprite = star;
            flash.preserveAspect = true;
            flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = new Vector2(0.75f, 0.2f);
            flash.rectTransform.sizeDelta = new Vector2(110, 110);
            flash.rectTransform.localScale = Vector3.one * 0.5f;
            DOTween.Sequence()
                .Append(flash.rectTransform.DOScale(1f, 0.08f))
                .Append(flash.DOFade(0f, 0.18f))
                .OnComplete(() => UnityEngine.Object.Destroy(flash.gameObject))
                .SetLink(flash.gameObject);
        }

        /// <summary>Nearer things (lower base line) draw on top; cats on or in a prop draw over it.</summary>
        private void SortByDepth()
        {
            var items = new List<(Transform Item, float BaseY)>(_depthItems);
            foreach (var token in _tokens.Values)
                items.Add((token.Root, token.Depth));
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
            public UiFlipbook Flipbook;
            public Color BaseColor;
            public HideoutLayout.CatSpot Spot;
            public float Depth;
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

        /// <summary>Card backdrop behind the portrait (cat card concept: orange, slate blue, purple).</summary>
        public static Color Backdrop(CatId id) => id.Value switch
        {
            "cat_ryzhy" => Palette.Hex("#D9821E"),
            "cat_shnyr" => Palette.Hex("#4F6E8C"),
            "cat_ponchik" => Palette.Hex("#6B5B95"),
            _ => Palette.PanelDark,
        };

        public static Color TextOn(Color background) => Ui.Skin != null ? Palette.Paper : TextOnPlain(background);

        private static Color TextOnPlain(Color background) =>
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
