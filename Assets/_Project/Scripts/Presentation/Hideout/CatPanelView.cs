using System;
using System.Collections.Generic;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
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
        private readonly Characters.CharacterViewFactory _art;

        public CatPanelView(GameSession session, RectTransform root, Action<CatId> onCardTapped, Action<CommandResult> onResult,
            Characters.CharacterViewFactory art = null)
        {
            _session = session;
            _onCardTapped = onCardTapped;
            _onResult = onResult;
            _art = art;

            Ui.Card(root, "PanelBack", CardStyle.Tin).rectTransform.Fill();
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
            private readonly Image _portrait;
            private readonly Image _backdrop;
            private readonly Image _card;
            private readonly TMP_Text _name;
            private TMP_Text _portraitName;
            private readonly TMP_Text _status;
            private readonly UiBar _progress;
            private readonly UiBar _energy;
            private readonly UiBar _satiety;
            private readonly UiButton _feed;
            private IReadOnlyCat _cat;

            public Card(RectTransform slot, CatPanelView owner, int index)
            {
                _owner = owner;

                var background = Ui.Card(slot, "CatCard" + index, CardStyle.Strip, raycast: true);
                background.rectTransform.Fill();
                background.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (_cat != null) _owner._onCardTapped(_cat.Id);
                });

                _content = Ui.Rect(background.transform, "Content").Fill(Ui.Skin != null ? 16 : 0).gameObject;
                var content = _content.transform;

                _header = Ui.Panel(content, "Header", Color.gray);
                if (Ui.Skin?.button != null)
                {
                    // the cat's name on a painted plate in the cat's colour
                    _header.sprite = Ui.Skin.button;
                    _header.type = Image.Type.Sliced;
                    _header.gameObject.AddComponent<SliceFit>();
                }
                _header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), Vector2.zero);
                _name = Ui.Label(_header.transform, "", 34, TextAnchor.MiddleCenter, Palette.Paper);

                // portrait on a coloured halftone backdrop, as in the concept; the name sits on its lower edge
                var border = Ui.Panel(content, "PortraitBorder", Palette.Ink);
                border.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), Vector2.zero);
                var window = Ui.Rect(border.transform, "Window").Fill(4);
                _backdrop = Ui.Panel(window, "Backdrop", Palette.PanelDark);
                _backdrop.rectTransform.Fill();
                // the concept's card picture (portrait already on its backdrop) covers the whole window when there is one
                window.gameObject.AddComponent<RectMask2D>();
                _card = Ui.Panel(window, "Card", Color.white);
                _card.rectTransform.pivot = new Vector2(0.5f, 1f); // ears stay in; the extra goes off the bottom, under the name
                _card.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                // the bust is clipped at the bottom and sides only: the ears stick out over the card, like in the concept
                var clip = Ui.Rect(border.transform, "BustClip").Place(Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, 110));
                clip.gameObject.AddComponent<RectMask2D>();
                _portrait = Ui.Panel(clip, "Portrait", Color.white);
                _portrait.preserveAspect = true;
                _portrait.rectTransform.Place(new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Vector2.zero, new Vector2(0, -40));
                _portraitName = Ui.Label(border.transform, "", 34, TextAnchor.LowerCenter, Palette.Paper, heading: true);
                _portraitName.rectTransform.Fill(4);
                _portraitName.Outlined();

                _status = Ui.Label(content, "", 26, TextAnchor.MiddleCenter, Palette.Ink);
                _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -204), new Vector2(-4, -174));

                _progress = Ui.Bar(content, "Progress", Palette.Progress);
                _progress.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -218), new Vector2(-10, -206));

                _energy = Ui.Bar(content, "Energy", Palette.Energy, "energy");
                _energy.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -258), new Vector2(-6, -226));

                _satiety = Ui.Bar(content, "Satiety", Palette.Satiety, "satiety");
                _satiety.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -298), new Vector2(-6, -266));

                _feed = Ui.Button(content, "Feed" + index, Loc.T("cat_panel_view.01"), Palette.Primary, () =>
                {
                    if (_cat != null) _owner._onResult(_owner._session.FeedCat(_cat.Id));
                }, 28);
                _feed.Rect.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 60));

                _locked = Ui.Label(background.transform, Loc.T("cat_panel_view.02"), 32, TextAnchor.MiddleCenter, Palette.Disabled).gameObject;
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
                var key = session.Config.GetCat(cat.Id).ViewKey;
                var card = key != null ? _owner._art?.Card(key) : null;
                var portrait = card ?? (key != null ? _owner._art?.Portrait(key) : null);
                _portrait.transform.parent.parent.gameObject.SetActive(portrait != null);
                _portrait.transform.parent.gameObject.SetActive(card == null);
                _backdrop.gameObject.SetActive(card == null);
                _card.gameObject.SetActive(card != null);
                if (card != null)
                {
                    _card.sprite = card;
                    _card.GetComponent<AspectRatioFitter>().aspectRatio = card.rect.width / card.rect.height;
                }
                _backdrop.color = CatColors.Backdrop(cat.Id);
                _header.gameObject.SetActive(portrait == null);
                if (portrait != null)
                {
                    if (card == null) _portrait.sprite = portrait;
                    _portraitName.text = Texts.CatName(cat.Id);
                }
                _name.color = CatColors.TextOn(color);
                _name.text = Texts.CatName(cat.Id);
                _status.text = cat.Activity.IsWorking
                    ? Texts.Doing(session.Config.GetActivity(cat.Activity.ActivityId))
                    : Texts.IdleStatus(cat.Activity.IdleReason);

                var max = (float)session.Config.Needs.Max;
                _energy.Set(cat.Energy / max, Ui.Skin != null ? cat.Energy.ToString() : Loc.T("cat_panel_view.03", cat.Energy));
                _satiety.Set(cat.Satiety / max, Ui.Skin != null ? cat.Satiety.ToString() : Loc.T("cat_panel_view.04", cat.Satiety));

                var canFeed = session.CanFeed(cat.Id);
                _feed.Interactable = canFeed == CommandError.None;
                _feed.Text.text = canFeed == CommandError.None ? Loc.T("cat_panel_view.05", session.FoodToFeed(cat.Id)) : Texts.Error(canFeed);
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
