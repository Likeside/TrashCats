using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Battle preparation (Docs/GDD/UI_UX.md §7): enemy queue with stats and skills, the gang with fight order
    /// (tap to add/remove), warning about interrupted activities, Fight / Back.
    /// </summary>
    public sealed class BattlePrepScreen
    {
        private readonly GameSession _session;
        private readonly Action _onBack;
        private readonly Action<TerritoryId> _onFight;
        private readonly Action<CatId> _onCatDetails;
        private readonly RectTransform _enemyList;
        private readonly RectTransform _catList;
        private readonly TMP_Text _title;
        private readonly TMP_Text _warning;
        private readonly UiButton _fight;
        private readonly List<CatId> _order = new List<CatId>();
        private TerritoryId _territory;

        public GameObject Root { get; }

        public BattlePrepScreen(GameSession session, RectTransform parent, Action onBack, Action<TerritoryId> onFight, Action<CatId> onCatDetails)
        {
            _session = session;
            _onBack = onBack;
            _onFight = onFight;
            _onCatDetails = onCatDetails;

            var root = Ui.Rect(parent, "BattlePrepScreen").Fill();
            Root = root.gameObject;
            Ui.Panel(root, "Back", Palette.Background).rectTransform.Fill();
            if (Ui.Skin != null)
                Ui.Card(root, "Tin", CardStyle.Tin).rectTransform.Fill(6);

            _title = Ui.Label(root, "", 50, TextAnchor.MiddleCenter, Palette.Paper);
            _title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -130), new Vector2(0, -20));
            _title.Outlined();

            Section(root, "Враги (по очереди)", 0.62f, 0.9f, out _enemyList);
            Section(root, "Твоя банда — нажми, чтобы выбрать порядок", 0.2f, 0.6f, out _catList);

            _warning = Ui.Label(root, "", 28, TextAnchor.MiddleCenter, Palette.Primary);
            _warning.rectTransform.Place(new Vector2(0, 0.12f), new Vector2(1, 0.19f), new Vector2(30, 0), new Vector2(-30, 0));

            var back = Ui.Button(root, "Prep_Back", "Назад", Palette.Disabled, () => _onBack(), 40);
            back.Rect.Place(Vector2.zero, new Vector2(0.4f, 0.11f), new Vector2(30, 30), new Vector2(-15, 0));
            _fight = Ui.Button(root, "Prep_Fight", "В БОЙ!", Palette.Primary, Fight, 48);
            _fight.Rect.Place(new Vector2(0.4f, 0), new Vector2(1f, 0.11f), new Vector2(15, 30), new Vector2(-30, 0));
        }

        public void Open(TerritoryId territory)
        {
            _territory = territory;
            _order.Clear();
            // sensible default: every rested cat, strongest first (the player can reorder with taps)
            _order.AddRange(_session.World.Cats
                .Where(c => c.Energy >= _session.Config.Needs.MinEnergyForBattle)
                .OrderByDescending(c => AttributeTypes.All.Sum(c.GetLevel))
                .Take(Battles.MaxTeamSize)
                .Select(c => c.Id));
            Refresh();
        }

        public void Refresh()
        {
            if (Root == null || !_session.Config.TryGetTerritory(_territory, out var territory))
                return;

            var config = _session.Config;
            _title.text = Texts.TerritoryName(_territory);

            Clear(_enemyList);
            var enemies = territory.EnemyQueue.Select(config.GetEnemy).ToList();
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                var hp = CombatFormulas.MaxHp(config.Combat, e.Toughness, e.HpMultiplierPermille);
                var skills = e.Skills.Count == 0 ? "только Царапина" : string.Join(", ", e.Skills.Select(Texts.SkillName));
                InfoRow(_enemyList, "PrepEnemy" + i, i, enemies.Count,
                    $"{i + 1}. {Texts.EnemyName(e.Id.Value)}   {Texts.Stats(e.Strength, e.Agility, e.Toughness)} · HP {hp}\n<size=26>{skills}</size>");
            }

            Clear(_catList);
            var cats = _session.World.Cats;
            for (var i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var position = _order.IndexOf(cat.Id);
                var tired = cat.Energy < config.Needs.MinEnergyForBattle;
                var skills = Enumerable.Range(0, Cat.SkillSlotCount).Select(cat.GetSkillInSlot).Where(s => s != null).Select(s => Texts.SkillName(s.Value)).ToList();
                var label = $"{(position >= 0 ? $"[{position + 1}] " : "")}{Texts.CatName(cat.Id)}   {Texts.Stats(cat.GetLevel(AttributeType.Strength), cat.GetLevel(AttributeType.Agility), cat.GetLevel(AttributeType.Toughness))} · энергия {cat.Energy}"
                            + $"\n<size=26>{(skills.Count == 0 ? "только Царапина" : string.Join(", ", skills))}{(tired ? " · устал" : "")}{(cat.Activity.IsWorking ? " · занят" : "")}</size>";
                var color = position >= 0 ? Palette.Primary : tired ? Palette.Disabled : Palette.Panel;
                var id = cat.Id;
                var row = Row(_catList, "PrepCat_" + cat.Id, i, Math.Max(3, cats.Count), color, label, () => Toggle(id));
                row.Interactable = !tired || position >= 0;

                var details = Ui.Button(row.Rect, "Details_" + cat.Id, "Умения", Palette.Paper, () => _onCatDetails(id), 26);
                details.Rect.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-170, -35), new Vector2(-15, 35));
            }

            var error = _session.CanStartBattle(_territory, _order);
            _fight.Interactable = error == CommandError.None;
            var busy = _order.Where(id => _session.World.TryGetCat(id, out var c) && c.Activity.IsWorking).Select(Texts.CatName).ToList();
            _warning.text = error != CommandError.None ? Texts.Error(error)
                : _order.Count > 1 ? $"Порядок: {string.Join(" → ", _order.Select(Texts.CatName))}. Победитель дуэли остаётся на ринге."
                : busy.Count > 0 ? $"{string.Join(", ", busy)} бросит текущее занятие (прогресс цикла пропадёт)"
                : "Победитель дуэли остаётся на ринге с текущим здоровьем.";
        }

        private void Toggle(CatId id)
        {
            if (!_order.Remove(id) && _order.Count < Battles.MaxTeamSize)
                _order.Add(id);
            Refresh();
        }

        private void Fight()
        {
            var result = _session.StartBattle(_territory, _order.ToArray());
            if (result.IsOk)
                _onFight(_territory);
            else
                _warning.text = Texts.Error(result.Error);
        }

        private static void Section(RectTransform root, string caption, float y0, float y1, out RectTransform list)
        {
            var box = Ui.Rect(root, caption).Place(new Vector2(0, y0), new Vector2(1, y1), new Vector2(60, 0), new Vector2(-60, 0));
            var label = Ui.Label(box, caption, 30, TextAnchor.UpperLeft, Palette.Disabled);
            label.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -44), Vector2.zero);
            list = Ui.Rect(box, "List").Place(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -50));
        }

        private static UiButton Row(RectTransform list, string name, int index, int count, Color color, string text, Action onClick)
        {
            var slots = Math.Max(count, 3);
            var y1 = 1f - (float)index / slots;
            var y0 = 1f - (index + 1f) / slots;
            var row = Ui.Button(list, name, text, color, onClick, 32);
            row.Rect.Place(new Vector2(0, y0), new Vector2(1, y1), new Vector2(0, 6), new Vector2(0, -6));
            row.Text.Align(TextAnchor.MiddleLeft);
            row.Text.rectTransform.Fill(20);
            return row;
        }

        /// <summary>Read-only line on a cream card (enemies).</summary>
        private static void InfoRow(RectTransform list, string name, int index, int count, string text)
        {
            var slots = Math.Max(count, 3);
            var card = Ui.Card(list, name, CardStyle.Strip);
            card.rectTransform.Place(new Vector2(0, 1f - (index + 1f) / slots), new Vector2(1, 1f - (float)index / slots), new Vector2(0, 6), new Vector2(0, -6));
            var label = Ui.Label(card.transform, text, 32, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            label.rectTransform.Fill(28);
        }

        private static void Clear(RectTransform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
        }
    }
}
