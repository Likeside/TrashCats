using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    public enum CardStyle
    {
        /// <summary>Light cardboard sheet with paper inside (sheets, modals).</summary>
        Paper,
        /// <summary>Wide cardboard strip (toasts, rows).</summary>
        Strip,
        /// <summary>Dark painted tin (HUD, navigation).</summary>
        Tin,
    }

    /// <summary>
    /// Code-built UI primitives (uGUI + TextMeshPro). With a <see cref="UiSkin"/> they use the comic cardboard art,
    /// without it they stay graybox; screens only talk to the small widget classes below.
    /// </summary>
    public static class Ui
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

        /// <summary>Text this large or larger uses the heading font.</summary>
        private const int HeadingSize = 44;

        public static UiSkin Skin { get; set; }

        /// <summary>Any button made by <see cref="Button"/> was pressed (click sound).</summary>
        public static event Action ButtonClicked;

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

        public static TextMeshProUGUI Label(Transform parent, string text, int size, TextAnchor align, Color color, bool? heading = null)
        {
            var rt = Rect(parent, "Label");
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = heading ?? size >= HeadingSize ? Skin?.headingFont : Skin?.bodyFont;
            if (font != null)
                label.font = font;
            label.text = text;
            label.fontSize = size;
            label.Align(align);
            // comic lettering: white text with an ink outline everywhere (dark text only without a skin)
            label.color = Skin != null && color == Palette.Ink ? Palette.Paper : color;
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            rt.Fill();
            if (Skin != null)
                label.Outlined();
            return label;
        }

        public static TMP_Text Align(this TMP_Text label, TextAnchor anchor)
        {
            label.alignment = anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center,
            };
            return label;
        }

        /// <summary>Ink outline for text drawn straight over pictures.</summary>
        public static TMP_Text Outlined(this TMP_Text label)
        {
            var bold = label.fontSize >= 32f;
            var material = label.font == Skin?.headingFont ? (bold ? Skin.headingOutlineBold : null) ?? Skin.headingOutline
                : label.font == Skin?.bodyFont ? (bold ? Skin.bodyOutlineBold : null) ?? Skin.bodyOutline
                : null;
            if (material != null)
                label.fontSharedMaterial = material;
            else
                label.outlineWidth = 0.2f;
            return label;
        }

        /// <summary>Background card: 9-sliced cardboard art with a skin, a flat colour without.</summary>
        public static Image Card(Transform parent, string name, CardStyle style, bool raycast = false)
        {
            var tin = style == CardStyle.Tin;
            var sprite = tin ? Skin?.tin : Skin?.paper;
            var image = Panel(parent, name, sprite != null ? Color.white : tin ? Palette.PanelDark : Palette.Panel, raycast);
            Sliced(image, sprite);
            if (sprite != null)
                image.pixelsPerUnitMultiplier = tin ? Skin.tinBorderScale : Skin.paperBorderScale * (style == CardStyle.Strip ? 1.6f : 1f);
            return image;
        }

        /// <summary>
        /// Big event icon sitting on the top edge of a card (victory, new cat, skill...), so every outcome is
        /// recognisable at a glance. Change it with <see cref="SetBadge"/>; hidden when the key has no art.
        /// </summary>
        public static Image Badge(Transform card, float size = 210f)
        {
            var image = Panel(card, "Badge", Color.white);
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(0, 10);
            image.gameObject.SetActive(false);
            return image;
        }

        public static void SetBadge(Image badge, string key)
        {
            var sprite = key != null ? Skin?.Icon(key) : null;
            badge.sprite = sprite;
            badge.gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                badge.rectTransform.DOKill();
                badge.rectTransform.localScale = Vector3.one * 0.6f;
                badge.rectTransform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetLink(badge.gameObject);
            }
        }

        /// <summary>Icon from the skin, or null when there is no art for the key.</summary>
        public static Image Icon(Transform parent, string key)
        {
            var sprite = Skin?.Icon(key);
            return sprite == null ? null : Picture(parent, "Icon_" + key, sprite);
        }

        private static void Sliced(Image image, Sprite sprite, float fitHeight = 0f)
        {
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (fitHeight > 0f)
                image.gameObject.AddComponent<SliceFit>().heightShare = fitHeight;
        }

        public static UiButton Button(Transform parent, string name, string label, Color color, Action onClick, int fontSize = 36)
        {
            var image = Panel(parent, name, color, raycast: true);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = Skin != null ? new Color(0.78f, 0.78f, 0.78f, 0.85f) : new Color(1f, 1f, 1f, 0.35f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;
            var text = Label(image.transform, label, fontSize, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            text.rectTransform.Fill(Skin?.button != null ? 22 : 12);
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(18, fontSize * 0.6f);
            button.onClick.AddListener(() => ButtonClicked?.Invoke());
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            var result = new UiButton(button, image, text);
            result.SetColor(color);
            return result;
        }

        /// <summary>Horizontal 0..1 slider: dark track, yellow fill, round handle.</summary>
        public static Slider Slider(Transform parent, string name, float value, Action<float> onChange)
        {
            var track = Panel(parent, name, Skin?.barFrame != null ? Color.white : Palette.BarBack, raycast: true);
            Sliced(track, Skin?.barFrame, 1.15f);
            var fillArea = Rect(track.transform, "FillArea").Fill(14);
            var fill = Panel(fillArea, "Fill", Palette.Primary);
            fill.rectTransform.Fill();
            var handleArea = Rect(track.transform, "HandleArea").Fill(14);
            var handle = Panel(handleArea, "Handle", Palette.Paper, raycast: true);
            handle.sprite = Skin?.circle;
            handle.rectTransform.sizeDelta = new Vector2(70, 40);
            var outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Ink;
            outline.effectDistance = new Vector2(3, -3);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            if (onChange != null)
                slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>Picture filling the parent like CSS "cover" (keeps aspect, crops the overflow).</summary>
        public static Image Cover(Transform parent, string name, Sprite sprite, Color tint)
        {
            var mask = Rect(parent, name).Fill();
            mask.gameObject.AddComponent<RectMask2D>();
            var image = Panel(mask, "Picture", tint);
            image.sprite = sprite;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            return image;
        }

        /// <summary>Picture fitted inside a rect keeping its aspect.</summary>
        public static Image Picture(Transform parent, string name, Sprite sprite)
        {
            var image = Panel(parent, name, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.Fill();
            return image;
        }

        /// <param name="badge">Icon drawn just left of the bar (energy, satiety, hp, grit); leave room for it.</param>
        public static UiBar Bar(Transform parent, string name, Color fill, string badge = null)
        {
            var frame = Skin?.barFrame;
            var back = Panel(parent, name, frame != null ? Color.white : Palette.BarBack);
            Sliced(back, frame, 1.15f);
            // the fill lives inside the frame's dark channel: inset by the frame's borders, scaled like the image
            var channel = Rect(back.transform, "Channel").Fill();
            if (frame != null)
                back.gameObject.AddComponent<ChannelFit>().Init(channel, frame);
            var icon = badge != null ? Skin?.Icon(badge == "grit" ? "paw" : badge) : null;
            if (icon != null)
            {
                var picture = Picture(back.transform, "Badge", icon);
                picture.rectTransform.pivot = new Vector2(1f, 0.5f);
                picture.rectTransform.Place(new Vector2(0, -0.2f), new Vector2(0, 1.2f));
                var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = icon.rect.width / icon.rect.height;
                picture.rectTransform.anchoredPosition = new Vector2(-4, 0);
            }
            var fillImage = Panel(channel, "Fill", fill);
            fillImage.rectTransform.Fill();
            var label = Label(back.transform, "", 24, TextAnchor.MiddleCenter, frame != null ? Palette.Paper : Palette.Ink, heading: frame != null);
            if (frame != null)
                label.Outlined();
            return new UiBar(back.rectTransform, fillImage.rectTransform, label);
        }
    }

    /// <summary>Keeps a bar's fill channel inside the frame's dark inner area when the frame's borders rescale.</summary>
    public sealed class ChannelFit : MonoBehaviour
    {
        private RectTransform _channel;
        private Image _image;
        private Vector4 _inner;

        public void Init(RectTransform channel, Sprite frame)
        {
            _channel = channel;
            _image = GetComponent<Image>();
            // the dark channel starts a little inside the 9-slice border on the badge side, at ~40% of the others
            var b = frame.border;
            _inner = new Vector4(b.x > b.z * 2f ? b.x * 0.97f : b.x * 0.55f, b.y * 0.55f, b.z * 0.55f, b.w * 0.55f);
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_channel == null)
                return;
            var m = Mathf.Max(0.01f, _image.pixelsPerUnitMultiplier);
            _channel.offsetMin = new Vector2(_inner.x / m, _inner.y / m);
            _channel.offsetMax = new Vector2(-_inner.z / m, -_inner.w / m);
        }
    }

    public sealed class UiButton
    {
        public Button Button { get; }
        public Image Image { get; }
        public TMP_Text Text { get; }
        public RectTransform Rect => Image.rectTransform;

        public UiButton(Button button, Image image, TMP_Text text)
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

        /// <summary>Palette yellow and blue use their hand-coloured art, any other colour tints the neutral button.</summary>
        public void SetColor(Color color)
        {
            var skin = Ui.Skin;
            if (skin == null || skin.button == null)
            {
                Image.color = color;
                return;
            }

            var sprite = Same(color, Palette.Primary) && skin.buttonPrimary != null ? skin.buttonPrimary
                : Same(color, Palette.Secondary) && skin.buttonSecondary != null ? skin.buttonSecondary
                : skin.button;
            if (Image.sprite != sprite)
            {
                Image.sprite = sprite;
                Image.type = Image.Type.Sliced;
                if (Image.GetComponent<SliceFit>() == null)
                    Image.gameObject.AddComponent<SliceFit>().heightShare = 1.05f;
            }

            Image.color = sprite == skin.button ? color : new Color(1f, 1f, 1f, color.a);
            // readable text on any fill: ink on light colours, paper on dark ones
            Text.color = Palette.Paper;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
    }

    public sealed class UiBar
    {
        private readonly RectTransform _fill;

        public RectTransform Rect { get; }
        public TMP_Text Label { get; }

        public UiBar(RectTransform rect, RectTransform fill, TMP_Text label)
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
