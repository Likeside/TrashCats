using System;
using TrashCats.App;
using TrashCats.Presentation.Audio;
using TrashCats.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>Settings (M5-08): sound and music volume, progress reset (with a confirmation), audio credits.</summary>
    public sealed class SettingsPanel
    {
        private readonly OverlayStack _overlays;
        private readonly GameObject _root;

        public SettingsPanel(RectTransform parent, OverlayStack overlays, SettingsSave settings, GameAudio audio, Action saveSettings,
            ConfirmModal confirm, Action resetProgress)
        {
            _overlays = overlays;
            var dim = Ui.Panel(parent, "SettingsPanel", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _overlays.Close(_root));
            _root = dim.gameObject;

            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(900, 1080));
            var title = Ui.Label(card.transform, Loc.T("settings.title"), 52, TextAnchor.UpperCenter, Palette.Ink);
            title.rectTransform.Fill(50);

            Row(card.transform, 0, Loc.T("settings.sounds"), settings.SfxVolume, v =>
            {
                settings.SfxVolume = v;
                audio.SfxVolume = v;
                saveSettings();
            });
            Row(card.transform, 1, Loc.T("settings.music"), settings.MusicVolume, v =>
            {
                settings.MusicVolume = v;
                audio.MusicVolume = v;
                saveSettings();
            });

            var reset = Ui.Button(card.transform, "Settings_Reset", Loc.T("settings.reset"), Palette.Warning, () =>
                confirm.Ask(Loc.T("settings.reset_title"), Loc.T("settings.reset_body"), Loc.T("settings.reset_yes"), resetProgress), 34);
            reset.Rect.Place(new Vector2(0.15f, 1), new Vector2(0.85f, 1), new Vector2(0, -560), new Vector2(0, -460));

            var credits = Ui.Label(card.transform, Loc.T("settings.credits"), 24, TextAnchor.MiddleCenter, Palette.Disabled);
            credits.color = new Color(0.35f, 0.3f, 0.25f);
            credits.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 170), new Vector2(-60, 330));

            var close = Ui.Button(card.transform, "Settings_Close", Loc.T("settings.close"), Palette.Primary, () => _overlays.Close(_root), 38);
            close.Rect.Place(new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 50), new Vector2(0, 150));
            _root.SetActive(false);
        }

        public void Open() => _overlays.Push(_root);

        private static void Row(Transform card, int index, string caption, float value, Action<float> onChange)
        {
            var top = -170 - index * 140;
            var label = Ui.Label(card, caption, 36, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            label.rectTransform.Place(new Vector2(0, 1), new Vector2(0.4f, 1), new Vector2(70, top - 70), new Vector2(0, top));
            var slider = Ui.Slider(card, "Slider_" + index, value, onChange);
            slider.GetComponent<RectTransform>().Place(new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, top - 62), new Vector2(-70, top - 8));
        }
    }
}
