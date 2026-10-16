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
        /// <summary>How much taller a card is than the original layout: the name band and the whole (taller) picture.</summary>
        public const float ExtraHeight = Card.NameBand + Card.PictureExtra;

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

            // no backing strip: the cards stand on the scene, as in the concept
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
            private Image _namePlate;
            /// <summary>Height of the name band above the picture.</summary>
            public const float NameBand = 44f;

            /// <summary>The pictures keep the whole window (chin included) and are taller than the layout was made for.</summary>
            public const float PictureExtra = 32f;

            private static readonly Color StatusInk = Palette.Hex("#2A2118");
            private static readonly Color StatusAlert = Palette.Hex("#B3261E");
            private readonly TMP_Text _status;
            private readonly Image _statusIcon;
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
                if (Ui.Skin?.catCard != null)
                {
                    background.sprite = Ui.Skin.catCard;
                    background.pixelsPerUnitMultiplier = Ui.Skin.catCardBorderScale;
                }
                background.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (_cat != null) _owner._onCardTapped(_cat.Id);
                });

                _content = Ui.Rect(background.transform, "Content").Fill(Ui.Skin != null ? 16 : 0).gameObject;
                // the picture on top (ears over the card's edge), the name in its own band under it, then the rest
                var content = Ui.Rect(_content.transform, "Body").Place(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -NameBand - PictureExtra));

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
                // the concept's card picture: a rounded framed window with the ears sticking out over its top edge.
                // The frame touches the picture's sides and bottom, so it is laid out by width from the bottom.
                _card = Ui.Panel(content, "Card", Color.white);
                _card.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, NameBand - 170), new Vector2(0, NameBand - 170));
                _card.rectTransform.pivot = new Vector2(0.5f, 0f);
                var fitter = _card.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                // the bust is clipped at the bottom and sides only: the ears stick out over the card, like in the concept
                var clip = Ui.Rect(border.transform, "BustClip").Place(Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, 110));
                clip.gameObject.AddComponent<RectMask2D>();
                _portrait = Ui.Panel(clip, "Portrait", Color.white);
                _portrait.preserveAspect = true;
                _portrait.rectTransform.Place(new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Vector2.zero, new Vector2(0, -40));

                // the name on a light plate in the colour of the picture's backdrop: it belongs to the picture and stays quiet
                // the name on a light darkening across the whole card, right under the picture (touching it)
                _namePlate = Ui.Panel(content, "NamePlate", new Color(0f, 0f, 0f, 0f)); // no backing, only the name
                _namePlate.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(-10, NameBand - 170 - 42), new Vector2(10, NameBand - 170));
                _portraitName = Ui.Label(_namePlate.transform, "", 30, TextAnchor.MiddleCenter, Palette.Paper, heading: true);
                _portraitName.rectTransform.Fill(2);
                _portraitName.enableAutoSizing = true;
                _portraitName.fontSizeMax = 30;
                _portraitName.fontSizeMin = 20;

                // what the cat does, or why it waits with the reason's icon (sleep, fish, energy) — the scene has no bubbles
                var statusRow = Info.Row(content, "Status", 4f, TextAnchor.MiddleCenter);
                statusRow.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -206), new Vector2(-10, -172));
                _statusIcon = Ui.Panel(statusRow, "Icon", Color.white);
                _statusIcon.preserveAspect = true;
                Info.Size(_statusIcon, 32, 32);
                // dark body text without the comic outline: small white outlined lettering was unreadable on the paper
                _status = Ui.Label(statusRow, "", 26, TextAnchor.MiddleCenter, Palette.Ink, heading: false);
                if (_status.font != null) _status.fontSharedMaterial = _status.font.material;
                _status.outlineWidth = 0f;
                _status.fontStyle = FontStyles.Bold;
                _status.enableAutoSizing = true;
                _status.fontSizeMax = 26;
                _status.fontSizeMin = 18;
                var statusLayout = _status.gameObject.AddComponent<LayoutElement>();
                statusLayout.flexibleWidth = 1f;
                statusLayout.minHeight = 34f;

                _progress = Ui.Bar(content, "Progress", Palette.Progress);
                _progress.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(51, -218), new Vector2(-6, -206)); // ends like the bars below

                _energy = Ui.Bar(content, "Energy", Palette.Energy, "energy");
                _energy.Animated = true;
                _energy.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -258), new Vector2(-6, -226));

                _satiety = Ui.Bar(content, "Satiety", Palette.Satiety, "satiety");
                _satiety.Animated = true;
                _satiety.Rect.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -298), new Vector2(-6, -266));

                _feed = Ui.Button(content, "Feed" + index, Loc.T("cat_panel_view.01"), Palette.Primary, () =>
                {
                    if (_cat != null) _owner._onResult(_owner._session.FeedCat(_cat.Id));
                }, 28);
                _feed.Rect.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 74));

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
                _portrait.transform.parent.parent.gameObject.SetActive(portrait != null && card == null);
                _card.gameObject.SetActive(card != null);
                if (card != null)
                {
                    _card.sprite = card;
                    _card.GetComponent<AspectRatioFitter>().aspectRatio = card.rect.width / card.rect.height;
                }
                _backdrop.color = CatColors.Backdrop(cat.Id);
                _header.gameObject.SetActive(portrait == null);
                _namePlate.gameObject.SetActive(portrait != null);
                if (portrait != null)
                {
                    if (card == null) _portrait.sprite = portrait;
                    _portraitName.text = Texts.CatName(cat.Id);
                }
                _name.color = CatColors.TextOn(color);
                _name.text = Texts.CatName(cat.Id);
                var reason = Texts.CurrentIdle(cat, session.Config);
                var reasonIcon = Texts.IdleIcon(reason) is string iconKey ? Ui.Skin?.Icon(iconKey) : null;
                _statusIcon.sprite = reasonIcon;
                _statusIcon.gameObject.SetActive(reasonIcon != null);
                // tired or hungry: in red, it needs the player
                _status.color = reason is IdleReason.Tired or IdleReason.Hungry ? StatusAlert : StatusInk;
                _status.text = cat.Activity.IsWorking
                    ? Texts.Doing(session.Config.GetActivity(cat.Activity.ActivityId))
                    : Texts.IdleStatus(reason);

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
