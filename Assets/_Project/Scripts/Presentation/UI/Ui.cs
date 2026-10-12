using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Code-built graybox UI primitives (uGUI). Real art and prefabs replace them in M6; screens only talk to the
    /// small widget classes below, so the swap does not touch presenter logic.
    /// </summary>
    public static class Ui
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

        private static Font _font;

        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Anchors in parent-relative 0..1 coordinates plus pixel offsets.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float padding = 0) =>
            rt.Place(Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));

        /// <summary>Fixed-size rect centred on a parent-relative anchor point.</summary>
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align, Color color)
        {
            var rt = Rect(parent, "Label");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            rt.Fill();
            return label;
        }

        public static UiButton Button(Transform parent, string name, string label, Color color, Action onClick, int fontSize = 36)
        {
            var image = Panel(parent, name, color, raycast: true);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;
            var text = Label(image.transform, label, fontSize, TextAnchor.MiddleCenter, Palette.Ink);
            text.rectTransform.Fill(12);
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            return new UiButton(button, image, text);
        }

        public static UiBar Bar(Transform parent, string name, Color fill)
        {
            var back = Panel(parent, name, Palette.BarBack);
            var fillImage = Panel(back.transform, "Fill", fill);
            fillImage.rectTransform.Fill();
            var label = Label(back.transform, "", 24, TextAnchor.MiddleCenter, Palette.Ink);
            return new UiBar(back.rectTransform, fillImage.rectTransform, label);
        }
    }

    public sealed class UiButton
    {
        public Button Button { get; }
        public Image Image { get; }
        public Text Text { get; }
        public RectTransform Rect => Image.rectTransform;

        public UiButton(Button button, Image image, Text text)
        {
            Button = button;
            Image = image;
            Text = text;
        }

        public bool Interactable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }
    }

    public sealed class UiBar
    {
        private readonly RectTransform _fill;

        public RectTransform Rect { get; }
        public Text Label { get; }

        public UiBar(RectTransform rect, RectTransform fill, Text label)
        {
            Rect = rect;
            _fill = fill;
            Label = label;
        }

        public void Set(float value01, string label = null)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            if (label != null)
                Label.text = label;
        }
    }

    /// <summary>Graybox colours, taken from the Art Bible where it defines them.</summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("#13171E");
        public static readonly Color Scene = Hex("#2B2A2E");
        public static readonly Color Panel = Hex("#E9D3AE");
        public static readonly Color PanelDark = Hex("#2F3738");
        public static readonly Color Station = Hex("#73462B");
        public static readonly Color Primary = Hex("#F5BD37");
        public static readonly Color Secondary = Hex("#4A92C6");
        public static readonly Color Disabled = Hex("#BBAE9C");
        public static readonly Color Energy = Hex("#FBC435");
        public static readonly Color Satiety = Hex("#D66428");
        public static readonly Color Progress = Hex("#4A92C6");
        public static readonly Color BarBack = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color Ink = Hex("#1A1A1A");
        public static readonly Color Paper = Hex("#FFF6E6");
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color Warning = Hex("#D9402B");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
