using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Cat details (Docs/GDD/UI_UX.md §5): attributes as icon + level + XP bar, combat stats as icon chips, the skill
    /// slots with the equipped skills' icons, and the skill list with icons, costs and the progress towards locked ones.
    /// </summary>
    public sealed class CatDetailsSheet
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly GameObject _root;
        private readonly TMP_Text _header;
        private readonly RectTransform _aptitude;
        private readonly RectTransform _attributes;
        private readonly RectTransform _combat;
        private readonly RectTransform _slots;
        private readonly RectTransform _skills;
        private CatId? _catId;

        public bool IsOpen => _catId != null;

        public CatDetailsSheet(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;

            var dim = Ui.Panel(parent, "CatDetails", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            _root = dim.gameObject;
            var panel = Ui.Card(dim.transform, "Sheet", CardStyle.Paper, raycast: true);
            panel.rectTransform.Place(Vector2.zero, new Vector2(1, 0.88f));

            _header = Ui.Label(panel.transform, "", 50, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            _header.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(60, -120), new Vector2(0, -50));
            _aptitude = Info.Row(panel.transform, "Aptitude", 8f, TextAnchor.MiddleRight);
            _aptitude.Place(new Vector2(0.5f, 1), Vector2.one, new Vector2(0, -120), new Vector2(-60, -50));

            _attributes = Info.Column(panel.transform, "Attributes", 6f);
            _attributes.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -380), new Vector2(-60, -130));
            _combat = Info.Row(panel.transform, "Combat", 36f, TextAnchor.MiddleLeft);
            _combat.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -460), new Vector2(-60, -390));
            _slots = Info.Row(panel.transform, "Slots", 16f, TextAnchor.MiddleLeft);
            _slots.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -585), new Vector2(-60, -475));
            _skills = Ui.Rect(panel.transform, "Skills").Place(Vector2.zero, Vector2.one, new Vector2(30, 150), new Vector2(-30, -600));

            var close = Ui.Button(panel.transform, "Details_Close", Loc.T("cat_details_sheet.01"), Palette.Disabled, Close, 36);
            close.Rect.Place(new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(0, 30), new Vector2(0, 130));
            _root.SetActive(false);
        }

        public void Open(CatId catId)
        {
            _catId = catId;
            Refresh();
            _overlays.Push(_root, () => _catId = null);
        }

        public void Close() => _overlays.Close(_root);

        public void Refresh()
        {
            if (_catId == null || !_session.World.TryGetCat(_catId.Value, out var cat))
                return;

            var config = _session.Config;
            var id = _catId.Value;
            _header.text = Texts.CatName(id);
            Clear(_aptitude);
            Info.Text(_aptitude, Loc.T("cat_details_sheet.11"), 28, Palette.Ink, height: 60);
            Info.Chip(_aptitude, Texts.AttributeIcon(cat.Aptitude), Texts.Attribute(cat.Aptitude), 56f, 32, Palette.Ink);

            // attributes: icon, name, level, XP towards the next level on a bar
            Clear(_attributes);
            foreach (var a in AttributeTypes.All)
            {
                var level = cat.GetLevel(a);
                var max = level >= config.Progression.MaxLevel;
                var next = max ? 1 : config.Progression.XpToNext(level);
                var xp = max ? next : cat.GetXp(a);
                Info.AttributeRow(_attributes, Texts.AttributeIcon(a), Texts.Attribute(a), level, (float)xp / next, max ? "MAX" : $"{xp}/{next}", a == cat.Aptitude);
            }

            var c = config.Combat;
            var endurance = cat.GetLevel(AttributeType.Endurance);
            Clear(_combat);
            Info.Chip(_combat, "hp", $"HP {CombatFormulas.MaxHp(c, endurance)}", 60f, 34, Palette.Ink);
            Info.Chip(_combat, "paw", Loc.T("cat_details_sheet.12", CombatFormulas.MaxGrit(c, endurance)), 60f, 34, Palette.Ink);
            Info.Chip(_combat, "skill_scratch", Loc.T("cat_details_sheet.13", CombatFormulas.BaseDamage(c, cat.GetLevel(AttributeType.Strength))), 60f, 34, Palette.Ink);

            // the slots: the equipped skill's icon and name, or an empty frame; the scratch is always there
            var equipped = Enumerable.Range(0, Cat.SkillSlotCount).Select(cat.GetSkillInSlot).ToList();
            Clear(_slots);
            for (var i = 0; i < equipped.Count; i++)
            {
                var slot = Ui.Panel(_slots, "Slot" + i, equipped[i] != null ? Palette.Primary : new Color(0, 0, 0, 0.12f));
                Info.Size(slot, 250, 100);
                var row = Info.Row(slot.transform, "Content", 10f, TextAnchor.MiddleLeft);
                row.Fill(10);
                if (equipped[i] is SkillId skill)
                {
                    Info.Icon(row, Texts.SkillIcon(skill), 70f);
                    Info.Text(row, Texts.SkillName(skill), 26, Palette.Paper).Outlined();
                }
                else
                    Info.Text(row, Loc.T("cat_details_sheet.14", i + 1), 24, Palette.Ink, heading: false);
            }
            var innate = config.Skills.FirstOrDefault(s => s.IsInnate);
            if (innate != null)
            {
                var always = Info.Row(_slots, "Innate", 6f, TextAnchor.MiddleLeft);
                Info.Size(always, -1, 100);
                Info.Icon(always, Texts.SkillIcon(innate.Id), 60f);
                Info.Text(always, Loc.T("cat_details_sheet.15"), 26, Palette.Ink);
            }

            for (var i = _skills.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_skills.GetChild(i).gameObject);
            var skills = config.Skills.Where(s => !s.IsInnate).ToList();
            for (var i = 0; i < skills.Count; i++)
                SkillRow(cat, skills[i], equipped.IndexOf(skills[i].Id), i, skills.Count);
        }

        /// <summary>
        /// One skill: icon, name and state, what it does, its grit cost; a locked one shows the required attribute's
        /// progress on a bar instead of being greyed out without a hint.
        /// </summary>
        private void SkillRow(IReadOnlyCat cat, SkillDefinition skill, int slot, int index, int count)
        {
            var unlocked = CatRules.IsSkillUnlocked(cat, skill);
            var color = !unlocked ? Palette.Panel : slot >= 0 ? Palette.Primary : Palette.Secondary;
            var skillId = skill.Id;
            var row = Ui.Button(_skills, "Skill_" + skill.Id, "", color, () => Toggle(skillId), 30);
            row.Rect.Place(new Vector2(0, 1f - (index + 1f) / count), new Vector2(1, 1f - (float)index / count), new Vector2(0, 5), new Vector2(0, -5));
            row.Interactable = unlocked;
            row.Text.gameObject.SetActive(false);

            var icon = Ui.Icon(row.Rect, Texts.SkillIcon(skill.Id));
            if (icon != null)
            {
                icon.rectTransform.Place(new Vector2(0, 0.12f), new Vector2(0, 0.88f), new Vector2(30, 0), new Vector2(150, 0));
                if (!unlocked) icon.color = new Color(1, 1, 1, 0.45f);
            }

            var state = !unlocked ? "" : slot >= 0 ? Loc.T("cat_details_sheet.16", slot + 1) : Loc.T("cat_details_sheet.09");
            var title = Ui.Label(row.Rect, $"{Texts.SkillName(skill.Id)}  <size=24>{state}</size>", 34, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            title.rectTransform.Place(new Vector2(0, 0.55f), new Vector2(1, 1), new Vector2(170, 0), new Vector2(-200, -10));
            var description = Ui.Label(row.Rect, Texts.SkillDescription(skill), 26, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            description.rectTransform.Place(new Vector2(0, 0.1f), new Vector2(1, 0.55f), new Vector2(170, 0), new Vector2(-200, 0));

            // grit cost on the right
            var cost = Info.Row(row.Rect, "Cost", 4f, TextAnchor.MiddleRight);
            cost.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-190, -40), new Vector2(-24, 40));
            Info.Chip(cost, "paw", skill.GritCost.ToString(), 56f, 32, Palette.Paper);

            if (!unlocked)
            {
                // the attribute the skill waits for, as progress: "Техника 3/7"
                var have = cat.GetLevel(skill.UnlockAttribute);
                var need = Info.Row(row.Rect, "Need", 8f, TextAnchor.MiddleLeft);
                need.Place(new Vector2(0, 0.1f), new Vector2(1, 0.5f), new Vector2(170, 0), new Vector2(-200, 0));
                description.gameObject.SetActive(false);
                Info.Icon(need, "lock", 44f);
                Info.Icon(need, Texts.AttributeIcon(skill.UnlockAttribute), 44f);
                var bar = Ui.Bar(need, "Progress", Palette.Progress);
                Info.Size(bar.Rect, 260, 40);
                bar.Set((float)have / Mathf.Max(1, skill.UnlockLevel), $"{have}/{skill.UnlockLevel}");
                Info.Text(need, Loc.T("cat_details_sheet.17", Texts.Attribute(skill.UnlockAttribute), skill.UnlockLevel), 24, Palette.Paper);
            }
            // light text with an ink outline reads on every row colour (taken, free, locked)
            foreach (var label in row.Rect.GetComponentsInChildren<TMP_Text>(true))
                label.Outlined();
        }

        private static void Clear(RectTransform rect)
        {
            for (var i = rect.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(rect.GetChild(i).gameObject);
        }

        private void Toggle(SkillId skill)
        {
            if (_catId == null || !_session.World.TryGetCat(_catId.Value, out var cat))
                return;
            var slots = Enumerable.Range(0, Cat.SkillSlotCount).Select(cat.GetSkillInSlot).ToList();
            var current = slots.IndexOf(skill);
            CommandResult result;
            if (current >= 0)
                result = _session.EquipSkill(_catId.Value, current, null);
            else
            {
                var free = slots.IndexOf(null);
                result = free >= 0 ? _session.EquipSkill(_catId.Value, free, skill) : CommandResult.Fail(CommandError.AlreadyEquipped);
                if (free < 0)
                {
                    _onResult(CommandResult.Fail(CommandError.InvalidSlot));
                    return;
                }
            }

            _onResult(result);
            Refresh();
        }
    }
}
