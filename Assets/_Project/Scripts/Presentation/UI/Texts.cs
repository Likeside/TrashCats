using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Graybox Russian strings in one place. Moves to a localization table in M5 (keys already exist in the config).
    /// </summary>
    public static class Texts
    {
        public static string CatName(CatId id) => id.Value switch
        {
            "cat_ryzhy" => "Рыжий",
            "cat_shnyr" => "Шнырь",
            "cat_ponchik" => "Пончик",
            _ => id.Value,
        };

        public static string Attribute(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => "Сила",
            AttributeType.Agility => "Ловкость",
            _ => "Крепость",
        };

        public static string AttributeGenitive(AttributeType attribute) => attribute switch
        {
            AttributeType.Strength => "Силы",
            AttributeType.Agility => "Ловкости",
            _ => "Крепости",
        };

        public static string ActivityName(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => "Добыча еды",
            ActivityKind.Rummage => "Поиск хлама",
            ActivityKind.Sleep => "Сон",
            _ => "Тренировка: " + Attribute(a.TrainedAttribute),
        };

        public static string ActivityShort(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => "Еда",
            ActivityKind.Rummage => "Хлам",
            ActivityKind.Sleep => "Сон",
            _ => Attribute(a.TrainedAttribute),
        };

        public static string Doing(ActivityDefinition a) => a.Kind switch
        {
            ActivityKind.Scavenge => "Ищет еду",
            ActivityKind.Rummage => "Роется в хламе",
            ActivityKind.Sleep => "Спит",
            _ => "Качает " + AttributeGenitive(a.TrainedAttribute).ToLowerInvariant(),
        };

        public static string Output(ActivityDefinition a, int amount) => a.Kind switch
        {
            ActivityKind.Scavenge => $"+{amount} еды",
            ActivityKind.Rummage => $"+{amount} хлама",
            ActivityKind.Sleep => $"+{amount} энергии",
            _ => $"+{amount} опыта",
        };

        public static string UpgradeName(UpgradeId id) => id.Value switch
        {
            "upg_boxes" => "Коробки",
            "upg_dumpster" => "Мусорный бак",
            "upg_can" => "Банка",
            "upg_mouse" => "Мышь",
            "upg_wheel" => "Колесо",
            _ => id.Value,
        };

        public static string IdleBubble(IdleReason reason) => reason switch
        {
            IdleReason.Tired => "Устал",
            IdleReason.Hungry => "Голоден",
            IdleReason.SleepDone => "Выспался",
            IdleReason.AttributeMax => "MAX",
            IdleReason.AfterBattle => "После боя",
            _ => "",
        };

        public static string IdleStatus(IdleReason reason) => reason switch
        {
            IdleReason.Tired => "Без дела: устал",
            IdleReason.Hungry => "Без дела: голоден",
            IdleReason.SleepDone => "Выспался",
            IdleReason.AttributeMax => "Атрибут на максимуме",
            _ => "Без дела",
        };

        public static string Error(CommandError error) => error switch
        {
            CommandError.Tired => "Устал",
            CommandError.EnergyFull => "Полон сил",
            CommandError.StationBusy => "Занято",
            CommandError.AttributeMaxed => "MAX",
            CommandError.InBattle => "В бою",
            CommandError.NoFood => "Нет еды",
            CommandError.AlreadyFull => "Сыт",
            CommandError.NotEnoughJunk => "Не хватает хлама",
            CommandError.MaxLevel => "Максимум",
            CommandError.NotWorking => "Ничем не занят",
            CommandError.TerritoryLocked => "Территория закрыта",
            CommandError.InvalidSlot => "Все слоты заняты — сначала сними умение",
            CommandError.SkillLocked => "Умение ещё не открыто",
            CommandError.InvalidTeam => "Выбери от 1 до 3 котов",
            CommandError.CatTooTired => "Кот слишком устал (нужно 20 энергии)",
            CommandError.BattlePending => "Бой уже идёт",
            _ => error.ToString(),
        };

        public static string TerritoryName(TerritoryId id) => id.Value switch
        {
            "ter_backyard" => "Задний двор",
            "ter_fishshop" => "Рыбная лавка",
            "ter_alley" => "Тёмный переулок",
            "ter_park" => "Городской парк",
            "ter_rooftop" => "Крыша ресторана",
            _ => id.Value,
        };

        public static string EnemyName(string id) => id switch
        {
            "enemy_rat" => "Крыса Хвост",
            "enemy_gull" => "Чайка",
            "enemy_shopcat" => "Кот Сёмга",
            "enemy_crow" => "Ворона",
            "enemy_bigrat" => "Крыса-громила",
            "enemy_crow_boss" => "Ворона-атаман",
            "enemy_raccoon" => "Енот",
            "enemy_pug" => "Мопс",
            "enemy_admiral" => "Адмирал",
            _ => CatName(new CatId(id)),
        };

        public static string SkillName(SkillId id) => id.Value switch
        {
            "skill_scratch" => "Царапина",
            "skill_bite" => "Укус",
            "skill_double_paw" => "Двойная лапа",
            "skill_thick_fur" => "Густая шерсть",
            "skill_dodge" => "Уворот",
            "skill_dirty_trick" => "Грязный трюк",
            _ => id.Value,
        };

        public static string SkillDescription(SkillDefinition s) => s.Effect switch
        {
            SkillEffectKind.Damage when s.Hits > 1 => $"{s.Hits} удара по {s.DamagePermille / 10}% урона",
            SkillEffectKind.Damage => s.DamagePermille > 1000 ? $"Урон ×{s.DamagePermille / 1000f:0.#}, точность {s.HitModifierPermille / 10}%" : "Обычная атака",
            SkillEffectKind.DamageReduction => $"{s.EffectCount} удара по вам: урон −{100 - s.EffectPermille / 10}%",
            SkillEffectKind.GuaranteedEvade => "Следующая атака врага промахнётся",
            _ => $"Враг теряет {s.EffectPermille / 10}% точности на {s.EffectCount} действия",
        };

        public static string Bonus(TerritoryDefinition t) => t.Bonus switch
        {
            TerritoryBonusKind.SleepEnergyFlat => $"Сон +{t.BonusValue} энергии",
            TerritoryBonusKind.FoodOutputPct => $"Добыча еды +{t.BonusValue}%",
            TerritoryBonusKind.JunkOutputPct => $"Поиск хлама +{t.BonusValue}%",
            TerritoryBonusKind.TrainingXpPct => $"Тренировки +{t.BonusValue}% опыта",
            _ => t.IsFinal ? "Главная банда района" : "",
        };

        public static string Stats(int s, int a, int t) => $"С{s} Л{a} К{t}";

        public static string Seconds(long ms) => $"{ms / 1000} с";

        public static string Duration(long ms)
        {
            var minutes = ms / 60_000;
            return minutes > 0 ? $"{minutes} мин" : $"{ms / 1000} с";
        }
    }
}
