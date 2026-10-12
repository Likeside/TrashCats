using System;
using System.Collections.Generic;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>Bottom panel with one card per gang slot: status, cycle progress, needs and quick feeding.</summary>
    public sealed class CatPanelView
    {
        private const int Slots = 3;

        private readonly GameSession _session;
        private readonly Action<CatId> _onCardTapped;
        private readonly Action<CommandResult> _onResult;
        private readonly Card[] _cards = new Card[Slots];

        public CatPanelView(GameSession session, RectTransform root, Action<CatId> onCardTapped, Action<CommandResult> onResult)
        {
            _session = session;
            _onCardTapped = onCardTapped;
            _onResult = onResult;

            Ui.Panel(root, "PanelBack", Palette.PanelDark).rectTransform.Fill();
            for (var i = 0; i < Slots; i++)
            {
                var slot = Ui.Rect(root, "Slot" + i);
                slot.Place(new Vector2(i / (float)Slots, 0f), new Vector2((i + 1) / (float)Slots, 1f), new Vector2(10, 10), new Vector2(-10, -10));
                _cards[i] = new Card(slot, this, i);
            }

            Refresh();
        }

        /// <summary>Text and interactivity; call on domain events.</summary>
        public void Refresh()
        {
            var cats = _session.World.Cats;
            for (var i = 0; i < Slots; i++)
                _cards[i].Bind(i < cats.Count ? cats[i] : null);
        }

        /// <summary>Progress bars; call every frame.</summary>
        public void Tick()
        {
            var world = _session.World;
            foreach (var card in _cards)
                card.Tick(world.GameTimeMs);
        }

        private sealed class Card
        {
            private readonly CatPanelView _owner;
            private readonly GameObject _content;
            private readonly GameObject _locked;
            private readonly Image _header;
            private readonly Text _name;
            private readonly Text _status;
            private readonly UiBar _progress;
            private readonly UiBar _energy;
            private readonly UiBar _satiety;
            private readonly UiButton _feed;
            private IReadOnlyCat _cat;

            public Card(RectTransform slot, CatPanelView owner, int index)
            {
                _owner = owner;

                var background = Ui.Panel(slot, "Card", Palette.Panel, raycast: true);
                background.rectTransform.Fill();
                background.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (_cat != null) _owner._onCardTapped(_cat.Id);
                });

                _content = Ui.Rect(background.transform, "Content").Fill().gameObject;
                var content = _content.transform;

                _header = Ui.Panel(content, "Header", Color.gray);
                _header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), Vector2.zero);
                _name = Ui.Label(_header.transform, "", 34, TextAnchor.MiddleCenter, Palette.Paper);

                _status = Ui.Label(content, "", 26, TextAnchor.MiddleCenter, Palette.Ink);
                _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -120), new Vector2(-8, -62));

                _progress = Ui.Bar(content, "Progress", Palette.Progress);
                _progress.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -142), new Vector2(-16, -126));

                _energy = Ui.Bar(content, "Energy", Palette.Energy);
                _energy.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -196), new Vector2(-16, -156));

                _satiety = Ui.Bar(content, "Satiety", Palette.Satiety);
                _satiety.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -246), new Vector2(-16, -206));

                _feed = Ui.Button(content, "Feed", "Покормить", Palette.Primary, () =>
                {
                    if (_cat != null) _owner._onResult(_owner._session.FeedCat(_cat.Id));
                }, 28);
                _feed.Rect.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 12), new Vector2(-16, 66));

                _locked = Ui.Label(background.transform, "Ещё не\nв банде", 32, TextAnchor.MiddleCenter, Palette.Disabled).gameObject;
            }

            public void Bind(IReadOnlyCat cat)
            {
                _cat = cat;
                _content.SetActive(cat != null);
                _locked.SetActive(cat == null);
                if (cat == null)
                    return;

                var session = _owner._session;
                var color = CatColors.For(cat.Id);
                _header.color = color;
                _name.color = CatColors.TextOn(color);
                _name.text = Texts.CatName(cat.Id);
                _status.text = cat.Activity.IsWorking
                    ? Texts.Doing(session.Config.GetActivity(cat.Activity.ActivityId))
                    : Texts.IdleStatus(cat.Activity.IdleReason);

                var max = (float)session.Config.Needs.Max;
                _energy.Set(cat.Energy / max, $"Энергия {cat.Energy}");
                _satiety.Set(cat.Satiety / max, $"Сытость {cat.Satiety}");

                var canFeed = session.CanFeed(cat.Id);
                _feed.Interactable = canFeed == CommandError.None;
                _feed.Text.text = canFeed == CommandError.None ? $"Покормить (−{session.FoodToFeed(cat.Id)})" : Texts.Error(canFeed);
            }

            public void Tick(long nowMs)
            {
                if (_cat == null)
                    return;
                _progress.Rect.gameObject.SetActive(_cat.Activity.IsWorking);
                if (_cat.Activity.IsWorking)
                    _progress.Set(ActivitySystem.GetCycleProgress(_cat, nowMs));
            }
        }
    }
}
