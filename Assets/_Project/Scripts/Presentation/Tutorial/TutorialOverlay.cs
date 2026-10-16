using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Tutorial
{
    /// <summary>
    /// Guided-chain overlay (Docs/GDD/UI_UX.md §11): points at the widget the step asks for with the cat-paw pointer and
    /// a ring, and the cat the step is about says one short phrase in its own voice (its portrait by the bubble);
    /// "Пропустить обучение" ends the chain. The first steps also dim everything else and block taps outside the target;
    /// soft steps only point. Widgets are found by object name, deepest first (e.g. the activity button if the sheet is
    /// open, else the cat in the yard, else its card). Of the waiting steps the first one whose target is on screen is
    /// shown; the overlay hides while another modal covers the target or the target is missing / disabled.
    /// </summary>
    public sealed class TutorialOverlay : IDisposable
    {
        private const float Padding = 14f;
        private const float LookupInterval = 0.15f;

        private readonly TutorialService _service;
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly RectTransform _root;
        private readonly RectTransform[] _blockers = new RectTransform[4];
        private readonly Image[] _blockerImages = new Image[4];
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.74f);
        private readonly RoundPicture _speaker;
        private readonly Func<CatId, Sprite> _portrait;
        private readonly RectTransform _frame;
        private readonly RectTransform[] _ring = new RectTransform[4];
        private const float RingWidth = 7f;
        private readonly RectTransform _pointer;
        private readonly RectTransform _bubble;
        private readonly TMP_Text _text;

        /// <summary>Name of the highlighted widget, or null when nothing is highlighted (dev autoplay).</summary>
        public string TargetName => _root.gameObject.activeSelf && _target != null ? _target.name : null;

        private IReadOnlyList<TutorialStep> _steps = Array.Empty<TutorialStep>();
        private TutorialStep _step;
        private RectTransform _target;
        private float _nextLookup;

        public TutorialOverlay(TutorialService service, GameSession session, OverlayStack overlays, Transform parent, Func<CatId, Sprite> portrait = null)
        {
            _service = service;
            _session = session;
            _overlays = overlays;
            _portrait = portrait;

            // own canvas above every screen, sheet and modal
            var canvas = Ui.CreateCanvas("Tutorial", 50);
            canvas.transform.SetParent(parent, false);
            _root = Ui.Rect(canvas.transform, "TutorialOverlay").Fill();

            for (var i = 0; i < 4; i++)
            {
                _blockerImages[i] = Ui.Panel(_root, "Block" + i, Dim, raycast: true);
                _blockers[i] = _blockerImages[i].rectTransform;
            }

            // a bright yellow ring around the hole; the target itself stays untouched and tappable
            _frame = Ui.Rect(_root, "Frame");
            for (var i = 0; i < 4; i++)
                _ring[i] = Ui.Panel(_frame, "Ring" + i, Palette.Primary).rectTransform;

            var pointerSprite = Ui.Skin?.pointer;
            var pointer = Ui.Panel(_root, "Pointer", pointerSprite != null ? Color.white : Palette.Primary);
            pointer.sprite = pointerSprite;
            pointer.preserveAspect = true;
            _pointer = pointer.rectTransform;
            _pointer.sizeDelta = new Vector2(150, 220);
            _pointer.anchorMin = _pointer.anchorMax = new Vector2(0.5f, 0.5f);

            var bubble = Ui.Card(_root, "Bubble", CardStyle.Paper, raycast: true);
            _bubble = bubble.rectTransform;
            _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
            _bubble.sizeDelta = new Vector2(940, 330);
            // the cat that talks, by the left edge of the bubble
            _speaker = new RoundPicture(_bubble, "Speaker", Palette.Disabled, 0.8f);
            _speaker.Root.anchorMin = _speaker.Root.anchorMax = new Vector2(0f, 1f);
            _speaker.Root.pivot = new Vector2(0.5f, 0.5f);
            _speaker.Root.sizeDelta = new Vector2(190, 190);
            _speaker.Root.anchoredPosition = new Vector2(70, -40);
            _text = Ui.Label(_bubble, "", 36, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            _text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(185, 96), new Vector2(-40, -30));
            _text.enableAutoSizing = true;
            _text.fontSizeMin = 26;
            _text.fontSizeMax = 36;
            var skip = Ui.Button(_bubble, "Tutorial_Skip", Loc.T("tutorial_overlay.01"), Palette.Disabled, () => _service.Skip(), 28);
            skip.Rect.Place(new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 26), new Vector2(0, 96));

            _root.gameObject.SetActive(false);
            _service.StepsChanged += OnStepsChanged;
            OnStepsChanged(_service.Steps);
        }

        public void Dispose() => _service.StepsChanged -= OnStepsChanged;

        private void OnStepsChanged(IReadOnlyList<TutorialStep> steps)
        {
            _steps = steps;
            _nextLookup = 0f;
            if (steps.Count == 0)
                Show(null);
        }

        /// <summary>Switches the shown step: its phrase, its speaker, hard or soft.</summary>
        private void Show(TutorialStep step)
        {
            if (Equals(step, _step))
                return;
            _step = step;
            _pointer.DOKill();
            _pointerBob = false;
            if (step == null)
            {
                _target = null;
                _root.gameObject.SetActive(false);
                return;
            }

            _text.text = Phrase(step);
            _speaker.Backdrop.color = CatColors.Backdrop(step.Cat);
            _speaker.SetPicture(_portrait?.Invoke(step.Cat));
            _speaker.Root.gameObject.SetActive(_portrait?.Invoke(step.Cat) != null);
            foreach (var blocker in _blockerImages)
            {
                blocker.color = step.Soft ? new Color(0, 0, 0, 0) : Dim;
                blocker.raycastTarget = !step.Soft;
            }
        }

        private bool _pointerBob;

        /// <summary>Call every frame (after the UI has laid out).</summary>
        public void LateTick()
        {
            if (_steps.Count == 0)
                return;

            if (Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + LookupInterval;
                // the first waiting step whose widget is on screen now
                TutorialStep shown = null;
                RectTransform target = null;
                foreach (var step in _steps)
                {
                    target = FindTarget(step);
                    if (target != null && IsReachable(target))
                    {
                        shown = step;
                        break;
                    }
                    target = null;
                }
                Show(shown);
                _target = target;
            }
            if (_step == null)
                return;

            var visible = _target != null && IsReachable(_target);
            if (_root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
            if (visible)
                Layout(_target);
        }

        private RectTransform FindTarget(TutorialStep step)
        {
            foreach (var name in Targets(step))
            {
                var go = GameObject.Find(name);
                if (go != null && go.activeInHierarchy && go.transform is RectTransform rt)
                    return rt;
            }

            return null;
        }

        /// <summary>The target is on top (inside the topmost sheet/modal, if any) and can be pressed.</summary>
        private bool IsReachable(RectTransform target)
        {
            var top = _overlays.Top;
            if (top != null && !target.IsChildOf(top.transform))
                return false;
            var button = target.GetComponent<Button>();
            return button == null || button.interactable;
        }

        private void Layout(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, RectTransformUtility.WorldToScreenPoint(camera, corners[0]), null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, RectTransformUtility.WorldToScreenPoint(camera, corners[2]), null, out var max);
            var area = _root.rect;
            min = min - area.min - new Vector2(Padding, Padding);
            max = max - area.min + new Vector2(Padding, Padding);
            var size = area.size;

            // four blockers around the hole (positions relative to the bottom-left corner)
            Set(_blockers[0], new Vector2(0, max.y), size);                 // top
            Set(_blockers[1], Vector2.zero, new Vector2(size.x, min.y));    // bottom
            Set(_blockers[2], new Vector2(0, min.y), new Vector2(min.x, max.y));        // left
            Set(_blockers[3], new Vector2(max.x, min.y), new Vector2(size.x, max.y));   // right
            Set(_frame, min, max);
            var w = max.x - min.x;
            var h = max.y - min.y;
            Set(_ring[0], new Vector2(-RingWidth, h), new Vector2(w + RingWidth, h + RingWidth));
            Set(_ring[1], new Vector2(-RingWidth, -RingWidth), new Vector2(w + RingWidth, 0));
            Set(_ring[2], new Vector2(-RingWidth, 0), new Vector2(0, h));
            Set(_ring[3], new Vector2(w, 0), new Vector2(w + RingWidth, h));

            // pointer above the hole pointing down, or below it pointing up when the hole is high on the screen
            var centre = (min + max) * 0.5f;
            var high = centre.y > size.y * 0.7f;
            var pointerPos = new Vector2(centre.x, high ? min.y - 110 : max.y + 110) - size * 0.5f;
            _pointer.localScale = new Vector3(1, high ? -1 : 1, 1);
            if (!_pointerBob)
            {
                _pointerBob = true;
                _pointer.anchoredPosition = pointerPos;
                _pointer.DOKill();
                DOTween.To(() => _bob, v => _bob = v, 1f, 0.45f).From(0f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                    .SetTarget(_pointer).SetLink(_pointer.gameObject);
            }
            _pointer.anchoredPosition = pointerPos + new Vector2(0, (high ? -1 : 1) * 24f * _bob);

            // the phrase sits in the half of the screen away from the target
            _bubble.anchoredPosition = new Vector2(0, centre.y < size.y * 0.5f ? size.y * 0.22f : -size.y * 0.25f);
        }

        private float _bob;

        private static void Set(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = min;
            rt.sizeDelta = new Vector2(Mathf.Max(0, max.x - min.x), Mathf.Max(0, max.y - min.y));
        }

        private IEnumerable<string> Targets(TutorialStep step)
        {
            var config = _session.Config;
            var cats = _session.World.Cats;
            var index = Math.Max(0, cats.ToList().FindIndex(c => c.Id.Equals(step.Cat)));
            var card = "CatCard" + index;
            // the cat right in the yard first (tapping it opens the same sheet as its card)
            var yard = "Cat_" + step.Cat;
            string Activity(ActivityKind kind, AttributeType? attribute = null) =>
                "Activity_" + config.Activities.First(a => a.Kind == kind && (attribute == null || a.TrainedAttribute == attribute)).Id;

            switch (step.Id)
            {
                case TutorialService.Scavenge: return new[] { Activity(ActivityKind.Scavenge), yard, card };
                case TutorialService.Bar: return new[] { "Progress_" + step.Cat };
                case TutorialService.Feed: return new[] { "Sheet_Feed", "Feed" + index };
                case TutorialService.Train: return new[] { Activity(ActivityKind.Train, AttributeType.Strength), yard, card };
                case TutorialService.Equip: return new[] { "Skill_" + step.Skill, "Sheet_Details", card };
                case TutorialService.Sleep: return new[] { Activity(ActivityKind.Sleep), yard, card };
                case TutorialService.Fight: return new[] { "Prep_Fight", "Node_" + step.Territory, "Tab_Map" };
                case TutorialService.Upgrade: return new[] { "Upgrade_Buy", "Station_" + step.Upgrade };
                case TutorialService.Order: return new[] { "Prep_Queue" };
                default: return Array.Empty<string>();
            }
        }

        private static string Phrase(TutorialStep step)
        {
            var name = Texts.CatName(step.Cat);
            switch (step.Id)
            {
                case TutorialService.Scavenge: return Loc.T("tutorial_overlay.02", name);
                case TutorialService.Bar: return Loc.T("tutorial_overlay.15");
                case TutorialService.Feed: return Loc.T("tutorial_overlay.03");
                case TutorialService.Train: return Loc.T("tutorial_overlay.04");
                case TutorialService.Equip: return Loc.T("tutorial_overlay.05");
                case TutorialService.Sleep: return Loc.T("tutorial_overlay.06", name);
                case TutorialService.Fight: return step.Territory is TerritoryId t ? Loc.T("tutorial_overlay.07", Texts.TerritoryName(t)) : Loc.T("tutorial_overlay.08");
                case TutorialService.Upgrade: return Loc.T("tutorial_overlay.16");
                case TutorialService.Order: return Loc.T("tutorial_overlay.17");
                default: return "";
            }
        }

        public static string HintIcon(string id) => id switch
        {
            TutorialService.HintJunk => "junk",
            TutorialService.HintRelay => "paw",
            TutorialService.HintSlots => "skill",
            TutorialService.HintTired => "energy",
            TutorialService.HintOffline => "clock",
            _ => null,
        };

        /// <summary>One-shot contextual hint: (title, body) for the message queue, or null.</summary>
        public static (string Title, string Body)? Hint(string id) => id switch
        {
            TutorialService.HintJunk => (Loc.T("tutorial_overlay.09"), Loc.T("tutorial_overlay.10")),
            TutorialService.HintRelay => (Loc.T("tutorial_overlay.11"), Loc.T("tutorial_overlay.12")),
            TutorialService.HintSlots => (Loc.T("tutorial_overlay.13"), Loc.T("tutorial_overlay.14")),
            TutorialService.HintTired => (Loc.T("tutorial_overlay.18"), Loc.T("tutorial_overlay.19")),
            TutorialService.HintOffline => (Loc.T("tutorial_overlay.20"), Loc.T("tutorial_overlay.21")),
            _ => null,
        };
    }
}
