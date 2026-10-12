# Trash Cats — Модели данных

> Статус: черновик v0.1 (фаза D). Псевдокод на C#-подобном синтаксисе — иллюстрация структуры, не production-код.

Три вида данных, которые **никогда не смешиваются**:

| Вид | Где | Изменяемость | Пример |
|---|---|---|---|
| **Конфигурация** | `Domain/Config` (записи) ← `Infrastructure` (ScriptableObject) | Неизменяемая в рантайме | Стоимость улучшения, статы Крысы |
| **Состояние** | `Domain` | Меняется только командами и симуляцией | Энергия Рыжего, Хлам на складе |
| **Сохранение (DTO)** | `Application/Save` | Плоские сериализуемые классы | `SaveData v1` |

## 1. Конфигурация

### 1.1. Идентификаторы
Строковые id с префиксами (`cat_ryzhy`, `skill_bite`, `act_train_str`, `upg_can`, `ter_alley`, `enemy_rat`). В домене — обёртки-структуры для типобезопасности:
```
readonly struct CatId { string Value }   // аналогично SkillId, ActivityId, UpgradeId, TerritoryId, EnemyId
enum AttributeType { Strength, Agility, Toughness }
enum ResourceType  { Food, Junk }
```

### 1.2. Доменный конфиг (неизменяемые записи)
```
GameConfig {
    Needs:       NeedsConfig { Max=100, TiredThreshold=20, HungryThreshold=20, HungryDurationMultiplier=2,
                               SatietyPerFood=10, MinEnergyForBattle=20 }
    Time:        TimeConfig  { OfflineCapMs=3_600_000, OfflineSummaryMinMs=60_000 }
    Progression: ProgressionConfig { MaxLevel=10, XpBase=10, XpPerLevel=7, BaseTrainingXp=10,
                                     AptitudeBonusPct=50,
                                     BattleXpWin=8, BattleXpLoss=3 }
    Start:       StartConfig { Food=10, Junk=0, StartingCatId="cat_ryzhy" }
    Combat:      CombatConfig (см. 1.4)
    Cats:        IReadOnlyList<CatDefinition>
    Enemies:     IReadOnlyList<EnemyDefinition>
    Skills:      IReadOnlyList<SkillDefinition>
    Activities:  IReadOnlyList<ActivityDefinition>
    Upgrades:    IReadOnlyList<UpgradeDefinition>
    Territories: IReadOnlyList<TerritoryDefinition>   // в порядке открытия
}

CatDefinition {
    Id, NameKey, Aptitude: AttributeType,
    StartAttributes: (Str, Agi, Tou),
    StartEnergy, StartSatiety,
    ViewKey                      // ссылка на визуал (используется только Presentation)
}

ActivityDefinition {
    Id, Kind: Scavenge | Rummage | Train | Sleep,
    DurationMs, EnergyCost, SatietyCost,
    TrainsAttribute?: AttributeType,     // для Train
    StationUpgradeId?: UpgradeId,        // Банка/Мышь/Колесо — вместимость 1
    BaseOutput: int,                     // Еда / Хлам / Энергия сна
    Capacity: int?                       // null = без лимита, 1 = станция
}

UpgradeDefinition {
    Id, Costs: int[] { 15, 40 },
    Effect: SleepEnergyBonus(int[] {0,10,20})
          | ResourceOutputBonus(Food: {0,1,2}, Junk: {0,1,2})
          | TrainingXpBonusPct(Attribute, int[] {0,25,50})
}

TerritoryDefinition {
    Id, NameKey, EnemyQueue: EnemyId[],
    FirstReward: (Food, Junk), RepeatRewardPct=30,
    PassiveBonus?: SleepEnergyFlat(5) | ResourceOutputPct(Food, 25) | ResourceOutputPct(Junk, 25) | TrainingXpPct(10),
    RecruitCatId?: CatId,
    IsFinal: bool
}

EnemyDefinition { Id, NameKey, Str, Agi, Tou, Skills: SkillId[], HpMultiplierPermille=1000|1500, ViewKey }

SkillDefinition {
    Id, NameKey, GritCost, CooldownActions,
    Unlock?: (AttributeType, Level),      // null = всегда (Царапина)
    Effect: Damage(MultPermille, HitModPermille, Hits)
          | DamageReductionBuff(Permille=600, Charges=3)
          | GuaranteedEvadeBuff(Charges=1)
          | AccuracyDebuff(Permille=-200, Actions=3),
    AiPriority: int                       // 1..6 по таблице GDD
}
```

### 1.3. Слой ScriptableObject (Infrastructure)
- `GameConfigAsset` (один корневой SO) ссылается на списки `CatDefinitionAsset`, `SkillDefinitionAsset` и т.д.
- `ConfigLoader.Load(GameConfigAsset) → GameConfig` — маппинг 1:1 + `Validate()`.
- Визуальные ссылки (спрайты, префабы, звуки) живут **только** в SO/Presentation-реестрах (`CatViewRegistry`), домен видит лишь `ViewKey`.
- Для тестов `GameConfig` создаётся билдером в коде (`TestConfigs.Default()`), с теми же значениями, что в ассетах. Отдельный тест сверяет, что ассеты и билдер совпадают, чтобы баланс тестов не расходился с игрой.

### 1.4. Боевой конфиг (целые числа)
```
CombatConfig {
    TickMs=100,
    HpBase=40, HpPerTou=12,
    GritBase=20, GritPerTou=5, GritRegenPerAction=3,
    DamageBase=4, DamagePerStr=2,
    IntervalBaseTicks=16, IntervalAgiDivisor=2, IntervalMinTicks=10,
    HitBasePermille=750, HitPerAgiDiffPermille=40, HitMinPermille=350, HitMaxPermille=950,
    VarianceMinPermille=850, VarianceMaxPermille=1150,
    SuddenDeathAfterTicks=450, SuddenDeathMultPermille=2000,
    FighterEntryPauseTicks=10,
    SafetyMaxTicks=10_000
}
```
Все множители — в промилле (‰), все вычисления — `int`/`long`. Деление округляется явно: `(a * b + 500) / 1000`.

## 2. Состояние (Domain)

```
GameWorld : IReadOnlyGameWorld {
    long GameTimeMs
    int  SaveSeed
    List<Cat> Cats                       // только завербованные
    Wallet Wallet                        // Food, Junk
    UpgradeLevels Upgrades               // Dictionary<UpgradeId,int>
    TerritoryProgress Territories        // Dictionary<TerritoryId, TerritoryState>
    TutorialState Tutorial               // HashSet<string> CompletedSteps, bool Skipped
    PendingBattle? PendingBattle
    GameFlags Flags                      // FinalReached, ...
}

Cat {
    CatId Id
    AttributeState[3] Attributes         // { Level, Xp }
    int Energy, Satiety
    SkillId?[3] EquippedSkills
    ActivityState Activity
}

ActivityState =                          // размеченное объединение
    Idle    { IdleReason Reason }        // None | Tired | Hungry | SleepDone | AttributeMax | Stopped | AfterBattle
  | Working { ActivityId Activity, long CycleStartMs, long CycleDurationMs, bool HungryOverride }
  | InBattle

TerritoryState { TerritoryStatus Status /*Locked|Available|Captured*/, int Attempts }

PendingBattle {
    TerritoryId Territory
    CatId[] Order
    int Seed
    BattleResult Result                  // см. ниже; сохраняется целиком
}
```

**Инварианты (проверяются в Debug-сборке после каждой команды и каждого `AdvanceTo`):**
1. На каждой станции с вместимостью 1 работает не больше одного кота.
2. `0 ≤ Energy, Satiety ≤ 100`; `Food, Junk ≥ 0`; `1 ≤ Level ≤ 10`.
3. `InBattle` ⇔ кот в `PendingBattle.Order`.
4. В слотах нет дублей и неоткрытых умений.
5. `Working.CycleStartMs ≤ GameTimeMs < CycleStartMs + CycleDurationMs` после `AdvanceTo`.
6. Ровно одна территория в статусе `Available` (до финала).

### 2.1. Боевые модели
```
FighterSnapshot { string Id; Side Side; int Str, Agi, Tou; SkillId[] Skills; int HpMultiplierPermille }
BattleSetup     { FighterSnapshot[] PlayerQueue; FighterSnapshot[] EnemyQueue }

// Внутреннее состояние бойца на ринге (не сохраняется)
FighterRuntime  { Snapshot; int Hp, MaxHp, Grit, MaxGrit, IntervalTicks, TimerTicks;
                  Dictionary<SkillId,int> Cooldowns; BuffState Buffs }

BattleResult {
    BattleOutcome Outcome              // Victory | Defeat
    string[] FoughtPlayerIds           // вышедшие на ринг
    int TotalTicks
    List<BattleEvent> Log
}

BattleEvent =                          // у каждого: int Tick
    FighterEntered { Side, FighterId, Hp, Grit }
  | SkillUsed      { Side, FighterId, SkillId }
  | AttackResolved { AttackerSide, Hit: bool, Damage, TargetHpAfter, HitIndex }
  | BuffApplied    { Side, BuffType, Charges }  | BuffExpired { Side, BuffType }
  | GritChanged    { Side, Grit }
  | SuddenDeath    { }
  | FighterKnockedOut { Side, FighterId }
  | BattleEnded    { Outcome }
```
Лог достаточен, чтобы презентация восстановила всё без доступа к симуляции.

## 3. Доменные события (для EventBus)

| Событие | Поля | Кто публикует |
|---|---|---|
| `ActivityStarted` | CatId, ActivityId, Start, Duration | Activities |
| `ActivityCycleCompleted` | CatId, ActivityId, Outputs (Food/Junk/Xp/Energy) | Activities |
| `ActivityStopped` | CatId, IdleReason | Activities |
| `NeedsChanged` | CatId, Energy, Satiety | Cats |
| `ResourcesChanged` | Food, Junk, Δ | Economy |
| `AttributeLevelUp` | CatId, Attribute, NewLevel | Cats |
| `SkillUnlocked` | CatId, SkillId | Cats |
| `SkillEquipped` | CatId, Slot, SkillId? | Cats |
| `UpgradePurchased` | UpgradeId, NewLevel | Upgrades |
| `BattleStarted` | TerritoryId, Order | BattleService |
| `BattleCompleted` | TerritoryId, Outcome, Rewards | BattleService |
| `TerritoryCaptured` | TerritoryId, NextUnlocked? | Territories |
| `CatRecruited` | CatId | Territories |
| `FinalReached` | — | Territories |
| `OfflineProgressApplied` | Summary | Time |
| `TutorialStepChanged` | StepId? | Tutorial |

События — неизменяемые записи (`record`/`readonly struct`). Порядок в пачке = порядок возникновения.

## 4. Сохранение (DTO)

DTO отделены от домена, чтобы рефакторинг доменных классов не ломал сохранения. Полная схема и версии — в [SAVE_SYSTEM.md](SAVE_SYSTEM.md).

```
SaveData {
    int Version = 1
    long SavedAtUtcMs
    long GameTimeMs
    int SaveSeed
    int Food, Junk
    List<CatSave> Cats
    Dictionary<string,int> Upgrades
    Dictionary<string,TerritorySave> Territories
    TutorialSave Tutorial
    PendingBattleSave? PendingBattle
    FlagsSave Flags
    SettingsSave Settings          // ×2 скорость боя, звук
}

CatSave {
    string Id
    int[] Levels; int[] Xp         // по AttributeType
    int Energy, Satiety
    string?[] Skills               // 3 слота
    ActivitySave Activity
}

ActivitySave { string State /*Idle|Working|InBattle*/; string? IdleReason;
               string? ActivityId; long CycleStartMs; long CycleDurationMs; bool HungryOverride }
```
