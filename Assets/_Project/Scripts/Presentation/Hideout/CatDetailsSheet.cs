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
    /// <summary>Cat details (Docs/GDD/UI_UX.md §5): attributes with XP, combat stats, skill slots.</summary>
    public sealed class CatDetailsSheet
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly GameObject _root;
        private readonly TMP_Text _header;
        private readonly TMP_Text _attributes;
        private readonly TMP_Text _slots;
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

            _header = Ui.Label(panel.transform, "", 48, TextAnchor.UpperLeft, Palette.Ink);
            _header.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -116), new Vector2(-60, -50));
            _attributes = Ui.Label(panel.transform, "", 32, TextAnchor.UpperLeft, Palette.Ink);
            _attributes.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -340), new Vector2(-60, -122));
            _slots = Ui.Label(panel.transform, "", 32, TextAnchor.UpperLeft, Palette.Ink);
            _slots.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, -425), new Vector2(-60, -345));
            _skills = Ui.Rect(panel.transform, "Skills").Place(Vector2.zero, Vector2.one, new Vector2(30, 150), new Vector2(-30, -430));

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
            _header.text = Loc.T("cat_details_sheet.02", Texts.CatName(id), Texts.Attribute(cat.Aptitude));
            var lines = AttributeTypes.All.Select(a =>
            {
                var level = cat.GetLevel(a);
                var xp = level >= config.Progression.MaxLevel ? "MAX" : $"{cat.GetXp(a)}/{config.Progression.XpToNext(level)}";
                return $"{Texts.Attribute(a)} {level}  <size=26>({xp})</size>{(a == cat.Aptitude ? " *" : "")}";
            });
            var c = config.Combat;
            _attributes.text = string.Join("\n", lines)
                               + Loc.T("cat_details_sheet.03", CombatFormulas.MaxHp(c, cat.GetLevel(AttributeType.Endurance)), CombatFormulas.MaxGrit(c, cat.GetLevel(AttributeType.Endurance)), CombatFormulas.BaseDamage(c, cat.GetLevel(AttributeType.Strength)));
            var equipped = Enumerable.Range(0, Cat.SkillSlotCount).Select(cat.GetSkillInSlot).ToList();
            _slots.text = Loc.T("cat_details_sheet.04") + string.Join(" | ", equipped.Select(s => s == null ? Loc.T("cat_details_sheet.05") : Texts.SkillName(s.Value))) + Loc.T("cat_details_sheet.06");

            for (var i = _skills.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_skills.GetChild(i).gameObject);
            var skills = config.Skills.Where(s => !s.IsInnate).ToList();
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var unlocked = CatRules.IsSkillUnlocked(cat, skill);
                var slot = equipped.IndexOf(skill.Id);
                var state = !unlocked ? Loc.T("cat_details_sheet.07", Texts.Attribute(skill.UnlockAttribute), skill.UnlockLevel) : slot >= 0 ? Loc.T("cat_details_sheet.08", slot + 1) : Loc.T("cat_details_sheet.09");
                var color = !unlocked ? Palette.Panel : slot >= 0 ? Palette.Primary : Palette.Secondary;
                var skillId = skill.Id;
                var row = Ui.Button(_skills, "Skill_" + skill.Id, Loc.T("cat_details_sheet.10", Texts.SkillName(skill.Id), state, Texts.SkillDescription(skill), skill.GritCost),
                    color, () => Toggle(skillId), 30);
                row.Text.Align(TextAnchor.MiddleLeft);
                row.Rect.Place(new Vector2(0, 1f - (i + 1f) / skills.Count), new Vector2(1, 1f - (float)i / skills.Count), new Vector2(0, 5), new Vector2(0, -5));
                row.Interactable = unlocked;
                row.Text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 12), new Vector2(-40, -12));
                if (!unlocked && Ui.Icon(row.Rect, "lock") is Image lockIcon)
                {
                    lockIcon.rectTransform.Place(new Vector2(1, 0.2f), new Vector2(1, 0.8f), new Vector2(-80, 0), new Vector2(-24, 0));
                    row.Text.rectTransform.offsetMax = new Vector2(-90, row.Text.rectTransform.offsetMax.y);
                }
            }
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
