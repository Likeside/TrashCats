using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>One modal message at a time, FIFO; can be paused (e.g. during a battle replay).</summary>
    public sealed class MessageQueue
    {
        private readonly OverlayStack _overlays;
        private readonly Queue<(string Title, string Body, string Icon)> _queue = new Queue<(string, string, string)>();
        private readonly Image _badge;
        private readonly GameObject _root;
        private readonly TMP_Text _title;
        private readonly TMP_Text _body;
        private bool _showing;
        private bool _paused;

        public MessageQueue(RectTransform parent, OverlayStack overlays)
        {
            _overlays = overlays;
            var dim = Ui.Panel(parent, "Message", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;
            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(880, 720));
            _title = Ui.Label(card.transform, "", 52, TextAnchor.UpperCenter, Palette.Ink);
            _title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 50), new Vector2(-50, -100));
            _badge = Ui.Badge(card.transform);
            _body = Ui.Label(card.transform, "", 36, TextAnchor.MiddleCenter, Palette.Ink);
            _body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 170), new Vector2(-50, -130));
            var ok = Ui.Button(card.transform, "Message_Ok", Loc.T("message_queue.01"), Palette.Primary, () => _overlays.Close(_root), 40);
            ok.Rect.Place(new Vector2(0.2f, 0), new Vector2(0.8f, 0), new Vector2(0, 40), new Vector2(0, 150));
            _root.SetActive(false);
        }

        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                TryShowNext();
            }
        }

        /// <param name="icon">Skin icon key shown on top of the card (newcat, flag, skill, final, levelup...).</param>
        public void Enqueue(string title, string body, string icon = null)
        {
            _queue.Enqueue((title, body, icon));
            TryShowNext();
        }

        private void TryShowNext()
        {
            if (_showing || _paused || _queue.Count == 0)
                return;
            var (title, body, icon) = _queue.Dequeue();
            _title.text = title;
            Ui.SetBadge(_badge, icon);
            _body.text = body;
            _showing = true;
            _overlays.Push(_root, () =>
            {
                _showing = false;
                TryShowNext();
            });
        }
    }
}
