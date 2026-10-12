using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    public enum SkillEquipError
    {
        None = 0,
        InvalidSlot,
        UnknownSkill,
        SkillLocked,
        NotEquippable,
        AlreadyEquipped,
    }

    /// <summary>Rules for a single cat: needs, experience, skill unlocks and slots.</summary>
    public static class CatRules
    {
        public static bool IsTired(IReadOnlyCat cat, GameConfig config) => cat.Energy < config.Needs.TiredThreshold;
        public static bool IsHungry(IReadOnlyCat cat, GameConfig config) => cat.Satiety < config.Needs.HungryThreshold;

        public static bool IsSkillUnlocked(IReadOnlyCat cat, SkillDefinition skill) =>
            skill.IsInnate || cat.GetLevel(skill.UnlockAttribute) >= skill.UnlockLevel;

        /// <summary>Applies need deltas clamped to 0..Max. Emits <see cref="NeedsChanged"/> if anything changed.</summary>
        public static void ChangeNeeds(Cat cat, int energyDelta, int satietyDelta, GameConfig config, List<DomainEvent> events)
        {
            var max = config.Needs.Max;
            var energy = Math.Clamp(cat.Energy + energyDelta, 0, max);
            var satiety = Math.Clamp(cat.Satiety + satietyDelta, 0, max);
            if (energy == cat.Energy && satiety == cat.Satiety)
                return;

            cat.SetNeeds(energy, satiety);
            events.Add(new NeedsChanged(cat.Id, energy, satiety));
        }

        /// <summary>
        /// Adds experience, levelling up as many times as it covers. Experience above the max level is discarded.
        /// Newly unlocked skills are announced and auto-equipped into a free slot.
        /// </summary>
        public static void AddXp(Cat cat, AttributeType attribute, int amount, GameConfig config, List<DomainEvent> events)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            var progression = config.Progression;
            var level = cat.GetLevel(attribute);
            if (level >= progression.MaxLevel || amount == 0)
                return;

            var xp = cat.GetXp(attribute) + amount;
            while (level < progression.MaxLevel && xp >= progression.XpToNext(level))
            {
                xp -= progression.XpToNext(level);
                level++;
                cat.SetProgress(attribute, level, level >= progression.MaxLevel ? 0 : xp);
                events.Add(new AttributeLevelUp(cat.Id, attribute, level));
                UnlockSkillsAt(cat, attribute, level, config, events);
            }

            cat.SetProgress(attribute, level, level >= progression.MaxLevel ? 0 : xp);
        }

        public static SkillEquipError CanEquip(IReadOnlyCat cat, int slot, SkillId? skillId, GameConfig config)
        {
            if (slot < 0 || slot >= Cat.SkillSlotCount) return SkillEquipError.InvalidSlot;
            if (skillId == null) return SkillEquipError.None;
            if (!config.TryGetSkill(skillId.Value, out var skill)) return SkillEquipError.UnknownSkill;
            if (skill.IsInnate) return SkillEquipError.NotEquippable;
            if (!IsSkillUnlocked(cat, skill)) return SkillEquipError.SkillLocked;

            for (var i = 0; i < Cat.SkillSlotCount; i++)
                if (i != slot && cat.GetSkillInSlot(i) == skillId)
                    return SkillEquipError.AlreadyEquipped;

            return SkillEquipError.None;
        }

        /// <summary>Puts a skill into a slot (or clears it with null). Throws if <see cref="CanEquip"/> fails.</summary>
        public static void Equip(Cat cat, int slot, SkillId? skillId, GameConfig config, List<DomainEvent> events)
        {
            var error = CanEquip(cat, slot, skillId, config);
            if (error != SkillEquipError.None)
                throw new InvalidOperationException($"Cannot equip {skillId} into slot {slot} of {cat.Id}: {error}.");
            if (cat.GetSkillInSlot(slot) == skillId)
                return;

            cat.SetSkillInSlot(slot, skillId);
            events.Add(new SkillEquipped(cat.Id, slot, skillId));
        }

        private static void UnlockSkillsAt(Cat cat, AttributeType attribute, int level, GameConfig config, List<DomainEvent> events)
        {
            foreach (var skill in config.Skills)
            {
                if (skill.IsInnate || skill.UnlockAttribute != attribute || skill.UnlockLevel != level)
                    continue;

                events.Add(new SkillUnlocked(cat.Id, skill.Id));
                TryAutoEquip(cat, skill.Id, events);
            }
        }

        internal static void TryAutoEquip(Cat cat, SkillId skillId, List<DomainEvent> events)
        {
            for (var slot = 0; slot < Cat.SkillSlotCount; slot++)
            {
                if (cat.GetSkillInSlot(slot) != null)
                    continue;

                cat.SetSkillInSlot(slot, skillId);
                events?.Add(new SkillEquipped(cat.Id, slot, skillId));
                return;
            }
        }
    }
}
