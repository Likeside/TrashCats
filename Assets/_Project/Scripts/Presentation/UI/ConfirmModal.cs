using System;
using TMPro;
using UnityEngine;

namespace TrashCats.Presentation.UI
{
    /// <summary>Yes / no question on a paper card (quit the game, reset progress).</summary>
    public sealed class ConfirmModal
    {
        private readonly OverlayStack _overlays;
        private readonly GameObject _root;
        private readonly TMP_Text _title;
        private readonly TMP_Text _body;
        private readonly UiButton _yes;
        private Action _onYes;

        public ConfirmModal(RectTransform parent, OverlayStack overlays)
        {
            _overlays = overlays;
            var dim = Ui.Panel(parent, "Confirm", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;
            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(860, 560));
            _title = Ui.Label(card.transform, "", 48, TextAnchor.UpperCenter, Palette.Ink);
            _title.rectTransform.Fill(50);
            _body = Ui.Label(card.transform, "", 34, TextAnchor.MiddleCenter, Palette.Ink);
            _body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 160), new Vector2(-50, -130));
            var no = Ui.Button(card.transform, "Confirm_No", Loc.T("confirm.no"), Palette.Secondary, () => _overlays.Close(_root), 38);
            no.Rect.Place(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(50, 40), new Vector2(-15, 140));
            _yes = Ui.Button(card.transform, "Confirm_Yes", "", Palette.Warning, () =>
            {
                _overlays.Close(_root);
                _onYes?.Invoke();
            }, 38);
            _yes.Rect.Place(new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(15, 40), new Vector2(-50, 140));
            _root.SetActive(false);
        }

        public void Ask(string title, string body, string yes, Action onYes)
        {
            _title.text = title;
            _body.text = body;
            _yes.Text.text = yes;
            _onYes = onYes;
            _overlays.Push(_root);
        }
    }
}
