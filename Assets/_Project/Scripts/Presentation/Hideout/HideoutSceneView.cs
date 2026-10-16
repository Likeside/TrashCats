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
    /// activity, show a cycle progress bar and floating results (why an idle cat waits is on its card); nearer things
    /// draw on top.
    /// Station captions sort with their props; cycle bars live in a layer over the whole scene; both
    /// follow their cat or prop every frame and are kept inside the visible part of the scene. Without art the props and cats are graybox
    /// boxes on the same spots.
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
        private readonly RectTransform _barLayer;
        /// <summary>Station plate of every activity: the busy cats' cycle bars stand over it.</summary>
        private readonly Dictionary<string, RectTransform> _plates = new Dictionary<string, RectTransform>();
        private readonly List<(RectTransform Plate, RectTransform Box, Vector2 At)> _captions = new List<(RectTransform, RectTransform, Vector2)>();
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly Vector3[] _sceneCorners = new Vector3[4];
        private RectTransform _can;
        private GameObject _ropeProp;
        private GameObject _barbell;
        private RectTransform _dumpster;
        private int _rummaging;
        private float _junkTimer;
        private int _scavenging;
        private float _foodTimer;
        private static readonly string[] FoodBits = { "fx_food_bone", "fx_food_stew", "fx_food_head" };
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
            _barLayer = Ui.Rect(_frame, "CycleBars").Fill();
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
            _root.GetWorldCorners(_sceneCorners);
            // captions first: the cycle bars and away badges step out of their way
            foreach (var (plate, box, at) in _captions)
            {
                plate.position = Point(box, at) + Vector3.down * (4f * Scale);
                KeepInScene(plate, true);
            }
            foreach (var cat in world.Cats)
            {
                if (!_tokens.TryGetValue(cat.Id, out var token))
                    continue;
                var working = cat.Activity.IsWorking;
                var visible = token.Root.gameObject.activeInHierarchy;
                HeldProps(token, visible);
                NextBenchVariant(token);
                var bar = working && (visible || token.Spot.Hidden) && token.Plate != null;
                if (token.Progress.Rect.gameObject.activeSelf != bar)
                    token.Progress.Rect.gameObject.SetActive(bar);
                if (working)
                {
                    var progress = ActivitySystem.GetCycleProgress(cat, world.GameTimeMs);
                    token.Progress.Set(progress);
                }
                if (bar)
                {
                    // a little over its station's plate; more cats at one station stand side by side
                    token.Plate.GetWorldCorners(_corners);
                    var top = new Vector3((_corners[1].x + _corners[2].x) * 0.5f, Mathf.Max(_corners[1].y, _corners[2].y), 0f);
                    token.Progress.Rect.position = top + new Vector3(token.PlateSlot * (ProgressWidth + 12f), BarGap) * Scale;
                    KeepInScene(token.Progress.Rect, true);
                    // the portrait of a cat out of sight sits a little over its bar, both right over the plate
                    if (token.Away.Root.gameObject.activeSelf)
                    {
                        token.Progress.Rect.GetWorldCorners(_corners);
                        token.Away.Root.position = new Vector3((_corners[1].x + _corners[2].x) * 0.5f, _corners[1].y + BarGap * Scale, 0f);
                        var x = token.Away.Root.position.x;
                        KeepInScene(token.Away.Root, true);
                        token.Progress.Rect.position += Vector3.right * (token.Away.Root.position.x - x);
                        // its caption is wider than the portrait: only the words step in from the screen edge
                        KeepCaptionInScene(token.Away.Caption);
                    }
                }
            }


            // junk tumbles out of the dumpster while someone rummages in it
            if (_rummaging > 0 && _dumpster != null && (_junkTimer -= Time.deltaTime) <= 0f)
            {
                _junkTimer = UnityEngine.Random.Range(0.25f, 0.5f);
                SpillJunk();
            }

            // leftovers fly in from off screen by the exit while someone is out getting food
            if (_scavenging > 0 && (_foodTimer -= Time.deltaTime) <= 0f)
            {
                _foodTimer = UnityEngine.Random.Range(0.5f, 0.9f);
                ThrowFood();
            }
        }

        private float Scale => _root.lossyScale.x;

        /// <summary>World position of a point given in 0..1 of a (possibly rotated) rect.</summary>
        private Vector3 Point(RectTransform rect, Vector2 at)
        {
            rect.GetWorldCorners(_corners);
            // unclamped: a point may lie outside the rect (the barbell's plate on the wall above the mat)
            return Vector3.LerpUnclamped(Vector3.LerpUnclamped(_corners[0], _corners[3], at.x), Vector3.LerpUnclamped(_corners[1], _corners[2], at.x), at.y);
        }

        /// <summary>Moves an overlay item back inside the visible scene (under the top bar), with a margin.</summary>
        private void KeepInScene(RectTransform item, bool horizontal, float extraSide = 0f)
        {
            item.GetWorldCorners(_corners);
            var margin = 10f * Scale;
            var side = margin + extraSide * Scale;
            var top = _sceneCorners[1].y - HideoutScreen.HudHeight * Scale;
            float dx = 0f, dy = 0f;
            var left = Mathf.Min(_corners[0].x, _corners[2].x);
            var right = Mathf.Max(_corners[0].x, _corners[2].x);
            if (horizontal)
            {
                if (left < _sceneCorners[0].x + side) dx = _sceneCorners[0].x + side - left;
                else if (right > _sceneCorners[2].x - side) dx = _sceneCorners[2].x - side - right;
            }
            if (_corners[0].y < _sceneCorners[0].y + margin) dy = _sceneCorners[0].y + margin - _corners[0].y;
            else if (_corners[1].y > top - margin) dy = top - margin - _corners[1].y;
            item.position += new Vector3(dx, dy, 0f);
        }

        /// <summary>Shifts a badge's caption sideways so its words (not its wide box) stay inside the visible scene.</summary>
        private void KeepCaptionInScene(TMP_Text caption)
        {
            var rt = caption.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            var half = caption.GetPreferredValues(caption.text).x * 0.5f * Scale;
            var center = rt.position.x;
            var margin = 10f * Scale;
            var dx = Mathf.Max(0f, _sceneCorners[0].x + margin - (center - half)) + Mathf.Min(0f, _sceneCorners[2].x - margin - (center + half));
            rt.position += Vector3.right * dx;
        }

        /// <summary>Barbell length, share of the bench frame's width.</summary>
        public const float HeldBarbellWidth = 0.76f;
        /// <summary>Rope thickness in sprite pixels.</summary>
        private const float RopePixels = 5f;

        /// <summary>
        /// The skipping rope and the barbell are drawn by the game between the hands marked on every frame
        /// (FlipbookClip.handles): the rope swings with the clip's phase and passes behind the cat on the way up,
        /// the barbell lies in the fists. Hidden for other clips.
        /// </summary>
        private void HeldProps(CatToken token, bool visible)
        {
            var clip = token.Flipbook.Clip;
            var hands = clip?.handles;
            var hasHands = visible && hands != null && hands.Length > 0 && clip.frames.Length > 0;
            var h = hasHands ? hands[Mathf.Clamp(token.Flipbook.Frame, 0, hands.Length - 1)] : default;
            var image = token.Image.rectTransform;

            var rope = hasHands && token.Spot.Clip == HideoutLayout.CatClip.Rope;
            if (rope && token.Rope == null)
            {
                var go = new GameObject("Rope", typeof(RectTransform), typeof(CanvasRenderer), typeof(RopeGraphic));
                go.transform.SetParent(token.Root, false);
                token.Rope = go.GetComponent<RopeGraphic>();
                token.Rope.raycastTarget = false;
                token.Rope.rectTransform.Fill();
            }
            if (token.Rope != null)
            {
                token.Rope.gameObject.SetActive(rope);
                if (rope)
                {
                    var rt = token.Rope.rectTransform;
                    rt.pivot = image.pivot;
                    rt.localScale = image.localScale;
                    token.Rope.Width = RopePixels * image.rect.height / clip.frames[0].rect.height;
                    token.Rope.Set(new Vector2(h.x, h.y), new Vector2(h.z, h.w), token.Flipbook.Phase);
                    // behind the body on the way up, in front on the way down
                    if (token.Rope.Behind) rt.SetSiblingIndex(image.GetSiblingIndex());
                    else rt.SetSiblingIndex(image.GetSiblingIndex() + 1);
                }
            }

            var bench = hasHands && token.Spot.Clip == HideoutLayout.CatClip.Bench;
            if (bench && token.HeldBarbell == null && _art?.GetSprite(HideoutLayout.Barbell.Sprite) is Sprite barbellSprite)
            {
                token.HeldBarbell = Ui.Panel(image, "HeldBarbell", Color.white);
                token.HeldBarbell.sprite = barbellSprite;
                var rt = token.HeldBarbell.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            if (token.HeldBarbell != null)
            {
                token.HeldBarbell.gameObject.SetActive(bench);
                if (bench)
                {
                    var size = image.rect.size;
                    var left = new Vector2(h.x * size.x, h.y * size.y);
                    var right = new Vector2(h.z * size.x, h.w * size.y);
                    var rt = token.HeldBarbell.rectTransform;
                    var sprite = token.HeldBarbell.sprite;
                    var width = HeldBarbellWidth * size.x;
                    rt.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
                    rt.anchoredPosition = (left + right) * 0.5f;
                    var d = right - left;
                    rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
            }
        }

        /// <summary>
        /// The bench press switches to another of the cat's push-up variants after every rep (the ping-pong clip is back
        /// at the bottom, where all variants start), so the reps do not repeat one drawing.
        /// </summary>
        private void NextBenchVariant(CatToken token)
        {
            var variants = token.Art?.benchVariants;
            if (token.Spot.Clip != HideoutLayout.CatClip.Bench || variants == null || variants.Length < 2 || token.Flipbook.Clip is not { pingPong: true })
            {
                token.BenchPhase = 0f;
                return;
            }
            var phase = token.Flipbook.Phase;
            if (phase < token.BenchPhase - 0.5f)
            {
                token.BenchVariant = (token.BenchVariant + UnityEngine.Random.Range(1, variants.Length)) % variants.Length;
                Show(token);
                phase = 0f;
            }
            token.BenchPhase = phase;
        }

        private const float ProgressWidth = 89f; // 96 - 20% + 15%
        private const float BarGap = 8f;

        /// <summary>
        /// Moves an item off any station caption it would overlap (the caption stays readable): under it (a cycle bar,
        /// closer to the cat's head) or over it.
        /// </summary>
        private void AvoidCaptions(RectTransform item, bool below)
        {
            var gap = 4f * Scale;
            foreach (var (plate, _, _) in _captions)
            {
                item.GetWorldCorners(_corners);
                var (iMin, iMax) = (_corners[0], _corners[2]);
                plate.GetWorldCorners(_corners);
                var (pMin, pMax) = (_corners[0], _corners[2]);
                if (iMax.x <= pMin.x || iMin.x >= pMax.x || iMax.y <= pMin.y || iMin.y >= pMax.y)
                    continue;
                item.position += Vector3.up * (below ? pMin.y - gap - iMax.y : pMax.y + gap - iMin.y);
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
                var (_, upgrade) = StationInfo(activity);
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
                if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Strength && box.transform.Find("Prop") is Transform mat
                    && _art?.GetSprite(HideoutLayout.Barbell.Sprite) is Sprite barbellSprite)
                {
                    // the barbell rests on the brick stands; a child of the mat picture, so it is mirrored and tilted with it
                    var barbell = Ui.Panel(mat, "Barbell", Color.white);
                    barbell.sprite = barbellSprite;
                    var (rest, size, tilt, under) = (HideoutLayout.Barbell.Seat, HideoutLayout.Barbell.Size, HideoutLayout.Barbell.Rotation,
                        HideoutLayout.Barbell.BarUnderside);
                    var corner = new Vector2(rest.x - size.x * 0.5f, rest.y - size.y * under);
                    barbell.rectTransform.Place(corner, corner + size);
                    barbell.rectTransform.pivot = new Vector2(0.5f, under);
                    barbell.rectTransform.localEulerAngles = new Vector3(0, 0, tilt);
                    _barbell = barbell.gameObject;
                }
                _depthItems.Add((box.transform, place.Depth));

                // the mat's plate hangs on the wall over the barbell (the EXIT sign stands in front of the mat)
                var bench = activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Strength;
                var text = Plate(box.rectTransform, Texts.OutcomeIcon(activity), bench ? HideoutLayout.Bench.Depth + 0.2f : place.Depth,
                    HideoutLayout.PlateAt(activity));
                _plates[activity.Id.Value] = (RectTransform)text.transform.parent;
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

        private const float PlateIcon = 46f;
        private const float PlatePad = 16f;
        private const float PlateHeight = 80f;

        /// <summary>
        /// Station plate: a small dark rectangle under the prop's base with the icon of what the station trains or brings
        /// and its upgrade level (no words). It is drawn in the depth order right in front of its prop and follows the
        /// prop (see Tick).
        /// </summary>
        private TMP_Text Plate(RectTransform box, string icon, float depth, Vector2 at)
        {
            var plate = Ui.Panel(_frame, "Caption_" + box.name, Ui.Skin?.barFrame != null ? Color.white : new Color(0, 0, 0, 0.55f));
            plate.sprite = Ui.Skin?.barFrame;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 2.6f;
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            _captions.Add((rt, box, at));
            // just in front of its prop: a cat nearer to the camera than the prop draws over the caption
            _depthItems.Add((rt, depth - 0.0005f));
            var picture = Ui.Icon(plate.transform, icon);
            if (picture != null)
            {
                picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                picture.rectTransform.pivot = new Vector2(0f, 0.5f);
                picture.rectTransform.sizeDelta = new Vector2(PlateIcon, PlateIcon);
                picture.rectTransform.anchoredPosition = new Vector2(PlatePad, 0f);
            }
            var text = Ui.Label(plate.transform, "", 34, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(PlatePad + PlateIcon + 6f, 0f), new Vector2(-PlatePad, 0f));
            FitPlate(text);
            return text;
        }

        /// <summary>Sizes a station plate to its icon and level (just the icon when there is no level).</summary>
        private static void FitPlate(TMP_Text text)
        {
            // nearly square: the icon sits well inside the frame
            var width = string.IsNullOrEmpty(text.text) ? 0f : text.GetPreferredValues(text.text).x + 6f;
            ((RectTransform)text.transform.parent).sizeDelta = new Vector2(Mathf.Max(PlateHeight, PlatePad * 2f + PlateIcon + width), PlateHeight);
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
            // the level; a green "+" when the upgrade is affordable now
            text.text = Loc.T("hideout_scene_view.02", _session.World.GetUpgradeLevel(id), CanAfford(id) ? " <color=#8BE36B>+</color>" : "");
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

            // a small cycle bar over the busy cat's head
            var progress = Ui.Bar(_barLayer, "Progress_" + cat.Id, Palette.Progress);
            progress.Rect.anchorMin = progress.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            progress.Rect.pivot = new Vector2(0.5f, 0f);
            progress.Rect.sizeDelta = new Vector2(ProgressWidth, 18);
            progress.Rect.gameObject.SetActive(false);

            var token = new CatToken(rt, progress) { Art = hasArt ? catArt : null, Image = image, Flipbook = flipbook, BaseColor = hasArt ? Color.white : color };
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
            var benchBusy = false;
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
                    token.Plate = _plates.TryGetValue(activity.Id.Value, out var plate) ? plate : null;
                    token.PlateSlot = index;
                    spot = HideoutLayout.Spot(activity, index, token.Art?.viewKey);
                    // one cat fits on the mat / on the bed; others wait beside it (behind the mat)
                    var (bMin, bMax) = (HideoutLayout.Bench.Min, HideoutLayout.Bench.Max);
                    if (spot.OverProp && index > 0)
                        spot = spot.Clip == HideoutLayout.CatClip.Bench
                            ? new HideoutLayout.CatSpot(new Vector2((bMin.x + bMax.x) * 0.5f + 0.12f * (index - 1), bMax.y + 0.01f), HideoutLayout.CatClip.Sit)
                            : new HideoutLayout.CatSpot(spot.Foot + new Vector2(-0.12f * index, -0.12f), HideoutLayout.CatClip.Sit);
                    ropeBusy |= spot.Clip == HideoutLayout.CatClip.Rope;
                    benchBusy |= spot.Clip == HideoutLayout.CatClip.Bench;
                    if (spot.Clip == HideoutLayout.CatClip.Bench && token.Art != null && HideoutLayout.BenchFit(token.Art.viewKey) is var (benchFoot, benchHeight, _))
                    {
                        // placed and sized against the mat's rect, as in the layout scene
                        var size = bMax - bMin;
                        spot = new HideoutLayout.CatSpot(bMin + Vector2.Scale(benchFoot, size), spot.Clip, spot.FaceLeft, spot.OverProp, height: benchHeight * size.y, depth: spot.Depth);
                    }
                }
                else
                    spot = HideoutLayout.Idle(token.Art?.viewKey, idle++, alone: world.Cats.Count == 1);

                // out on the street or in the dumpster: a portrait with the cycle bar instead of the cat, right over the
                // dumpster or above it under the street gap (it fits under the HUD); more cats go to the left
                token.Away.Root.gameObject.SetActive(spot.Hidden);
                if (spot.Hidden)
                {
                    const float badge = 0.12f;
                    var rummage = kind == ActivityKind.Rummage;
                    var at = rummage
                        ? new Vector2(spot.Foot.x - 0.09f - 0.13f * rummaging++, spot.Foot.y + 0.002f)
                        : new Vector2(spot.Foot.x + 0.105f + 0.13f * away++, spot.Foot.y + 0.01f); // right of the EXIT sign
                    token.Away.Root.Place(at, at + new Vector2(badge, badge));
                    token.Away.Root.pivot = new Vector2(0.5f, 0f); // stood on its cycle bar in Tick
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

            // the cat has the rope: it stays on its nail as a glowing ghost, so the station is still easy to find and tap
            if (_ropeProp != null) Ghost(_ropeProp, ropeBusy, _art?.GetSprite(HideoutLayout.RopeProp.Sprite + "_outline"));
            // the barbell is in the pressing cat's paws
            _barbell?.SetActive(!benchBusy);
            _rummaging = rummaging;
            _scavenging = away;

            SortByDepth();
        }

        /// <summary>
        /// A prop in use (the cat has it): only its glowing outline (Tools/make_outline.py) stays, gently pulsing, so the
        /// station is still easy to find and tap; the picture comes back when it is free.
        /// </summary>
        private static void Ghost(GameObject prop, bool on, Sprite outlineSprite)
        {
            var image = prop.GetComponent<Image>();
            var glow = prop.transform.Find("Outline")?.GetComponent<Image>();
            if (glow == null && outlineSprite != null)
            {
                glow = Ui.Panel(prop.transform, "Outline", Color.white);
                glow.sprite = outlineSprite;
                glow.preserveAspect = true;
                glow.rectTransform.Fill();
                glow.gameObject.SetActive(false);
            }
            if (glow == null || glow.gameObject.activeSelf == on)
                return;
            glow.gameObject.SetActive(on);
            image.color = new Color(1f, 1f, 1f, on ? 0f : 1f);
            glow.DOKill();
            if (on)
            {
                glow.color = new Color(1f, 1f, 1f, 0.55f);
                glow.DOFade(1f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(glow.gameObject);
            }
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
                if (clip.frames.Length > 1 || clip.breathe > 0f) token.Flipbook.Play(clip);
                else
                {
                    token.Flipbook.Stop();
                    token.Image.sprite = clip.frames[0];
                }
            }

            var h = spot.Height > 0f ? spot.Height : size.y * HideoutLayout.HeightPerPixel * HideoutLayout.DepthScale(spot.Foot.y) * spot.Scale;
            // frames on a bigger canvas than the fit was made for (a drawn barbell sticks out) keep the cat's size
            if (spot.Clip == HideoutLayout.CatClip.Bench && spot.Height > 0f && clip != null && HideoutLayout.BenchFit(token.Art?.viewKey) is var (_, _, canvas))
                h *= size.y / canvas;
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

        /// <summary>Clip for a hideout pose and whether its frames face left.</summary>
        private static (FlipbookClip Clip, bool FacesLeft) ClipFor(CatToken token, HideoutLayout.CatClip clip)
        {
            var art = token.Art;
            if (art == null)
                return (null, false);
            FlipbookClip Pose(int index) => art.HideoutPose(index) is Sprite sprite ? new FlipbookClip { frames = new[] { sprite } } : null;
            return clip switch
            {
                HideoutLayout.CatClip.Bench when art.benchVariants?.Length > 0 => (art.benchVariants[token.BenchVariant % art.benchVariants.Length], false),
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
        /// "Ушёл за едой" / "роется в мусоре": round portrait with its caption over it, shown a little over the cat's
        /// cycle bar (which stands over the street's / the dumpster's plate) while the cat is out of sight.
        /// </summary>
        private (RectTransform Root, TMP_Text Caption) AwayBadge(CatId id, Sprite portrait)
        {
            var root = Ui.Rect(_frame, "Away_" + id);
            var picture = new RoundPicture(root, "Portrait", CatColors.Backdrop(id), 0.8f);
            picture.Root.Place(new Vector2(0.1f, 0f), new Vector2(0.9f, 0.8f));
            picture.Root.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            picture.SetPicture(portrait);
            var caption = Ui.Label(root, Loc.T("hideout.away"), 22, TextAnchor.MiddleCenter, Palette.Paper);
            caption.rectTransform.Place(new Vector2(-0.4f, 0.8f), new Vector2(1.4f, 1f));
            root.gameObject.SetActive(false);
            var button = Ui.Panel(root, "Tap", new Color(0, 0, 0, 0), raycast: true);
            button.rectTransform.Fill();
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => _onCatTapped(id));
            return (root, caption);
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

        /// <summary>
        /// A leftover (gnawed fish, half-eaten tin of stew, fish head) flies in from behind the left edge of the screen,
        /// tumbles past the EXIT sign and fades on the ground.
        /// </summary>
        private void ThrowFood()
        {
            var sprite = _art?.GetSprite(FoodBits[UnityEngine.Random.Range(0, FoodBits.Length)]);
            if (sprite == null)
                return;
            var food = Ui.Panel(_frame, "Leftover", Color.white);
            food.sprite = sprite;
            food.preserveAspect = true;
            food.transform.SetSiblingIndex(_barLayer.GetSiblingIndex());
            var rt = food.rectTransform;
            var exit = HideoutLayout.ExitSign;
            rt.anchorMin = rt.anchorMax = new Vector2(exit.Min.x - 0.08f, exit.Foot.y + 0.1f);
            rt.sizeDelta = Vector2.one * UnityEngine.Random.Range(60f, 84f);
            var dx = UnityEngine.Random.Range(170f, 320f);
            var duration = UnityEngine.Random.Range(0.7f, 0.95f);
            DOTween.Sequence()
                .Append(rt.DOAnchorPosX(dx, duration).SetEase(Ease.Linear))
                .Join(rt.DOAnchorPosY(150f, duration * 0.45f).SetEase(Ease.OutQuad))
                .Insert(duration * 0.45f, rt.DOAnchorPosY(-UnityEngine.Random.Range(70f, 110f), duration * 0.55f).SetEase(Ease.InQuad))
                .Join(rt.DORotate(new Vector3(0, 0, -360f), duration, RotateMode.FastBeyond360))
                .AppendInterval(0.25f)
                .Append(food.DOFade(0f, 0.3f))
                .OnComplete(() => UnityEngine.Object.Destroy(food.gameObject))
                .SetLink(food.gameObject);
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
            // over everything: the cycle bars (they step out of the captions' way)
            _barLayer.SetAsLastSibling();
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
            if (e.Kind == ActivityKind.Scavenge || e.Kind == ActivityKind.Rummage)
                Loot?.Invoke(target, e.Kind == ActivityKind.Scavenge ? "food" : "junk");
        }

        /// <summary>Food or junk was brought in at this spot (the HUD flies it into its counter).</summary>
        public event Action<RectTransform, string> Loot;

        private sealed class CatToken
        {
            public RectTransform Root { get; }
            public UiBar Progress { get; }
            public CharacterArt Art;
            public Image Image;
            public UiFlipbook Flipbook;
            /// <summary>Props the game draws in the cat's hands (the frames have only the hands).</summary>
            public RopeGraphic Rope;
            public Image HeldBarbell;
            /// <summary>Bench press: the push-up variant played and the clip phase last tick (a drop = a rep ended).</summary>
            public int BenchVariant;
            public float BenchPhase;
            public (RectTransform Root, TMP_Text Caption) Away;
            public Color BaseColor;
            public HideoutLayout.CatSpot Spot;
            /// <summary>Plate of the station the cat works at (its cycle bar stands over it) and its place among the cats there.</summary>
            public RectTransform Plate;
            public int PlateSlot;
            public float Depth;
            public bool Placed;

            public CatToken(RectTransform root, UiBar progress)
            {
                Root = root;
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
