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
        private RectTransform _can;
        private GameObject _ropeProp;
        private Vector2 _streetGap;
        private RectTransform _dumpster;
        private int _rummaging;
        private float _junkTimer;
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
                {
                    var progress = ActivitySystem.GetCycleProgress(cat, world.GameTimeMs);
                    token.Progress.Set(progress);
                    token.Away.Bar.Set(progress);
                }
                var reason = working ? "" : Texts.IdleBubble(cat.Activity.IdleReason);
                token.Bubble.text = reason;
                var back = token.Bubble.transform.parent.gameObject;
                if (back.activeSelf != (reason.Length > 0))
                    back.SetActive(reason.Length > 0);
            }

            // junk tumbles out of the dumpster while someone rummages in it
            if (_rummaging > 0 && _dumpster != null && (_junkTimer -= Time.deltaTime) <= 0f)
            {
                _junkTimer = UnityEngine.Random.Range(0.25f, 0.5f);
                SpillJunk();
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
                // placed as in the layout scene: sprite, rect, rotation about the bottom centre, mirroring, draw order
                var place = HideoutLayout.Prop(activity);
                var prop = place.Sprite != null ? _art?.GetSprite(place.Sprite) : null;
                var (min, max) = (place.Min, place.Max);
                var scavenge = activity.Kind == ActivityKind.Scavenge;
                var box = Ui.Panel(_frame, "Station_" + activity.Id, scavenge || prop != null ? new Color(0, 0, 0, 0) : Palette.Station);
                box.rectTransform.Place(min, max);
                box.rectTransform.pivot = new Vector2(0.5f, 0f);
                box.rectTransform.localEulerAngles = new Vector3(0, 0, place.Rotation);
                if (prop != null)
                {
                    var picture = Ui.Picture(box.transform, "Prop", prop);
                    if (place.Flip)
                        picture.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                    if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Technique)
                    {
                        // the can swings from the top of its rope when punched
                        picture.rectTransform.pivot = new Vector2(0.5f, 1f);
                        picture.rectTransform.Fill();
                        _can = picture.rectTransform;
                        // a wrought-iron bracket sticks out of the wall; its hook holds the top of the can's rope
                        var bracketSprite = _art?.GetSprite(HideoutLayout.Bracket.Sprite);
                        if (bracketSprite != null)
                        {
                            var bracket = Ui.Panel(_frame, "Bracket", Color.white);
                            bracket.sprite = bracketSprite;
                            bracket.rectTransform.Place(HideoutLayout.Bracket.Min, HideoutLayout.Bracket.Max);
                            _depthItems.Add((bracket.transform, HideoutLayout.Bracket.Depth));
                        }
                    }
                }
                if (activity.Kind == ActivityKind.Rummage)
                {
                    _dumpster = box.rectTransform;
                    box.rectTransform.offsetMin += HideoutLayout.DumpsterShift;
                    box.rectTransform.offsetMax += HideoutLayout.DumpsterShift;
                }
                if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Endurance)
                    _ropeProp = box.transform.Find("Prop")?.gameObject;
                if (scavenge)
                    _streetGap = new Vector2((min.x + max.x) * 0.5f, min.y);
                _depthItems.Add((box.transform, place.Depth));

                var text = Plate(box.rectTransform, label, scavenge);
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

        /// <summary>
        /// Station caption on a small dark plate that hugs the text, under the prop's base (in the middle of the street
        /// gap for the exit), so it does not float in the air.
        /// </summary>
        private static TMP_Text Plate(RectTransform box, string caption, bool centred)
        {
            var plate = Ui.Panel(box, "Caption", Ui.Skin?.barFrame != null ? Color.white : new Color(0, 0, 0, 0.55f));
            plate.sprite = Ui.Skin?.barFrame;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 3.2f;
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, centred ? 0.5f : 0f);
            rt.pivot = new Vector2(0.5f, centred ? 0.5f : 1f);
            rt.anchoredPosition = new Vector2(0, centred ? 0 : -4);
            var text = Ui.Label(plate.transform, caption, 26, TextAnchor.MiddleCenter, Palette.Paper);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.Fill();
            FitPlate(text);
            return text;
        }

        /// <summary>Sizes a caption plate to its text plus padding.</summary>
        private static void FitPlate(TMP_Text text)
        {
            var size = text.GetPreferredValues(text.text);
            ((RectTransform)text.transform.parent).sizeDelta = new Vector2(size.x + 56f, size.y + 18f);
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
            if (!_upgradeLabels.TryGetValue(id, out var text))
                return;
            text.text = Loc.T("hideout_scene_view.02", Texts.UpgradeShort(id), _session.World.GetUpgradeLevel(id), (CanAfford(id) ? " (+)" : ""));
            FitPlate(text);
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
            // a narrow cycle bar right above the busy cat's head
            progress.Rect.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-75, 4), new Vector2(75, 24));
            bubble.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 26), new Vector2(-12, -10));
            if (Ui.Skin?.bubble == null) bubble.Outlined();

            var token = new CatToken(rt, bubble, progress) { Art = hasArt ? catArt : null, Image = image, Flipbook = flipbook, BaseColor = hasArt ? Color.white : color };
            token.Away = AwayBadge(cat.Id, hasArt ? catArt.portrait : null);
            flipbook.FrameChanged += frame => OnCatFrame(token, frame);
            _tokens[cat.Id] = token;
        }

        private void LayoutTokens(bool animate)
        {
            var world = _session.World;
            var config = _session.Config;
            var groups = new Dictionary<string, int>();
            var idle = 0;
            var ropeBusy = false;
            var away = 0;
            var rummaging = 0;
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
                    spot = HideoutLayout.Spot(activity, index, token.Art?.viewKey);
                    // one cat fits on the mat / on the bed; others wait beside it (behind the mat)
                    var (bMin, bMax) = (HideoutLayout.Bench.Min, HideoutLayout.Bench.Max);
                    if (spot.OverProp && index > 0)
                        spot = spot.Clip == HideoutLayout.CatClip.Bench
                            ? new HideoutLayout.CatSpot(new Vector2((bMin.x + bMax.x) * 0.5f + 0.12f * (index - 1), bMax.y + 0.01f), HideoutLayout.CatClip.Sit)
                            : new HideoutLayout.CatSpot(spot.Foot + new Vector2(-0.12f * index, -0.12f), HideoutLayout.CatClip.Sit);
                    ropeBusy |= spot.Clip == HideoutLayout.CatClip.Rope;
                    if (spot.Clip == HideoutLayout.CatClip.Bench && token.Art != null && HideoutLayout.BenchFit(token.Art.viewKey) is var (benchFoot, benchHeight))
                    {
                        // placed and sized against the mat's rect, as in the layout scene
                        var size = bMax - bMin;
                        spot = new HideoutLayout.CatSpot(bMin + Vector2.Scale(benchFoot, size), spot.Clip, spot.FaceLeft, spot.OverProp, height: benchHeight * size.y, depth: spot.Depth);
                    }
                }
                else
                    spot = HideoutLayout.Idle(token.Art?.viewKey, idle++);

                // out on the street or in the dumpster: a portrait with the cycle bar instead of the cat, right over the
                // dumpster or above it under the street gap (it fits under the HUD); more cats go to the left
                token.Away.Root.gameObject.SetActive(spot.Hidden);
                if (spot.Hidden)
                {
                    const float badge = 0.12f;
                    var rummage = kind == ActivityKind.Rummage;
                    var at = rummage
                        ? new Vector2(spot.Foot.x - 0.09f - 0.13f * rummaging++, spot.Foot.y + 0.002f)
                        : new Vector2(_streetGap.x - 0.06f - 0.13f * away++, spot.Foot.y + 0.002f - 0.035f);
                    token.Away.Root.Place(at, at + new Vector2(badge, badge));
                    token.Away.Caption.text = Loc.T(rummage ? "hideout.rummaging" : "hideout.away");
                }

                var moved = token.Placed && (spot.Foot != token.Spot.Foot || spot.Clip != token.Spot.Clip);
                token.Spot = spot;
                token.Depth = float.IsNaN(spot.Depth) ? spot.Foot.y - 0.001f : spot.Depth;
                if (animate && moved)
                    Teleport(token);
                else
                    Show(token);
                token.Placed = true;
            }

            _ropeProp?.SetActive(!ropeBusy);
            _rummaging = rummaging;

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

            var h = spot.Height > 0f ? spot.Height : size.y * HideoutLayout.HeightPerPixel * HideoutLayout.DepthScale(spot.Foot.y) * spot.Scale;
            var w = h * size.x / size.y / HideoutLayout.PictureAspect;
            token.Root.Place(new Vector2(spot.Foot.x - w * 0.5f, spot.Foot.y), new Vector2(spot.Foot.x + w * 0.5f, spot.Foot.y + h));
            token.Image.rectTransform.localScale = new Vector3(spot.FaceLeft != sourceFacesLeft ? -1f : 1f, 1f, 1f);
            // the bench frames leave room above the lying cat for the raised bar: the cycle bar goes over the barbell
            var barY = spot.Clip == HideoutLayout.CatClip.Bench ? 0.8f : 1f;
            token.Progress.Rect.Place(new Vector2(0.5f, barY), new Vector2(0.5f, barY), new Vector2(-75, 4), new Vector2(75, 24));
            token.Image.color = token.BaseColor;

            // a sleeping cat breathes: a slow ping-pong squash from the feet
            var body = token.Image.rectTransform;
            body.DOKill();
            body.pivot = new Vector2(0.5f, 0f);
            body.localScale = new Vector3(body.localScale.x, 1f, 1f);
            if (spot.Clip == HideoutLayout.CatClip.Sleep)
                body.DOScaleY(1.045f, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(body.gameObject);
        }

        /// <summary>Clip for a hideout pose and whether its frames face left.</summary>
        private static (FlipbookClip Clip, bool FacesLeft) ClipFor(CatToken token, HideoutLayout.CatClip clip)
        {
            var art = token.Art;
            if (art == null)
                return (null, false);
            FlipbookClip Pose(int index) => art.HideoutPose(index) is Sprite sprite ? new FlipbookClip { frames = new[] { sprite } } : null;
            return clip switch
            {
                HideoutLayout.CatClip.Bench when art.bench?.frames.Length > 0 => (art.bench, false),
                HideoutLayout.CatClip.Punch when art.canPunch?.frames.Length > 0 => (art.canPunch, false),
                HideoutLayout.CatClip.Rope when art.rope?.frames.Length > 0 => (art.rope, false),
                HideoutLayout.CatClip.Sleep => (Pose(1) ?? Pose(0), false),
                HideoutLayout.CatClip.Walk or HideoutLayout.CatClip.Bench => (Pose(3) ?? Pose(0), false),
                HideoutLayout.CatClip.Punch => (Pose(3) ?? Pose(0), false),
                _ when art.hideoutIdle?.frames.Length > 0 => (art.hideoutIdle, false),
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
            // the jab and the hook are the originally drawn frames 2 and 5
            var punch = token.Art?.canPunch;
            if (_can == null || token.Spot.Clip != HideoutLayout.CatClip.Punch || punch == null || (frame != punch.KeyFrame(2) && frame != punch.KeyFrame(5)))
                return;
            _can.DOComplete();
            // hit from the right: the can swings to the left
            _can.DOPunchRotation(new Vector3(0, 0, frame == punch.KeyFrame(5) ? -16f : -10f), 0.55f, 4, 0.6f).SetLink(_can.gameObject);
            var star = Ui.Skin?.fxStar;
            if (star == null)
                return;
            var flash = Ui.Panel(_can, "Hit", Color.white);
            flash.sprite = star;
            flash.preserveAspect = true;
            flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = new Vector2(0.75f, 0.2f); // hit from the right
            flash.rectTransform.sizeDelta = new Vector2(110, 110);
            flash.rectTransform.localScale = Vector3.one * 0.5f;
            DOTween.Sequence()
                .Append(flash.rectTransform.DOScale(1f, 0.08f))
                .Append(flash.DOFade(0f, 0.18f))
                .OnComplete(() => UnityEngine.Object.Destroy(flash.gameObject))
                .SetLink(flash.gameObject);
        }

        /// <summary>
        /// "Ушёл за едой" / "роется в мусоре": round portrait, caption and cycle bar shown at the street gap or over the
        /// dumpster while the cat is out of sight.
        /// </summary>
        private (RectTransform Root, UiBar Bar, TMP_Text Caption) AwayBadge(CatId id, Sprite portrait)
        {
            var root = Ui.Rect(_frame, "Away_" + id);
            var picture = new RoundPicture(root, "Portrait", CatColors.Backdrop(id), 0.8f);
            picture.Root.Place(new Vector2(0.15f, 0.3f), new Vector2(0.85f, 1f));
            picture.Root.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            picture.SetPicture(portrait);
            var caption = Ui.Label(root, Loc.T("hideout.away"), 22, TextAnchor.MiddleCenter, Palette.Paper);
            caption.rectTransform.Place(new Vector2(-0.4f, 0.12f), new Vector2(1.4f, 0.3f));
            var bar = Ui.Bar(root, "AwayBar", Palette.Progress);
            bar.Rect.Place(new Vector2(0.05f, 0f), new Vector2(0.95f, 0.11f));
            root.gameObject.SetActive(false);
            var button = Ui.Panel(root, "Tap", new Color(0, 0, 0, 0), raycast: true);
            button.rectTransform.Fill();
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => _onCatTapped(id));
            return (root, bar, caption);
        }

        /// <summary>A bit of junk (bolt, cap, can) pops up out of the dumpster and tumbles down over its front.</summary>
        private void SpillJunk()
        {
            var sprite = Ui.Skin?.Icon("junk");
            if (sprite == null)
                return;
            var junk = Ui.Panel(_frame, "Junk", Color.white);
            junk.sprite = sprite;
            junk.preserveAspect = true;
            junk.transform.SetSiblingIndex(_dumpster.GetSiblingIndex() + 1);
            var rt = junk.rectTransform;
            var from = new Vector2(Mathf.Lerp(_dumpster.anchorMin.x, _dumpster.anchorMax.x, UnityEngine.Random.Range(0.25f, 0.7f)), _dumpster.anchorMax.y - 0.03f);
            rt.anchorMin = rt.anchorMax = from;
            rt.sizeDelta = Vector2.one * UnityEngine.Random.Range(40f, 60f);
            var shift = HideoutLayout.DumpsterShift;
            rt.anchoredPosition = shift;
            var dx = shift.x + UnityEngine.Random.Range(-90f, 50f);
            var duration = UnityEngine.Random.Range(0.6f, 0.8f);
            DOTween.Sequence()
                .Append(rt.DOAnchorPosX(dx, duration).SetEase(Ease.Linear))
                .Join(rt.DOAnchorPosY(shift.y + 140f, duration * 0.45f).SetEase(Ease.OutQuad))
                .Insert(duration * 0.45f, rt.DOAnchorPosY(shift.y - 90f, duration * 0.55f).SetEase(Ease.InQuad))
                .Join(rt.DORotate(new Vector3(0, 0, dx < 0 ? 360f : -360f), duration, RotateMode.FastBeyond360))
                .Append(junk.DOFade(0f, 0.2f))
                .OnComplete(() => UnityEngine.Object.Destroy(junk.gameObject))
                .SetLink(junk.gameObject);
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

            var target = token.Spot.Hidden ? token.Away.Root : token.Root;
            target.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
            var text = e.Kind == ActivityKind.Train
                ? $"+{e.Output} {Texts.AttributeGenitive(e.TrainedAttribute).ToLowerInvariant()}"
                : Texts.Output(_session.Config.GetActivity(e.ActivityId), e.Output);
            FloatingText.Spawn(_root, target, text, Texts.OutcomeIcon(_session.Config.GetActivity(e.ActivityId)));
        }

        private sealed class CatToken
        {
            public RectTransform Root { get; }
            public TMP_Text Bubble { get; }
            public UiBar Progress { get; }
            public CharacterArt Art;
            public Image Image;
            public UiFlipbook Flipbook;
            public (RectTransform Root, UiBar Bar, TMP_Text Caption) Away;
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
        public static void Spawn(RectTransform parent, RectTransform target, string text, string icon = null)
        {
            var label = Ui.Label(parent, text, 34, TextAnchor.MiddleCenter, Palette.Primary);
            var sprite = icon != null ? Ui.Skin?.Icon(icon) : null;
            if (sprite != null)
            {
                var picture = Ui.Panel(label.transform, "Icon", Color.white);
                picture.sprite = sprite;
                picture.preserveAspect = true;
                picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                picture.rectTransform.sizeDelta = new Vector2(64, 64);
                picture.rectTransform.anchoredPosition = new Vector2(-label.preferredWidth * 0.5f - 38f, 0);
                picture.DOFade(0f, 1.1f).SetEase(Ease.InQuad).SetLink(picture.gameObject);
            }
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
