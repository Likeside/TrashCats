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
        private RectTransform _wheelArcs;
        private RectTransform _can;
        private GameObject _ropeProp;
        private Vector2 _streetGap;
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
                    // one still frame of the wheel; while a cat runs, comic speed arcs circle along the rim (the drawn
                    // spin frames do not loop seamlessly)
                    var picture = Ui.Picture(box.transform, "Prop", wheel.idle.frames[0]);
                    var arcs = _art?.GetSprite("fx_wheel_arcs");
                    if (arcs != null)
                        _wheelArcs = WheelArcs(picture.rectTransform, wheel.idle.frames[0], arcs);
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
                        // a wrought-iron bracket sticks out of the wall; its hook holds the top of the can's rope
                        var bracketSprite = _art?.GetSprite("prop_bracket");
                        if (bracketSprite != null)
                        {
                            var hook = new Vector2((min.x + max.x) * 0.5f, max.y - 0.006f);
                            const float width = 0.17f;
                            var height = width * HideoutLayout.PictureAspect * bracketSprite.rect.height / bracketSprite.rect.width;
                            var bracket = Ui.Panel(_frame, "Bracket", Color.white);
                            bracket.sprite = bracketSprite;
                            bracket.rectTransform.Place(new Vector2(hook.x - 0.892f * width, hook.y), new Vector2(hook.x + 0.108f * width, hook.y + height));
                            _depthItems.Add((bracket.transform, min.y + 0.0001f));
                        }
                    }
                }
                if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Agility)
                    _ropeProp = box.transform.Find("Prop")?.gameObject;
                if (scavenge)
                    _streetGap = new Vector2((min.x + max.x) * 0.5f, min.y);
                _depthItems.Add((box.transform, min.y));
                if (activity.Kind == ActivityKind.Sleep || (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Toughness))
                    _propBase[activity.Kind == ActivityKind.Sleep ? ActivityKind.Sleep : ActivityKind.Train] = min.y;

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
            ((RectTransform)text.transform.parent).sizeDelta = new Vector2(size.x + 40f, size.y + 14f);
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
            var wheelBusy = false;
            var ropeBusy = false;
            var away = 0;
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
                    // one cat fits in the wheel / on the bed; others wait beside it
                    if (spot.OverProp && index > 0)
                        spot = new HideoutLayout.CatSpot(spot.Foot + new Vector2(-0.12f * index, -0.12f), HideoutLayout.CatClip.Sit);
                    wheelBusy |= spot.Clip == HideoutLayout.CatClip.Run;
                    ropeBusy |= spot.Clip == HideoutLayout.CatClip.Rope;
                    if (spot.Clip == HideoutLayout.CatClip.Run && token.Art != null && HideoutLayout.RunFit(token.Art.viewKey) is var (runFoot, runHeight))
                    {
                        // placed and sized inside the wheel's rect, so the cat always fits the drum
                        var (wMin, wMax) = HideoutLayout.Wheel;
                        var size = wMax - wMin;
                        spot = new HideoutLayout.CatSpot(wMin + Vector2.Scale(runFoot, size), spot.Clip, spot.FaceLeft, spot.OverProp, height: runHeight * size.y);
                    }
                }
                else
                    spot = HideoutLayout.Idle(token.Art?.viewKey, idle++);

                // out on the street: a portrait with the cycle bar at the street gap instead of the cat
                token.Away.Root.gameObject.SetActive(spot.Hidden);
                if (spot.Hidden)
                {
                    var top = _streetGap.y - 0.02f - 0.15f * away++;
                    token.Away.Root.Place(new Vector2(_streetGap.x - 0.08f, top - 0.14f), new Vector2(_streetGap.x + 0.06f, top));
                }

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

            _ropeProp?.SetActive(!ropeBusy);
            if (_wheelArcs != null && _wheelArcs.gameObject.activeSelf != wheelBusy)
            {
                _wheelArcs.gameObject.SetActive(wheelBusy);
                _wheelArcs.DOKill();
                if (wheelBusy)
                    _wheelArcs.DOLocalRotate(new Vector3(0, 0, 360f), 1.6f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1).SetLink(_wheelArcs.gameObject);
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

            var h = spot.Height > 0f ? spot.Height : size.y * HideoutLayout.HeightPerPixel * HideoutLayout.DepthScale(spot.Foot.y) * spot.Scale;
            var w = h * size.x / size.y / HideoutLayout.PictureAspect;
            token.Root.Place(new Vector2(spot.Foot.x - w * 0.5f, spot.Foot.y), new Vector2(spot.Foot.x + w * 0.5f, spot.Foot.y + h));
            token.Image.rectTransform.localScale = new Vector3(spot.FaceLeft != sourceFacesLeft ? -1f : 1f, 1f, 1f);
            token.Image.color = token.BaseColor;

            // a sleeping cat breathes: a slow ping-pong squash from the feet
            var body = token.Image.rectTransform;
            body.DOKill();
            body.pivot = new Vector2(0.5f, 0f);
            body.localScale = new Vector3(body.localScale.x, 1f, 1f);
            if (spot.Clip == HideoutLayout.CatClip.Sleep)
                body.DOScaleY(1.045f, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(body.gameObject);
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
                HideoutLayout.CatClip.Rope when art.rope?.frames.Length > 0 => (art.rope, false),
                HideoutLayout.CatClip.Dig when art.dig?.frames.Length > 0 => (art.dig, false),
                HideoutLayout.CatClip.Sleep => (Pose(1) ?? Pose(0), false),
                HideoutLayout.CatClip.Walk or HideoutLayout.CatClip.Run => (Pose(3) ?? Pose(0), false),
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
            if (token.Spot.Clip == HideoutLayout.CatClip.Dig && (frame == 1 || frame == 5))
                ThrowJunk(token.Root);
            // the jab and the hook are the originally drawn frames 2 and 5
            var punch = token.Art?.canPunch;
            if (_can == null || token.Spot.Clip != HideoutLayout.CatClip.Punch || punch == null || (frame != punch.KeyFrame(2) && frame != punch.KeyFrame(5)))
                return;
            _can.DOComplete();
            _can.DOPunchRotation(new Vector3(0, 0, frame == punch.KeyFrame(5) ? 16f : 10f), 0.55f, 4, 0.6f).SetLink(_can.gameObject);
            var star = Ui.Skin?.fxStar;
            if (star == null)
                return;
            var flash = Ui.Panel(_can, "Hit", Color.white);
            flash.sprite = star;
            flash.preserveAspect = true;
            flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = new Vector2(0.25f, 0.2f); // hit from the left
            flash.rectTransform.sizeDelta = new Vector2(110, 110);
            flash.rectTransform.localScale = Vector3.one * 0.5f;
            DOTween.Sequence()
                .Append(flash.rectTransform.DOScale(1f, 0.08f))
                .Append(flash.DOFade(0f, 0.18f))
                .OnComplete(() => UnityEngine.Object.Destroy(flash.gameObject))
                .SetLink(flash.gameObject);
        }

        /// <summary>"Ушёл за едой": round portrait, caption and cycle bar shown at the street gap while the cat is away.</summary>
        private (RectTransform Root, UiBar Bar) AwayBadge(CatId id, Sprite portrait)
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
            return (root, bar);
        }

        /// <summary>
        /// Speed arcs on the wheel's rim: a square that rotates inside a parent squashed to the rim's ellipse
        /// (measured on the wheel sprite: drum opening x 0.19..0.66, y 0.25..0.84 from the bottom).
        /// </summary>
        private static RectTransform WheelArcs(RectTransform picture, Sprite wheel, Sprite arcs)
        {
            var ellipse = Ui.Rect(picture, "SpinEllipse");
            ellipse.anchorMin = ellipse.anchorMax = new Vector2(0.425f, 0.545f);
            var w = wheel.rect.width * 0.47f * 1.18f;
            var h = wheel.rect.height * 0.59f * 1.12f;
            // sizes are in sprite pixels: the picture is fitted with preserveAspect, so scale with its rect
            var fit = ellipse.gameObject.AddComponent<SpinFit>();
            fit.Init(picture, wheel, w, h);
            var image = Ui.Panel(ellipse, "Arcs", Color.white);
            image.sprite = arcs;
            image.rectTransform.Fill();
            image.gameObject.SetActive(false);
            return image.rectTransform;
        }

        /// <summary>A bit of junk (bolt, cap, can) flies out of the dumpster in an arc while a cat digs in it.</summary>
        private void ThrowJunk(RectTransform cat)
        {
            var sprite = Ui.Skin?.Icon("junk");
            if (sprite == null)
                return;
            var junk = Ui.Panel(_frame, "Junk", Color.white);
            junk.sprite = sprite;
            junk.preserveAspect = true;
            var rt = junk.rectTransform;
            var from = new Vector2((cat.anchorMin.x + cat.anchorMax.x) * 0.5f, cat.anchorMin.y + 0.02f);
            rt.anchorMin = rt.anchorMax = from;
            rt.sizeDelta = new Vector2(70, 70);
            var dx = UnityEngine.Random.Range(60f, 160f);
            var duration = 0.7f;
            DOTween.Sequence()
                .Append(rt.DOAnchorPosX(dx, duration).SetEase(Ease.Linear))
                .Join(rt.DOAnchorPosY(140f, duration * 0.45f).SetEase(Ease.OutQuad))
                .Insert(duration * 0.45f, rt.DOAnchorPosY(-40f, duration * 0.55f).SetEase(Ease.InQuad))
                .Join(rt.DORotate(new Vector3(0, 0, -360f), duration, RotateMode.FastBeyond360))
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
            public (RectTransform Root, UiBar Bar) Away;
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
