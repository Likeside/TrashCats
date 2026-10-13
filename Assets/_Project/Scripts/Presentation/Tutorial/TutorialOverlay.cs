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

namespace TrashCats.Presentation.Tutorial
{
    /// <summary>
    /// Guided-chain overlay (Docs/GDD/UI_UX.md §11): dims everything except the widget the step asks for, blocks taps
    /// outside it, points at it with the cat-paw pointer and says one short phrase; "Пропустить обучение" ends the chain.
    /// Widgets are found by object name, deepest first (e.g. the activity button if the sheet is open, else the cat
    /// card). The overlay waits (hidden) while another modal covers the target or the target is missing / disabled.
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
        private readonly RectTransform _frame;
        private readonly RectTransform[] _ring = new RectTransform[4];
        private const float RingWidth = 7f;
        private readonly RectTransform _pointer;
        private readonly RectTransform _bubble;
        private readonly TMP_Text _text;

        /// <summary>Name of the highlighted widget, or null when nothing is highlighted (dev autoplay).</summary>
        public string TargetName => _root.gameObject.activeSelf && _target != null ? _target.name : null;

        private TutorialStep _step;
        private RectTransform _target;
        private float _nextLookup;

        public TutorialOverlay(TutorialService service, GameSession session, OverlayStack overlays, Transform parent)
        {
            _service = service;
            _session = session;
            _overlays = overlays;

            // own canvas above every screen, sheet and modal
            var canvas = Ui.CreateCanvas("Tutorial", 50);
            canvas.transform.SetParent(parent, false);
            _root = Ui.Rect(canvas.transform, "TutorialOverlay").Fill();

            var dim = new Color(0f, 0f, 0f, 0.74f);
            for (var i = 0; i < 4; i++)
                _blockers[i] = Ui.Panel(_root, "Block" + i, dim, raycast: true).rectTransform;

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
            _bubble.sizeDelta = new Vector2(920, 300);
            _text = Ui.Label(_bubble, "", 40, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            _text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 90), new Vector2(-50, -40));
            var skip = Ui.Button(_bubble, "Tutorial_Skip", "Пропустить обучение", Palette.Disabled, () => _service.Skip(), 28);
            skip.Rect.Place(new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 26), new Vector2(0, 96));

            _root.gameObject.SetActive(false);
            _service.StepChanged += OnStepChanged;
            OnStepChanged(_service.Current);
        }

        public void Dispose() => _service.StepChanged -= OnStepChanged;

        private void OnStepChanged(TutorialStep step)
        {
            _step = step;
            _target = null;
            _nextLookup = 0f;
            if (step != null)
                _text.text = Phrase(step);
            _pointer.DOKill();
            _pointerBob = false;
            if (step == null)
                _root.gameObject.SetActive(false);
        }

        private bool _pointerBob;

        /// <summary>Call every frame (after the UI has laid out).</summary>
        public void LateTick()
        {
            if (_step == null)
                return;

            if (Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + LookupInterval;
                _target = FindTarget(_step);
            }

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
            string Activity(ActivityKind kind, AttributeType? attribute = null) =>
                "Activity_" + config.Activities.First(a => a.Kind == kind && (attribute == null || a.TrainedAttribute == attribute)).Id;

            switch (step.Id)
            {
                case TutorialService.Scavenge: return new[] { Activity(ActivityKind.Scavenge), card };
                case TutorialService.Feed: return new[] { "Sheet_Feed", "Feed" + index };
                case TutorialService.Train: return new[] { Activity(ActivityKind.Train, AttributeType.Strength), card };
                case TutorialService.Equip: return new[] { "Skill_" + step.Skill, "Sheet_Details", card };
                case TutorialService.Sleep: return new[] { Activity(ActivityKind.Sleep), card };
                case TutorialService.Fight: return new[] { "Prep_Fight", "Node_" + step.Territory, "Tab_Map" };
                default: return Array.Empty<string>();
            }
        }

        private static string Phrase(TutorialStep step)
        {
            var name = Texts.CatName(step.Cat);
            switch (step.Id)
            {
                case TutorialService.Scavenge: return $"{name} голоден. Отправь его за едой!";
                case TutorialService.Feed: return "Еда есть! Покорми кота — сытые работают быстрее.";
                case TutorialService.Train: return "Пора качаться! Тренируй Силу на банке.";
                case TutorialService.Equip: return "Новое умение! Возьми его в слот — в бою кот будет его применять.";
                case TutorialService.Sleep: return $"{name} устал. Уложи его спать.";
                case TutorialService.Fight: return step.Territory is TerritoryId t ? $"Пора проучить соседей! Нападай: {Texts.TerritoryName(t)}." : "Пора в бой!";
                default: return "";
            }
        }

        /// <summary>One-shot contextual hint: (title, body) for the message queue, or null.</summary>
        public static (string Title, string Body)? Hint(string id) => id switch
        {
            TutorialService.HintJunk => ("Хлам", "Хлам тратится на улучшения.\nНажми на предмет во дворе — банку, мышь, колесо, бак или коробки."),
            TutorialService.HintRelay => ("Порядок важен", "Бой — эстафета: победитель остаётся на ринге и бьётся со следующим.\nНажимай на котов, чтобы выбрать порядок."),
            TutorialService.HintSlots => ("Слотов только три", "Умений больше, чем слотов.\nВыбирай под врага в «Умения и статы»."),
            _ => null,
        };
    }
}
