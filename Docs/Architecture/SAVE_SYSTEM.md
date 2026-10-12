# Trash Cats — Система сохранений

> Статус: черновик v0.1 (фаза D).

## 1. Требования
1. Один локальный слот, без облака и бэкенда.
2. Сохранение должно точно восстанавливать идущие занятия (до миллисекунды игрового времени).
3. Выживание при убийстве процесса и сбоях записи (не более ~30 с потери прогресса).
4. Отсутствие эксплойтов «перезапуск ради другого исхода боя».
5. Возможность менять формат между версиями прототипа без сброса прогресса тестеров.

## 2. Формат
- **JSON**, `com.unity.nuget.newtonsoft-json` (официальный пакет Unity, совместим с IL2CPP при наличии `link.xml` для DTO-сборки).
- Файл: `Application.persistentDataPath/save.json`, бэкап: `save.bak.json`.
- Кодировка UTF-8, без форматирования в Release, с отступами в Development (удобно читать при отладке).
- Без шифрования (прототип). Защита от правки файла не требуется.

### 2.1. Пример (v1)
```json
{
  "Version": 1,
  "SavedAtUtcMs": 1791820800000,
  "GameTimeMs": 1534200,
  "SaveSeed": 918273645,
  "Food": 12,
  "Junk": 7,
  "Cats": [
    {
      "Id": "cat_ryzhy",
      "Levels": [5, 2, 3], "Xp": [12, 4, 0],
      "Energy": 41, "Satiety": 58,
      "Skills": ["skill_bite", null, null],
      "Activity": { "State": "Working", "ActivityId": "act_train_str",
                    "CycleStartMs": 1521000, "CycleDurationMs": 30000, "HungryOverride": false }
    },
    {
      "Id": "cat_shnyr",
      "Levels": [3, 5, 3], "Xp": [0, 20, 0],
      "Energy": 15, "Satiety": 70,
      "Skills": ["skill_double_paw", null, null],
      "Activity": { "State": "Idle", "IdleReason": "Tired" }
    }
  ],
  "Upgrades": { "upg_can": 1 },
  "Territories": {
    "ter_backyard": { "Status": "Captured", "Attempts": 1 },
    "ter_fishshop": { "Status": "Available", "Attempts": 0 }
  },
  "Tutorial": { "Completed": ["t_scavenge", "t_feed", "t_train"], "Skipped": false },
  "PendingBattle": null,
  "Flags": { "FinalReached": false },
  "Settings": { "BattleSpeed": 2, "SfxVolume": 1.0, "MusicVolume": 0.7 }
}
```
Непоказанные территории имеют статус `Locked` по умолчанию; отсутствующие ключи заполняются значениями по умолчанию при загрузке.

### 2.2. PendingBattle
Сохраняется **целиком, включая лог**, — несколько сотен событий, ~20–60 КБ JSON. Это проще и надёжнее, чем пересимулировать бой при загрузке: результат не изменится, даже если между версиями поменялся баланс.

## 3. Маппинг
```
SaveMapper.ToSave(GameWorld, Settings, utcNow) → SaveData
SaveMapper.FromSave(SaveData, GameConfig) → (GameWorld, Settings, warnings)
```
- Неизвестный id кота/умения/территории (удалён из конфига) → пропустить запись + предупреждение.
- Неизвестное занятие у `Working` → `Idle(None)`.
- Значения вне диапазона → обрезка + предупреждение.
- После маппинга — проверка инвариантов ([DATA_MODELS.md](DATA_MODELS.md#2-состояние-domain)). Нарушение → попытка исправления (например, два кота на станции → второй `Idle`), иначе загрузка бэкапа.

## 4. Версионирование и миграции
```
Load():
    json = storage.Read("save") ?? storage.Read("save.bak") ?? return NewGame()
    root = JObject.Parse(json)
    v = root["Version"]
    while v < CurrentVersion:
        root = Migrations[v](root)        // чистые функции JObject → JObject
        v++
    return root.ToObject<SaveData>()
```
- Каждая миграция — отдельная функция с тестом «старый JSON-образец → новый».
- Образцы сохранений каждой версии лежат в `Tests/EditMode/SaveSamples/`.
- Версия выше текущей (откат билда) → не загружать, показать «Сохранение из более новой версии», предложить новую игру (сохранив файл как `save.future.json`).

## 5. Когда сохраняем

| Триггер | Тип |
|---|---|
| `StartBattle` (после симуляции) | Немедленно |
| `CompleteBattle` | Немедленно |
| `BuyUpgrade`, вербовка, захват территории | Немедленно |
| Прочие команды (занятия, кормление, слоты) и завершения циклов при тике | Отложенно: «грязный» флаг → сохранение не чаще раза в 5 с (`SaveScheduler`). Отдельный 30-секундный автосейв не нужен — троттлинг его покрывает |
| `OnApplicationPause(true)`, `OnApplicationQuit` | Немедленно, синхронно |

`SaveScheduler` (Application) решает **когда**, `ISaveStorage` (Infrastructure) делает **как**.

## 6. Надёжная запись
```
JsonFileSaveStorage.Write(json):
    write json → save.tmp.json; flush to disk
    if save.json exists: copy save.json → save.bak.json
    replace save.json ← save.tmp.json        // File.Replace / Move с перезаписью
```
- Сериализация — в главном потоке (мир не потокобезопасен; занимает < 1 мс). Запись файла — тоже синхронная (файл маленький, < 100 КБ). Если профилирование покажет фризы, запись переносится в фон через UniTask, а сериализация остаётся в главном потоке.
- Ошибка записи (нет места и т.п.) → лог + тост «Не удалось сохранить», игра продолжается.

## 7. Новая игра и сброс
- `NewGame`: `SaveSeed = случайный int` (единственное место, где используется недетерминированный источник), стартовые ресурсы, Рыжий, `ter_backyard = Available`.
- Сброс (меню настроек + debug): удалить `save.json`, `save.bak.json` → `NewGame`.

## 8. Тесты
| Тест | Что проверяет |
|---|---|
| Round-trip | `FromSave(ToSave(world)) == world` для мира в разных состояниях (идёт цикл, бой в ожидании, финал) |
| Восстановление цикла | Сохранили посреди цикла → загрузили → `AdvanceTo` даёт тот же результат, что без перезапуска |
| Pending battle | Сохранили с `PendingBattle` → загрузили → `CompleteBattle` применяет те же награды |
| Битый файл | Мусор в `save.json` → загружается бэкап; оба битые → новая игра |
| Миграции | Каждый образец старой версии мигрирует и проходит инварианты |
| Неизвестные id | Удалённый из конфига id не роняет загрузку |
| Атомарность | Симуляция сбоя между записью tmp и заменой → валидный файл остаётся |

## 9. Реализация (M2-04)
- DTO: `App/Save/SaveData.cs`; маппинг с ремонтом: `SaveMapper`; JSON и миграции: `SaveJson` (Newtonsoft в сборке `TrashCats.App`, без UnityEngine).
- Загрузка: `SaveService.LoadOrCreate()` → основной файл → бэкап → новая игра; файл из будущей версии откладывается как `save.future.v<N>.json`.
- Перед записью сессия догоняет время (`Tick`), `SavedAtUtcMs` = момент последнего тика — прошедшее время не теряется.
- Поля `Territories`, `Tutorial`, `PendingBattle`, `Flags` добавляются в DTO вместе с соответствующими системами (M4/M5); старые файлы загружаются с значениями по умолчанию.
