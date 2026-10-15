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

namespace TrashCats.Presentation.Battle
{
    /// <summary>
    /// Battle preparation (Docs/GDD/UI_UX.md §7). The relay is shown as two numbered queues face to face: the player's
    /// fight order (slots 1-3, each cat with ▲▼ to swap with its neighbour) against the enemy queue; the cats not in the
    /// order wait below in the reserve (tap to add; a tired one says why it cannot fight). Every fighter shows the same
    /// three combat numbers (damage, attack interval, HP). "Best order" tries every order of the rested cats with the
    /// same simulation as the win chance and applies the best one; the win chance sits right over the Fight button.
    /// </summary>
    public sealed class BattlePrepScreen
    {
        private const float CardHeight = 222f;
        private const float OrderStrip = 62f;
        private const float ChipIcon = 34f;
        private const int ChipFont = 26;
        /// <summary>Inner padding of a fighter card from its (single) frame.</summary>
        private const float Pad = 24f;
        private const float AvatarSize = 112f;

        private readonly GameSession _session;
        private readonly Action _onBack;
        private readonly Action<TerritoryId> _onFight;
        private readonly Action<CatId> _onCatDetails;
        private readonly Characters.CharacterViewFactory _art;
        private readonly RectTransform _queue;
        private readonly RectTransform _enemies;
        private readonly RectTransform _reserve;
        private readonly TMP_Text _reserveCaption;
        private readonly TMP_Text _title;
        private readonly TMP_Text _warning;
        private readonly TMP_Text _hint;
        private readonly UiButton _best;
        private readonly Info.SplitBar _chance;
        private readonly UiButton _fight;
        private readonly List<CatId> _order = new List<CatId>();
        private TerritoryId _territory;
        private string _bestKey;
        private (List<CatId> Order, int Win) _bestOrder;
        private int _shownWin = -1;

        public GameObject Root { get; }

        public BattlePrepScreen(GameSession session, RectTransform parent, Action onBack, Action<TerritoryId> onFight, Action<CatId> onCatDetails,
            Characters.CharacterViewFactory art = null)
        {
            _session = session;
            _onBack = onBack;
            _onFight = onFight;
            _onCatDetails = onCatDetails;
            _art = art;

            var root = Ui.Rect(parent, "BattlePrepScreen").Fill();
            Root = root.gameObject;
            Ui.Panel(root, "Back", Palette.Background).rectTransform.Fill();
            if (Ui.Skin != null)
                Ui.Card(root, "Tin", CardStyle.Tin).rectTransform.Fill(6);

            _title = Ui.Label(root, "", 50, TextAnchor.MiddleCenter, Palette.Paper);
            _title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -120), new Vector2(0, -20));
            _title.Outlined();

            // the two queues face to face
            var ring = Ui.Rect(root, "Ring").Place(new Vector2(0, 1), Vector2.one, new Vector2(24, -890), new Vector2(-24, -130));
            Caption(ring, Loc.T("battle_prep_screen.15"), new Vector2(0, 1), new Vector2(0.44f, 1), TextAnchor.MiddleLeft);
            Caption(ring, Loc.T("battle_prep_screen.16"), new Vector2(0.56f, 1), new Vector2(1, 1), TextAnchor.MiddleLeft);
            var vs = Ui.Label(ring, "VS", 44, TextAnchor.MiddleCenter, Palette.Primary, heading: true);
            vs.rectTransform.Place(new Vector2(0.44f, 1), new Vector2(0.56f, 1), new Vector2(0, -52), Vector2.zero);
            vs.Outlined();
            _queue = Info.Column(ring, "Queue", 14f);
            _queue.Place(Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, new Vector2(-8, -58));
            _enemies = Info.Column(ring, "Enemies", 14f);
            _enemies.Place(new Vector2(0.5f, 0), Vector2.one, new Vector2(8, 0), new Vector2(0, -58));

            // the reserve
            var reserve = Ui.Rect(root, "ReserveBox").Place(new Vector2(0, 1), Vector2.one, new Vector2(36, -1180), new Vector2(-36, -905));
            _reserveCaption = Caption(reserve, Loc.T("battle_prep_screen.17"), new Vector2(0, 1), Vector2.one, TextAnchor.MiddleLeft);
            _reserve = Info.Row(reserve, "Reserve", 14f, TextAnchor.UpperLeft);
            _reserve.Place(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -50));
            _reserve.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            // best order: what it would give, and a button that applies it
            _hint = Ui.Label(root, "", 28, TextAnchor.MiddleLeft, Palette.Paper);
            _hint.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 330), new Vector2(-330, 400));
            _hint.enableAutoSizing = true;
            _hint.fontSizeMin = 20;
            _hint.fontSizeMax = 28;
            _best = Ui.Button(root, "Prep_Best", Loc.T("battle_prep_screen.18"), Palette.Secondary, ApplyBest, 28);
            _best.Rect.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 326), new Vector2(-36, 404));

            _warning = Ui.Label(root, "", 26, TextAnchor.MiddleCenter, Palette.Primary);
            _warning.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 270), new Vector2(-40, 320));
            _warning.enableAutoSizing = true;
            _warning.fontSizeMin = 18;
            _warning.fontSizeMax = 26;

            // the chance to win right over the Fight button
            _chance = new Info.SplitBar(root, "WinChance");
            _chance.Rect.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 186), new Vector2(-40, 256));

            var back = Ui.Button(root, "Prep_Back", Loc.T("battle_prep_screen.03"), Palette.Disabled, () => _onBack(), 40);
            back.Rect.Place(Vector2.zero, new Vector2(0.4f, 0f), new Vector2(30, 30), new Vector2(-15, 170));
            _fight = Ui.Button(root, "Prep_Fight", Loc.T("battle_prep_screen.04"), Palette.Primary, Fight, 48);
            _fight.Rect.Place(new Vector2(0.4f, 0), new Vector2(1f, 0f), new Vector2(15, 30), new Vector2(-30, 170));
        }

        public void Open(TerritoryId territory)
        {
            _territory = territory;
            _order.Clear();
            // sensible default: every rested cat, strongest first (the player can reorder)
            _order.AddRange(_session.World.Cats
                .Where(c => c.Energy >= _session.Config.Needs.MinEnergyForBattle)
                .OrderByDescending(c => AttributeTypes.All.Sum(c.GetLevel))
                .Take(Battles.MaxTeamSize)
                .Select(c => c.Id));
            _shownWin = -1;
            Refresh();
        }

        public void Refresh()
        {
            if (Root == null || !_session.Config.TryGetTerritory(_territory, out var territory))
                return;

            var config = _session.Config;
            _title.text = Texts.TerritoryName(_territory);
            // a cat that got tired or is gone leaves the order
            _order.RemoveAll(id => !_session.World.TryGetCat(id, out var c) || c.Energy < config.Needs.MinEnergyForBattle);

            // enemies
            Clear(_enemies);
            var enemies = territory.EnemyQueue.Select(config.GetEnemy).ToList();
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                var card = Card(_enemies, "PrepEnemy" + i, CardStyle.Strip);
                Picture(card, _art?.Portrait(e.ViewKey), new Color(0.27f, 0.22f, 0.2f), round: true);
                Body(card, Texts.EnemyName(e.Id.Value), FighterSnapshot.FromEnemy(e), Palette.Ink);
            }

            // the order: slots 1..3, empty ones say how to fill them
            Clear(_queue);
            for (var i = 0; i < Battles.MaxTeamSize; i++)
            {
                if (i < _order.Count && _session.World.TryGetCat(_order[i], out var cat))
                    QueueCat(cat, i);
                else
                    EmptySlot(i);
            }

            // the reserve
            Clear(_reserve);
            var reserve = _session.World.Cats.Where(c => !_order.Contains(c.Id)).ToList();
            _reserveCaption.transform.parent.gameObject.SetActive(reserve.Count > 0);
            foreach (var cat in reserve)
                ReserveCat(cat);

            RefreshChance();
        }

        // --- cards -----------------------------------------------------------------------------------------------

        private void QueueCat(IReadOnlyCat cat, int index)
        {
            var id = cat.Id;
            var card = Card(_queue, "PrepCat_" + cat.Id, CardStyle.Strip);
            var background = card.GetComponent<Image>();
            background.color = Ui.Skin != null ? new Color(1f, 0.74f, 0.25f) : Palette.Primary;
            background.raycastTarget = true;
            background.gameObject.AddComponent<Button>().onClick.AddListener(() => Remove(id));
            Avatar(card, cat);
            Body(card, Texts.CatName(cat.Id), FighterSnapshot.FromCat(cat, _session.Config), Palette.Ink, rightGap: Pad + OrderStrip + 14);

            // the order control on the right edge, one dark plate: ▲ (if it can go up), the cat's number, ▼ (if it can go
            // down); arrows that could not move the cat are not drawn
            var plate = Ui.Panel(card, "Order", Palette.Ink);
            if (Ui.Skin?.button != null)
            {
                plate.sprite = Ui.Skin.button;
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 3f;
            }
            plate.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-Pad - OrderStrip, Pad - 4), new Vector2(-Pad, -Pad + 4));
            var label = Ui.Label(plate.transform, (index + 1).ToString(), 40, TextAnchor.MiddleCenter, Palette.Primary, heading: true);
            label.rectTransform.Place(new Vector2(0, 0.33f), new Vector2(1, 0.67f));
            label.Outlined();
            if (index > 0)
                Arrow(plate.rectTransform, "Up_" + cat.Id, "▲", () => Swap(index, index - 1)).Rect.Place(new Vector2(0, 0.67f), Vector2.one, new Vector2(5, 0), new Vector2(-5, -5));
            if (index < _order.Count - 1)
                Arrow(plate.rectTransform, "Down_" + cat.Id, "▼", () => Swap(index, index + 1)).Rect.Place(Vector2.zero, new Vector2(1, 0.33f), new Vector2(5, 5), new Vector2(-5, 0));
            if (cat.Activity.IsWorking)
                Note(card, Loc.T("battle_prep_screen.19"));
        }

        private void EmptySlot(int index)
        {
            var slot = Ui.Panel(_queue, "PrepSlot" + index, Ui.Skin?.barFrame != null ? new Color(1f, 1f, 1f, 0.35f) : new Color(0, 0, 0, 0.3f));
            slot.sprite = Ui.Skin?.barFrame;
            slot.type = Image.Type.Sliced;
            slot.pixelsPerUnitMultiplier = 2f;
            Info.Size(slot, -1, CardHeight);
            var label = Ui.Label(slot.transform, Loc.T("battle_prep_screen.20"), 26, TextAnchor.MiddleCenter, Palette.Paper);
            label.rectTransform.Fill(30);
            label.Outlined();
        }

        /// <summary>A cat waiting in the reserve: tap to add; a tired one is faded and says why it cannot fight.</summary>
        private void ReserveCat(IReadOnlyCat cat)
        {
            var config = _session.Config;
            var tired = cat.Energy < config.Needs.MinEnergyForBattle;
            var id = cat.Id;
            var reason = tired ? Loc.T("battle_prep_screen.21", config.Needs.MinEnergyForBattle, cat.Energy) : null;
            var row = Ui.Button(_reserve, "PrepCat_" + cat.Id, "", tired ? Palette.Disabled : Palette.Secondary, () =>
            {
                if (reason != null) Warn(Texts.CatName(id) + ": " + reason);
                else Add(id);
            }, 26);
            row.Text.gameObject.SetActive(false);
            Info.Size(row.Image, 310, 200);
            var card = row.Rect;
            var avatar = Avatar(card, cat, size: 96f, top: true);
            // a tired cat: only the card's backing is grey, the avatar and the texts stay as they are
            var name = Ui.Label(card, Texts.CatName(cat.Id), 28, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(132, -70), new Vector2(-14, -18));
            name.Outlined();
            var energy = Ui.Bar(card, "Energy", Palette.Energy, "energy");
            energy.Rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(176, -112), new Vector2(-18, -82));
            energy.Set(cat.Energy / (float)config.Needs.Max, cat.Energy.ToString());
            var status = Info.Row(card, "Status", 6f, TextAnchor.MiddleLeft);
            status.Place(Vector2.zero, new Vector2(1, 0), new Vector2(20, 14), new Vector2(-14, 74));
            if (tired) Info.Icon(status, "sleep", 34f);
            var text = Info.Text(status, tired ? reason : Loc.T("battle_prep_screen.22"), 22, Palette.Paper);
            text.textWrappingMode = TextWrappingModes.Normal;
            Info.Size(text, 250, 56);
            text.Outlined();
        }

        /// <summary>A fighter card with a single thin frame (no inner line); content is padded from that frame.</summary>
        private static RectTransform Card(RectTransform column, string name, CardStyle style)
        {
            var card = Ui.Card(column, name, style);
            if (Ui.Skin?.plainCard != null)
            {
                card.sprite = Ui.Skin.plainCard;
                card.type = Image.Type.Sliced;
                card.pixelsPerUnitMultiplier = 3.2f;
            }
            Info.Size(card, -1, CardHeight);
            return card.rectTransform;
        }

        private static TMP_Text Caption(RectTransform parent, string text, Vector2 min, Vector2 max, TextAnchor align)
        {
            var label = Ui.Label(parent, text, 28, align, Palette.Disabled);
            label.rectTransform.Place(min, max, new Vector2(4, -48), Vector2.zero);
            return label;
        }

        /// <summary>Square avatar on the left (tap = the cat's skills and stats), with a small skill badge in its corner.</summary>
        private Image Avatar(RectTransform card, IReadOnlyCat cat, float size = AvatarSize, bool top = false)
        {
            var viewKey = _session.Config.GetCat(cat.Id).ViewKey;
            var sprite = viewKey != null ? _art?.Avatar(viewKey) ?? _art?.Portrait(viewKey) : null;
            var image = Ui.Panel(card, "Avatar", Color.white, raycast: true);
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            if (top)
                image.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -18 - size), new Vector2(18 + size, -18));
            else
                image.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(Pad, -size / 2), new Vector2(Pad + size, size / 2));
            var id = cat.Id;
            image.gameObject.AddComponent<Button>().onClick.AddListener(() => _onCatDetails(id));
            if (Ui.Icon(image.transform, "skill") is Image badge)
                badge.rectTransform.Place(new Vector2(0.62f, -0.08f), new Vector2(1.08f, 0.38f));
            return image;
        }

        private static void Picture(RectTransform card, Sprite sprite, Color backdrop, bool round)
        {
            var holder = Ui.Rect(card, "Portrait").Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(Pad, -AvatarSize / 2), new Vector2(Pad + AvatarSize, AvatarSize / 2));
            var picture = new RoundPicture(holder, "Round", backdrop, 0.8f);
            picture.Root.Fill();
            picture.SetPicture(sprite);
        }

        /// <summary>Name, then the three combat numbers as chips (damage, attack interval, HP), then the skills as icons.</summary>
        private void Body(RectTransform card, string name, FighterSnapshot f, Color color, float rightGap = 16f)
        {
            var c = _session.Config.Combat;
            var column = Info.Column(card, "Body", 2f);
            column.Place(Vector2.zero, Vector2.one, new Vector2(Pad + AvatarSize + 16, Pad - 6), new Vector2(-rightGap, -Pad + 6));
            var title = Info.Text(column, name, 34, color, height: 52f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 22;
            title.fontSizeMax = 34;
            var stats = Info.Row(column, "Stats", 14f, TextAnchor.MiddleLeft);
            Info.Size(stats, -1, 56);
            Chip(stats, "skill_scratch", CombatFormulas.BaseDamage(c, f.Strength).ToString(), color);
            var interval = CombatFormulas.IntervalTicks(c, f.Technique) * c.TickMs / 1000f;
            Chip(stats, "speed", interval.ToString("0.#"), color);
            Chip(stats, "hp", CombatFormulas.MaxHp(c, f.Endurance, f.HpMultiplierPermille).ToString(), color);
            var skills = Info.Row(column, "Skills", 6f, TextAnchor.MiddleLeft);
            Info.Size(skills, -1, 56);
            if (f.Skills.Count == 0)
                Info.Text(skills, Loc.T("battle_prep_screen.05"), 24, color);
            foreach (var skill in f.Skills)
                Info.Icon(skills, Texts.SkillIcon(skill), 52f);
            if (color == Palette.Paper)
                foreach (var label in column.GetComponentsInChildren<TMP_Text>())
                    label.Outlined();
        }

        /// <summary>Icon and number with air between them.</summary>
        private static void Chip(RectTransform row, string icon, string text, Color color)
        {
            var chip = Info.Row(row, "Chip_" + icon, 7f);
            Info.Size(chip, -1, 56);
            Info.Icon(chip, icon, ChipIcon);
            Info.Text(chip, text, ChipFont, color, height: 56);
        }

        /// <summary>A big arrow button on the order plate.</summary>
        private static UiButton Arrow(RectTransform plate, string name, string glyph, Action onClick)
        {
            var arrow = Ui.Button(plate, name, glyph, Palette.Secondary, onClick, 34);
            arrow.Text.rectTransform.Fill(2);
            arrow.Text.fontSizeMin = 26;
            arrow.Text.color = Palette.Paper;
            return arrow;
        }

        private static void Note(RectTransform card, string text)
        {
            var label = Ui.Label(card, text, 20, TextAnchor.LowerRight, Palette.Paper);
            label.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(160, 4), new Vector2(-70, 30));
            label.Outlined();
        }

        // --- order -----------------------------------------------------------------------------------------------

        private void Add(CatId id)
        {
            if (_order.Count >= Battles.MaxTeamSize)
            {
                Warn(Loc.T("battle_prep_screen.24"));
                return;
            }
            _order.Add(id);
            Refresh();
        }

        private void Remove(CatId id)
        {
            _order.Remove(id);
            Refresh();
        }

        private void Swap(int a, int b)
        {
            if (a < 0 || b < 0 || a >= _order.Count || b >= _order.Count) return;
            (_order[a], _order[b]) = (_order[b], _order[a]);
            Refresh();
        }

        private void ApplyBest()
        {
            if (_bestOrder.Order == null) return;
            _order.Clear();
            _order.AddRange(_bestOrder.Order);
            Refresh();
        }

        private void Warn(string text)
        {
            _warning.text = text;
            _warning.rectTransform.DOKill();
            _warning.rectTransform.localScale = Vector3.one;
            _warning.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 4, 0.5f).SetLink(_warning.gameObject);
        }

        /// <summary>Win chance of the order and of the best order of the rested cats (cached while nothing changes).</summary>
        private void RefreshChance()
        {
            var error = _session.CanStartBattle(_territory, _order);
            _fight.Interactable = error == CommandError.None;
            _chance.Rect.gameObject.SetActive(_order.Count > 0);
            var win = _order.Count > 0 ? Mathf.RoundToInt(_session.EstimateWinChance(_territory, _order) * 100f) : 0;
            if (_order.Count > 0)
            {
                _chance.Set(win / 100f, Loc.T("battle_prep_screen.13", win), Loc.T("battle_prep_screen.14", 100 - win));
                if (_shownWin >= 0 && _shownWin != win)
                    _chance.Rect.DOPunchScale(new Vector3(0.03f, 0.12f, 0f), 0.3f, 4, 0.5f).SetLink(_chance.Rect.gameObject);
                _shownWin = win;
            }

            var best = BestOrder();
            var better = best.Order != null && best.Win > win && !best.Order.SequenceEqual(_order);
            _best.Interactable = better;
            _best.SetColor(better ? Palette.Secondary : Palette.Disabled);
            _hint.text = best.Order == null ? ""
                : better ? Loc.T("battle_prep_screen.25", string.Join(" → ", best.Order.Select(Texts.CatName)), best.Win - win)
                : Loc.T("battle_prep_screen.26");
            _hint.Outlined();

            var busy = _order.Where(id => _session.World.TryGetCat(id, out var c) && c.Activity.IsWorking).Select(Texts.CatName).ToList();
            _warning.text = error != CommandError.None ? Texts.Error(error)
                : busy.Count > 0 ? Loc.T("battle_prep_screen.11", string.Join(", ", busy))
                : Loc.T("battle_prep_screen.12");
        }

        /// <summary>Every order of every team of the rested cats (at most 15 for three cats), the best win chance.</summary>
        private (List<CatId> Order, int Win) BestOrder()
        {
            var config = _session.Config;
            var rested = _session.World.Cats.Where(c => c.Energy >= config.Needs.MinEnergyForBattle).ToList();
            var key = _territory.Value + "|" + string.Join(";", rested.Select(c =>
                $"{c.Id}:{string.Join(",", AttributeTypes.All.Select(c.GetLevel))}:{string.Join(",", Enumerable.Range(0, Cat.SkillSlotCount).Select(s => c.GetSkillInSlot(s)?.Value))}"));
            if (key == _bestKey)
                return _bestOrder;
            _bestKey = key;
            _bestOrder = (null, -1);
            foreach (var order in Orders(rested.Select(c => c.Id).ToList()))
            {
                var win = Mathf.RoundToInt(_session.EstimateWinChance(_territory, order) * 100f);
                // more cats only when it helps: a smaller team keeps the others rested
                if (win > _bestOrder.Win)
                    _bestOrder = (order, win);
            }
            return _bestOrder;
        }

        private static IEnumerable<List<CatId>> Orders(List<CatId> cats)
        {
            for (var size = 1; size <= Math.Min(Battles.MaxTeamSize, cats.Count); size++)
                foreach (var order in Permutations(cats, size))
                    yield return order;
        }

        private static IEnumerable<List<CatId>> Permutations(List<CatId> pool, int size)
        {
            if (size == 0)
            {
                yield return new List<CatId>();
                yield break;
            }
            foreach (var first in pool)
                foreach (var rest in Permutations(pool.Where(c => !c.Equals(first)).ToList(), size - 1))
                {
                    rest.Insert(0, first);
                    yield return rest;
                }
        }

        private void Fight()
        {
            var result = _session.StartBattle(_territory, _order.ToArray());
            if (result.IsOk)
                _onFight(_territory);
            else
                Warn(Texts.Error(result.Error));
        }

        private static void Clear(RectTransform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
        }
    }
}
