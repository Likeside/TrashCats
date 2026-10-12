# Trash Cats — Бриф по арт-дирекшну и план сравнения стилей

> Статус: черновик v0.1 (фаза F).
> **Генерация изображений НЕ разрешена.** Документ готовит будущую генерацию через OpenAI Image API. Вызов API, создание ассетов и выбор финального стиля — только после явного разрешения пользователя.

## 1. Цель фазы
Сравнить несколько визуальных стилей **на одном и том же игровом сценарии**, чтобы пользователь выбрал стиль по реальным критериям игры (читаемость, анимация, стоимость), а не по «красивой картинке». Итог фазы — выбранный стиль и Art Bible.

## 2. Требования к стилю (из GDD)

| # | Требование | Почему | Как проверяем на концепте |
|---|---|---|---|
| 1 | Коты читаются на телефоне | Панель котов ~150 px в высоту, арена — половина экрана | Уменьшить изображение до 25% и рассмотреть с расстояния вытянутой руки |
| 2 | Различимые силуэты трёх котов | Рыжий / Шнырь / Пончик должны узнаваться без цвета | Перевести концепт персонажей в чёрно-белый силуэт |
| 3 | Выразительная анимация при доступной цене | ~45 клипов на 5 ригов (CONTENT_SPEC §8) | Оценка: подходит ли стиль под скелетную 2D-анимацию или требует покадровой |
| 4 | Читаемый бой | Цифры урона, плашки умений, бары HP/Задора поверх арены | Концепт боя: понятно ли, кто бьёт, кто проигрывает |
| 5 | Модульное окружение | 5 объектов × 3 уровня + 6 фонов | Объекты убежища выглядят как отдельные элементы, а не нарисованы в фоне |
| 6 | Доступное производство | Маленькая команда, прототип | Сколько деталей на персонаже; есть ли сложное освещение |
| 7 | Единство UI и игры | UI-скин в том же языке, что и мир | Концепт UI-кита рядом с концептом убежища |
| 8 | Тон: смешной, тёплый, «уличный» | Юмор, не мрачность; без жестокости | Общее впечатление |

**Жёсткие ограничения:** без крови и реалистичного насилия; без реализма; портрет 9:16; фон убежища тёплый, «вечер за рестораном».

## 3. Стили-кандидаты

Это примеры из брифа, а не утверждённые варианты. Пользователь может убрать или добавить стиль до генерации.

| # | Стиль | Сильные стороны | Риски для Trash Cats | Технология анимации | Стоимость производства |
|---|---|---|---|---|---|
| S1 | **Cartoon 2D, жирный контур** | Максимальная читаемость, яркие силуэты, юмор | Может выглядеть «как все мобильные игры» | 2D-скелетная (Unity 2D Animation / Spine) — отлично | Низкая–средняя |
| S2 | **Детальный пиксель-арт** | Атмосфера свалки, ностальгия, ассоциация с Punch Club | Детали теряются на мелких элементах UI; покадровая анимация дорогая; масштабирование под разные экраны | Покадровая | Средняя–высокая (анимации) |
| S3 | **Стилизованный 2.5D, мягкий свет** | Объём, «премиальный» вид | Самый дорогой; 3D-пайплайн или сложный 2D-рендер; хуже читаемость силуэтов при мягком свете | 3D-риг или 2D с картами нормалей | Высокая |
| S4 | **Комикс, утрированные эмоции** | Юмор, выразительность, сильные позы для боя | Халфтон и штриховка шумят на мелких размерах | 2D-скелетная + замена спрайтов лиц | Средняя |
| S5 | **Hand-painted casual** | Тёплый уютный мир, хорош для фонов | Персонажи могут «тонуть» в фоне; кисть сложно держать единой | 2D-скелетная | Средняя |

**Рекомендация на первый раунд:** S1, S2, S4, S5. S3 (2.5D) — только если пользователь готов к самому дорогому пайплайну: для прототипа он противоречит столпу «маленький тестируемый скоуп». Решение за пользователем.

## 4. План сравнения

### 4.1. Раунды
| Раунд | Что генерируем | Стилей | Изображений | Цель |
|---|---|---|---|---|
| **1** | Линейка котов + экран убежища | 4 (или 5) | 8 (10) | Отсеять стили по читаемости персонажей и цельности сцены |
| **2** | Бой + тренировки + карта + UI-кит | 2 финалиста | 8 | Проверить бой, UI и модульность |
| **3** (опц.) | Повтор лучших с правками | 1 | 2–4 | Зафиксировать стиль для Art Bible |

Всего ~16–22 изображения. Стоимость оценим перед запуском по актуальному прайсу API.

### 4.2. Правила честного сравнения
1. **Один и тот же сценарий и композиция** для всех стилей (§5). Меняется только блок стиля.
2. Одинаковые параметры API: размер, качество, без прозрачности.
3. По 1 изображению на (стиль × сценарий) в первом раунде. Лучшая из нескольких попыток не выбирается, иначе сравнение нечестное. Если изображение явно сломано (не тот кадр, лишние коты), допускается одна перегенерация, и это отмечается.
4. Изображения показываются пользователю **группами по сценарию** (все линейки рядом, все убежища рядом) без подписи стиля. Подписи раскрываются после оценки.
5. Текст на изображениях не оценивается: модели плохо рисуют надписи. UI оценивается по форме, цвету и иерархии.

### 4.3. Оценочный лист (для каждого изображения)
| Критерий | Вес | Оценка 1–5 |
|---|---|---|
| Читаемость котов при 25% размера | ×3 | |
| Различимость силуэтов трёх котов | ×3 | |
| Понятность игровой сцены (что где, кто что делает) | ×2 | |
| Юмор и обаяние | ×2 | |
| Реалистичность производства (анимация, модульность) | ×2 | |
| Единство с UI | ×1 | |
| Личное «хочу в это играть» | ×2 | |

## 5. Структура промптов

Промпт = **[Блок стиля] + [Блок сцены] + [Общие ограничения]**. Промпты на английском: модели генерации стабильнее работают с английским.

### 5.1. Общие ограничения (добавляются ко всем)
```
Mobile game concept art, portrait orientation. Humorous, warm, family-friendly tone.
No blood, no realistic violence, no gore. No photorealism.
Characters must read clearly at small size: strong silhouettes, clear value contrast against background.
Minimal text; any UI labels may be placeholder shapes. No watermark, no signature, no logos.
```

### 5.2. Описания персонажей (одинаковые во всех промптах)
```
RYZHY: a ginger tomcat, broad shoulders and chunky paws, torn left ear, small scar across the nose, cocky grin — the brawler.
SHNYR: a lanky grey striped tabby, oversized ears, very long thin tail, narrow shifty eyes — the sneaky one.
PONCHIK: a huge round black-and-white tuxedo cat, tiny head on a big body, sleepy half-closed eyes — the tank.
```

### 5.3. Блоки стиля (черновики)
| # | Блок стиля |
|---|---|
| S1 | `Cartoon 2D style, thick clean dark outlines, flat colors with simple cel shading, bold saturated palette, exaggerated proportions, in the spirit of modern mobile cartoon games.` |
| S2 | `Detailed pixel art, 16-bit era inspired, limited palette with careful dithering, crisp pixel edges, expressive chunky character sprites, cozy dumpster atmosphere.` |
| S3 | `Stylized 2.5D look, soft volumetric lighting, rounded dimensional characters like a stylized 3D animated film, gentle ambient occlusion, warm evening light.` |
| S4 | `Comic book style, bold ink lines, halftone shading, exaggerated facial expressions and dynamic poses, punchy limited color palette, speech-bubble energy.` |
| S5 | `Hand-painted casual mobile game style, soft painterly brush textures, warm cozy palette, gentle lighting, characters with clean readable shapes over painted backgrounds.` |

### 5.4. Блоки сцен

**A. Линейка котов** (раунд 1; горизонтальный кадр 3:2)
```
Character lineup sheet of three stray cats standing side by side on a plain light neutral background, full body, three-quarter view, same ground line, showing relative size:
left RYZHY, center PONCHIK, right SHNYR. [character descriptions]
Below each cat, a small solid black silhouette of the same pose.
```

**B. Экран убежища** (раунд 1; портрет 2:3 ≈ 9:16)
```
Mobile game main screen mockup, portrait. Scene: the back alley behind a small restaurant at dusk, warm light from the restaurant back door.
Center-left: a big rusty dumpster with junk around it. Right: a stack of cardboard boxes where a cat sleeps.
Upper-left: a tin can hanging on a string from a pipe — RYZHY punches it.
Upper-center: a wind-up toy mouse — SHNYR chases it.
Upper-right: an improvised exercise wheel made from an old tire — PONCHIK runs in it, looking exhausted.
UI overlay: thin top bar with a food icon and a scrap-metal icon with numbers; bottom area with three cat portrait cards, each with two small progress bars; two large tabs at the very bottom.
The game scene occupies the middle 55% of the screen.
```

**C. Бой** (раунд 2; портрет)
```
Mobile auto-battle screen, portrait. Arena: a dark brick alley with a puddle and a street lamp.
Left: RYZHY lunging with an open-mouth bite attack; right: a big muscular rat in a torn hoodie recoiling.
Floating damage number above the rat, a short skill banner above Ryzhy.
Under each fighter: a health bar and a smaller energy bar, small buff icons.
Top: two rows of small round portraits showing each side's fighter queue (cats vs rivals).
Bottom: speed toggle button and skip button.
```

**D. Тренировки** (раунд 2; горизонтальный 3:2)
```
Three-panel illustration of cats training with junk equipment, same scale in each panel:
1) RYZHY punching a tin can hanging on a string, can dented and swinging;
2) SHNYR pouncing at a wind-up toy mouse, mid-air;
3) PONCHIK running inside a wheel made from an old tire, sweating, tongue out.
Simple matching backgrounds, emphasis on readable poses suitable for looping animation.
```

**E. Карта территорий** (раунд 2; портрет)
```
Mobile game territory map, portrait, vertical path from bottom to top through a small city block at night:
bottom — restaurant dumpster (home base); then backyard with laundry lines; fish shop back door; dark alley; city park with a bench; top — the restaurant rooftop with a neon sign and a big grey tomcat in a captain's hat silhouetted against the moon.
Each location is a round node connected by a dashed path; lower nodes look conquered (cat paw flag), the next one glows, upper ones are dimmed and locked.
```

**F. UI-кит** (раунд 2; портрет)
```
Mobile game UI style sheet on a neutral background: primary and secondary buttons (normal / pressed / disabled), a bottom sheet panel, a popup card with a portrait frame, horizontal progress bars (energy yellow, satiety orange, health red, grit blue),
resource icons (food: fish skeleton; junk: bent bolt and bottle cap), stat icons (strength paw, agility feather-like tail, toughness shield), a small toast notification, tab bar with two tabs.
Materials and shapes inspired by street junk: cardboard, tape, bottle caps, tin.
```

### 5.5. Параметры API (черновик, уточнить перед запуском)
| Параметр | Значение |
|---|---|
| Модель | Актуальная модель OpenAI Image API на дату генерации (уточнить; например, `gpt-image-1` или новее) |
| Размер | Портрет `1024×1536`; линейка и тренировки — `1536×1024` |
| Качество | `high` |
| Количество | 1 на запрос |
| Ключ API | Предоставляет пользователь через переменную окружения; ключ не сохраняется ни в репозитории, ни в документах |
| Хранение результатов | `Docs/ArtDirection/Concepts/round1/<scene>_<style>.png` + `prompts.md` с точными промптами каждого изображения |

## 6. После выбора стиля — структура Art Bible
`Docs/ArtDirection/ART_BIBLE.md` (создаётся только после выбора):
1. Суть стиля в 3 фразах + 3 референса (выбранные концепты).
2. Палитра: основная, акцентная, UI, состояния (опасно / доступно / заблокировано).
3. Пропорции и силуэты котов; линейка размеров; правила выразительности (уши, хвост, глаза).
4. Контур, толщина линий, свет и тень.
5. Окружение: модульность объектов, уровни улучшений, фоны арен.
6. UI: формы, шрифт(ы), иконки, отступы, состояния кнопок.
7. Анимация: технология, принципы (squash & stretch, тайминги), список клипов из CONTENT_SPEC.
8. VFX: язык эффектов (цифры, вспышки, пыль).
9. Технические требования: разрешения, PPU, атласы, сжатие для Android/iOS.
10. Чего избегать.

## 7. Что нужно от пользователя для запуска генерации
1. Явное разрешение на вызов OpenAI Image API.
2. Список стилей для раунда 1 (по умолчанию S1, S2, S4, S5).
3. API-ключ (в переменной окружения) и согласие с ориентировочной стоимостью.
4. Правки описаний персонажей или сцен, если есть.
