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
    /// Cat details (Docs/GDD/UI_UX.md §5). A header with the avatar, name, aptitude plate and a close cross; then three
    /// framed blocks: attributes (icon, level, XP bar), combat stats with the attribute each one comes from, and skills —
    /// the slots as sockets (numbered; empty ones say so), the always-on basic hit, a hint, the skills the cat can take
    /// (an equipped one carries its slot number) and, under a divider, the locked ones in compact rows with their progress.
    /// </summary>
    public sealed class CatDetailsSheet
    {
        private const float Side = 50f;
        private const float SlotHeight = 120f;
        private const float RowHeight = 124f;
        private const float LockedRowHeight = 78f;

        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly Characters.CharacterViewFactory _art;
        private readonly GameObject _root;
        private readonly Image _avatar;
        private readonly TMP_Text _header;
        private readonly RectTransform _aptitude;
        private readonly RectTransform _attributes;
        private readonly RectTransform _combat;
        private readonly RectTransform _slots;
        private readonly RectTransform _innate;
        private readonly RectTransform _skills;
        private CatId? _catId;

        public bool IsOpen => _catId != null;

        public CatDetailsSheet(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult,
            Characters.CharacterViewFactory art = null)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;
            _art = art;

            var dim = Ui.Panel(parent, "CatDetails", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            _root = dim.gameObject;
            var panel = Ui.Card(dim.transform, "Sheet", CardStyle.Paper, raycast: true);
            panel.rectTransform.Place(Vector2.zero, new Vector2(1, 0.88f));

            // header: avatar, name, aptitude plate, close cross
            _avatar = Ui.Panel(panel.transform, "Avatar", Color.white);
            _avatar.preserveAspect = true;
            _avatar.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Side, -206), new Vector2(Side + 160, -46));
            _header = Ui.Label(panel.transform, "", 50, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            _header.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(Side + 186, -112), new Vector2(-150, -46));
            var plate = Ui.Panel(panel.transform, "AptitudePlate", Ui.Skin?.barFrame != null ? Color.white : new Color(0, 0, 0, 0.5f));
            plate.sprite = Ui.Skin?.barFrame;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 2.4f;
            plate.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(Side + 180, -196), new Vector2(-150, -124));
            _aptitude = Info.Row(plate.transform, "Aptitude", 10f, TextAnchor.MiddleLeft);
            _aptitude.Place(Vector2.zero, Vector2.one, new Vector2(24, 6), new Vector2(-20, -6));

            var close = Ui.Button(panel.transform, "Details_Close", "×", Palette.Disabled, Close, 60);
            close.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-136, -146), new Vector2(-40, -50));
            close.Text.rectTransform.Fill(4);

            // attributes
            var attributes = Block(panel.transform, "AttributesBlock", Loc.T("cat_details_sheet.18"), -222, -486);
            _attributes = Info.Column(attributes, "Attributes", 6f);
            _attributes.Place(Vector2.zero, Vector2.one, new Vector2(30, 16), new Vector2(-30, -60));

            // combat: what the attributes give in a fight
            var combat = Block(panel.transform, "CombatBlock", Loc.T("cat_details_sheet.19"), -496, -676);
            _combat = Info.Row(combat, "Combat", 10f, TextAnchor.MiddleCenter);
            _combat.Place(Vector2.zero, Vector2.one, new Vector2(24, 14), new Vector2(-24, -58));
            var layout = _combat.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;

            // skills: sockets, the basic hit, a hint, the list
            var skills = Block(panel.transform, "SkillsBlock", Loc.T("cat_details_sheet.20"), -686, 30, fromBottom: true);
            _slots = Info.Row(skills, "Slots", 16f, TextAnchor.MiddleLeft);
            _slots.Place(new Vector2(0, 1), Vector2.one, new Vector2(30, -60 - SlotHeight), new Vector2(-30, -60));
            var slotsLayout = _slots.GetComponent<HorizontalLayoutGroup>();
            slotsLayout.childForceExpandWidth = true;
            _innate = Info.Row(skills, "Innate", 10f, TextAnchor.MiddleLeft);
            _innate.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -60 - SlotHeight - 56), new Vector2(-30, -60 - SlotHeight - 8));
            var hint = Ui.Label(skills, Loc.T("cat_details_sheet.21"), 24, TextAnchor.MiddleLeft, Palette.Ink, heading: false);
            hint.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -60 - SlotHeight - 100), new Vector2(-30, -60 - SlotHeight - 58));
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 18;
            hint.fontSizeMax = 24;
            _skills = Info.Column(skills, "Skills", 8f);
            _skills.Place(Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-24, -60 - SlotHeight - 108));

            _root.SetActive(false);
        }

        /// <summary>A framed group with a title in its top left corner; returns the frame to fill.</summary>
        private static RectTransform Block(Transform panel, string name, string title, float top, float bottom, bool fromBottom = false)
        {
            var frame = Ui.Panel(panel, name, Ui.Skin?.catCard != null ? Color.white : new Color(0, 0, 0, 0.06f));
            if (Ui.Skin?.catCard != null)
            {
                frame.sprite = Ui.Skin.catCard;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.2f;
            }
            if (fromBottom)
                frame.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(Side - 20, bottom), new Vector2(-Side + 20, top));
            else
                frame.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(Side - 20, bottom), new Vector2(-Side + 20, top));
            var label = Ui.Label(frame.transform, title, 30, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            label.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -58), new Vector2(-30, -14));
            return frame.rectTransform;
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
            var viewKey = config.GetCat(id).ViewKey;
            var avatar = viewKey != null ? _art?.Avatar(viewKey) ?? _art?.Portrait(viewKey) : null;
            _avatar.sprite = avatar;
            _avatar.enabled = avatar != null;
            _header.text = Texts.CatName(id);

            // aptitude: what it means, not just its name
            Clear(_aptitude);
            Info.Icon(_aptitude, Texts.AttributeIcon(cat.Aptitude), 48f);
            Info.Text(_aptitude, Loc.T("cat_details_sheet.24", Texts.Attribute(cat.Aptitude), config.Progression.AptitudeBonusPct), 28, Palette.Paper)
                .Outlined();

            // attributes: icon, name, level, XP towards the next level on a bar
            Clear(_attributes);
            foreach (var a in AttributeTypes.All)
            {
                var level = cat.GetLevel(a);
                var max = level >= config.Progression.MaxLevel;
                var next = max ? 1 : config.Progression.XpToNext(level);
                var xp = max ? next : cat.GetXp(a);
                Info.AttributeRow(_attributes, Texts.AttributeIcon(a), Texts.Attribute(a), level, (float)xp / next, max ? "MAX" : $"{xp}/{next}", a == cat.Aptitude, 62f);
            }

            // combat stats, each with the attribute it grows from
            var c = config.Combat;
            var strength = cat.GetLevel(AttributeType.Strength);
            var technique = cat.GetLevel(AttributeType.Technique);
            var endurance = cat.GetLevel(AttributeType.Endurance);
            var interval = CombatFormulas.IntervalTicks(c, technique) * c.TickMs / 1000f;
            Clear(_combat);
            // one attribute — one stat, each level +10% of its base, in the attributes' order
            CombatStat("skill_scratch", Loc.T("cat_details_sheet.13", CombatFormulas.BaseDamage(c, strength)), AttributeType.Strength, strength);
            CombatStat("speed", Loc.T("cat_details_sheet.26", interval.ToString("0.##")), AttributeType.Technique, technique);
            CombatStat("hp", $"HP {CombatFormulas.MaxHp(c, endurance)}", AttributeType.Endurance, endurance);
            CombatStat("paw", Loc.T("cat_details_sheet.12", c.GritMax), null, 0);

            // the slots as sockets
            var equipped = Enumerable.Range(0, Cat.SkillSlotCount).Select(cat.GetSkillInSlot).ToList();
            Clear(_slots);
            for (var i = 0; i < equipped.Count; i++)
                Slot(i, equipped[i]);
            Clear(_innate);
            var innate = config.Skills.FirstOrDefault(s => s.IsInnate);
            if (innate != null)
            {
                Info.Icon(_innate, Texts.SkillIcon(innate.Id), 40f);
                Info.Text(_innate, Loc.T("cat_details_sheet.25", Texts.SkillName(innate.Id)), 24, Palette.Ink, heading: false);
            }

            // the list: what can be taken, then the locked ones under a divider
            Clear(_skills);
            var skills = config.Skills.Where(s => !s.IsInnate).ToList();
            var open = skills.Where(s => CatRules.IsSkillUnlocked(cat, s)).ToList();
            var locked = skills.Except(open).ToList();
            foreach (var skill in open)
                SkillRow(skill, equipped.IndexOf(skill.Id));
            if (locked.Count > 0)
            {
                var divider = Info.Text(_skills, Loc.T("cat_details_sheet.22"), 26, Palette.Ink, height: 40f);
                divider.alignment = TextAlignmentOptions.MidlineLeft;
                foreach (var skill in locked)
                    LockedRow(cat, skill);
            }
        }

        /// <summary>One combat stat: icon and value, and under it the attribute it comes from (grit: built up in the fight).</summary>
        private void CombatStat(string icon, string value, AttributeType? source, int level)
        {
            var cell = Info.Column(_combat, "Stat_" + icon, 2f);
            cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var top = Info.Row(cell, "Value", 6f, TextAnchor.MiddleCenter);
            Info.Size(top, -1, 54);
            Info.Chip(top, icon, value, 48f, 30, Palette.Ink);
            var from = Info.Row(cell, "From", 4f, TextAnchor.MiddleCenter);
            Info.Size(from, -1, 36);
            if (source is AttributeType attribute)
            {
                Info.Text(from, "←", 22, Palette.Ink, heading: false);
                Info.Chip(from, Texts.AttributeIcon(attribute), $"{level} · +{CombatFormulas.LevelPercent(_session.Config.Combat, level) - 100}%", 32f, 22, Palette.Ink, heading: false);
            }
            else
                Info.Text(from, Loc.T("cat_details_sheet.27"), 22, Palette.Ink, heading: false);
        }

        /// <summary>A socket: dark frame with its number; the equipped skill (tap to take it off) or "+ Пусто".</summary>
        private void Slot(int index, SkillId? skill)
        {
            var socket = Ui.Panel(_slots, "Slot" + index, Ui.Skin?.barFrame != null ? Color.white : Palette.PanelDark, raycast: skill != null);
            socket.sprite = Ui.Skin?.barFrame;
            socket.type = Image.Type.Sliced;
            socket.pixelsPerUnitMultiplier = 2f;
            if (skill == null) socket.color = new Color(1f, 1f, 1f, 0.45f);
            Info.Size(socket, -1, SlotHeight);
            var content = Info.Row(socket.transform, "Content", 8f, TextAnchor.MiddleCenter);
            content.Place(Vector2.zero, Vector2.one, new Vector2(46, 10), new Vector2(-16, -10));
            if (skill is SkillId taken)
            {
                Info.Icon(content, Texts.SkillIcon(taken), 66f);
                var name = Info.Text(content, Texts.SkillName(taken), 24, Palette.Paper);
                name.Outlined();
                name.textWrappingMode = TextWrappingModes.Normal;
                Info.Size(name, 120, 90);
                socket.gameObject.AddComponent<Button>().onClick.AddListener(() => Toggle(taken));
            }
            else
                Info.Text(content, "+ " + Loc.T("cat_details_sheet.23"), 28, Palette.Paper).Outlined();
            Number(socket.rectTransform, index, skill != null);
        }

        /// <summary>Slot number in a small disc in the top left corner (the same disc marks the skill in the list).</summary>
        private static void Number(RectTransform parent, int index, bool taken)
        {
            var disc = Ui.Panel(parent, "Number", taken ? Palette.Primary : Palette.Disabled);
            disc.sprite = Ui.Skin?.circle;
            disc.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(-8, -36), new Vector2(36, 8));
            var label = Ui.Label(disc.transform, (index + 1).ToString(), 26, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            label.rectTransform.Fill();
        }

        /// <summary>A skill the cat can take: icon, name, what it does, grit cost; an equipped one is yellow with its slot number.</summary>
        private void SkillRow(SkillDefinition skill, int slot)
        {
            var skillId = skill.Id;
            var row = Ui.Button(_skills, "Skill_" + skill.Id, "", slot >= 0 ? Palette.Primary : Palette.Secondary, () => Toggle(skillId), 30);
            Info.Size(row.Image, -1, RowHeight);
            row.Text.gameObject.SetActive(false);

            var icon = Ui.Icon(row.Rect, Texts.SkillIcon(skill.Id));
            icon?.rectTransform.Place(new Vector2(0, 0.12f), new Vector2(0, 0.88f), new Vector2(34, 0), new Vector2(144, 0));
            if (slot >= 0)
                Number(row.Rect, slot, true);

            var title = Ui.Label(row.Rect, Texts.SkillName(skill.Id), 32, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            title.rectTransform.Place(new Vector2(0, 0.52f), new Vector2(1, 1), new Vector2(166, 0), new Vector2(-170, -12));
            var description = Ui.Label(row.Rect, Texts.SkillDescription(skill), 24, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            description.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0.52f), new Vector2(166, 14), new Vector2(-170, 0));
            description.enableAutoSizing = true;
            description.fontSizeMin = 18;
            description.fontSizeMax = 24;
            Cost(row.Rect, skill);
            foreach (var label in row.Rect.GetComponentsInChildren<TMP_Text>(true))
                label.Outlined();
        }

        /// <summary>A locked skill in one compact line: faded icon, name, lock, the attribute's progress, grit cost.</summary>
        private void LockedRow(IReadOnlyCat cat, SkillDefinition skill)
        {
            var row = Ui.Panel(_skills, "Skill_" + skill.Id, Ui.Skin?.button != null ? Palette.Disabled : Palette.Panel);
            if (Ui.Skin?.button != null)
            {
                row.sprite = Ui.Skin.button;
                row.type = Image.Type.Sliced;
                row.gameObject.AddComponent<SliceFit>();
            }
            Info.Size(row, -1, LockedRowHeight);
            var line = Info.Row(row.transform, "Line", 10f, TextAnchor.MiddleLeft);
            line.Place(Vector2.zero, Vector2.one, new Vector2(30, 6), new Vector2(-150, -6));
            var icon = Info.Icon(line, Texts.SkillIcon(skill.Id), 56f);
            if (icon != null) icon.color = new Color(1f, 1f, 1f, 0.5f);
            Info.Text(line, Texts.SkillName(skill.Id), 26, Palette.Paper).Outlined();
            Info.Icon(line, "lock", 36f);
            Info.Icon(line, Texts.AttributeIcon(skill.UnlockAttribute), 36f);
            var have = cat.GetLevel(skill.UnlockAttribute);
            var bar = Ui.Bar(line, "Progress", Palette.Progress);
            Info.Size(bar.Rect, 150, 34);
            bar.Set((float)have / Mathf.Max(1, skill.UnlockLevel), $"{have}/{skill.UnlockLevel}");
            Cost(row.rectTransform, skill);
            foreach (var label in row.GetComponentsInChildren<TMP_Text>(true))
                label.Outlined();
        }

        private static void Cost(RectTransform row, SkillDefinition skill)
        {
            var cost = Info.Row(row, "Cost", 4f, TextAnchor.MiddleRight);
            cost.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, -36), new Vector2(-30, 36));
            Info.Chip(cost, "paw", skill.GritCost.ToString(), 50f, 30, Palette.Paper);
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
                if (free < 0)
                {
                    _onResult(CommandResult.Fail(CommandError.InvalidSlot));
                    return;
                }
                result = _session.EquipSkill(_catId.Value, free, skill);
            }

            _onResult(result);
            Refresh();
        }
    }
}
