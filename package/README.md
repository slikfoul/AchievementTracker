# AchievementTracker

**English** · [Русский](#русский)

Client-side helper for Valheim's built-in achievements. See what you can complete right now, what exactly is missing, and track progress while you play. Nothing to install on the server — each player installs it for themselves.

> **Earning achievements with mods:** Valheim stops counting achievement progress when it detects mods (Jötunn marks the game as modded). This mod only *shows* achievements — to actually earn them while modded you also need a mod that lifts that restriction, for example [Unshamed](https://thunderstore.io/c/valheim/p/Azumatt/Unshamed/).

## Features

- **Achievement window (F7)** with filters: *Doable now*, *Partly*, *Locked*, *Completed*, *Tracked*, *All*. Resize it by dragging the bottom-right corner; the size is remembered. Font size is adjustable.
- **Full requirements** with real numbers, and for each one what blocks it: a biome to discover, a boss to defeat, a tool tier, missing materials or an unknown recipe.
- **"How to get it" guide** for every achievement. Names of bosses, items, biomes and places are taken from the game's own localization, so they match what you see in game.
- **Where creatures live:** spawn biomes plus dungeons and locations (e.g. *Sunken Crypts (Swamp)*). Dungeons are scanned once in the background on the first world load and cached until the game updates.
- **On-screen tracking:** tracked achievements stay on screen with a list of requirements you can complete right now. Completed ones untrack themselves after 10 seconds.
- **Progress popups:** e.g. *The Lumberjack 132 / 500* when you fell a tree.
- **Stars on what you still need** (optional): unbuilt pieces in the build menu, uncrafted items at crafting stations, and in your inventory and chests — trophies not picked up yet, food not eaten yet, fish not caught yet, raw ingredients for dishes not cooked yet and seeds for plantings not made yet. **Hold Alt** over a starred item to see which achievements need it.
- **English and Russian** interface. Achievement names can be shown in the game's language, English, Russian or both.

## Console commands

- `achtracker` — open or close the window.
- `achtracker dump` — write a full breakdown of all achievements to `BepInEx/AchievementTracker_dump.txt`.
- `achtracker rescan` — scan dungeons and locations again.

## Settings

All settings are in the Configuration Manager and are translated to the selected language.

| Section | Setting | Default |
|---|---|---|
| General | Language (Game / English / Russian) | Game |
| General | Achievement names (Game / English / Russian / Both) | Game |
| General | Window key | F7 |
| General | Window font size | 15 |
| General | Reveal secret achievements | On |
| General | Stars on what you still need | Off |
| Progress popups | For all achievements (otherwise only tracked) | On |
| Progress popups | Skip tracked achievements | On |
| Progress popups | Duration, vertical position, background opacity | 3 s, −120, 60 % |
| On-screen tracker | Show on screen | On |
| On-screen tracker | Horizontal and vertical position, background opacity | −20, −300, 45 % |
| On-screen tracker | List length | 5 |
| On-screen tracker | Show all requirements (otherwise only doable now) | Off |

## Notes

- This mod was made with the help of AI (Claude) and tested in game by the author.
- It reads the game's data at runtime and does not ship any game assets.
- Source code: [github.com/slikfoul/AchievementTracker](https://github.com/slikfoul/AchievementTracker) (MIT license).

---

## Русский

Клиентский мод для встроенных достижений Valheim. Показывает, что можно выполнить прямо сейчас, чего именно не хватает, и следит за прогрессом во время игры. На сервер ставить не нужно — каждый игрок ставит себе.

> **Получение достижений с модами:** Valheim перестаёт засчитывать прогресс достижений, когда видит моды (Jötunn помечает игру как модифицированную). Этот мод только *показывает* достижения — чтобы они засчитывались с модами, нужен мод, который снимает это ограничение, например [Unshamed](https://thunderstore.io/c/valheim/p/Azumatt/Unshamed/).

## Возможности

- **Окно достижений (F7)** с фильтрами: *Можно выполнить*, *Частично*, *Недоступно*, *Выполнено*, *Отслеживается*, *Все*. Размер окна меняется уголком в правом нижнем углу и запоминается; размер шрифта настраивается.
- **Все условия** с реальными цифрами и для каждого — что мешает: какой биом открыть, какого босса победить, какой нужен инструмент, каких материалов или рецепта не хватает.
- **Блок «Как выполнить»** у каждого достижения. Названия боссов, предметов, биомов и мест берутся из локализации самой игры и совпадают с тем, что видно в игре.
- **Где водятся существа:** биомы спавна, а также подземелья и локации (например, *«Затонувшие склепы» (Болото)*). Подземелья сканируются один раз в фоне при первом входе в мир и кэшируются до обновления игры.
- **Отслеживание на экране:** отслеживаемые достижения висят на экране со списком условий, которые можно выполнить сейчас. Выполненные сами снимаются через 10 секунд.
- **Всплывающие счётчики:** например, *Лесоруб 132 / 500*, когда срубил дерево.
- **Звёздочки на том, что ещё нужно** (по желанию): непостроенное в меню строительства, нескрафченное на станках, а в инвентаре и сундуках — трофеи, которые ещё не подбирал, еда, которую ещё не ел, рыба, которую ещё не ловил, сырьё для неприготовленных блюд и семена для непосаженного. **Зажми Alt** и наведи на предмет со звёздочкой — увидишь, для каких достижений он нужен.
- **Английский и русский** интерфейс. Названия достижений можно показывать на языке игры, по-английски, по-русски или сразу на обоих.

## Команды консоли

- `achtracker` — открыть или закрыть окно.
- `achtracker dump` — выгрузить разбор всех достижений в `BepInEx/AchievementTracker_dump.txt`.
- `achtracker rescan` — заново просканировать подземелья и локации.

## Настройки

Все настройки — в Configuration Manager, подписи переводятся на выбранный язык.

| Раздел | Настройка | По умолчанию |
|---|---|---|
| Общие | Язык (Game / English / Russian) | Game |
| Общие | Названия достижений (Game / English / Russian / Both) | Game |
| Общие | Клавиша окна | F7 |
| Общие | Размер шрифта окна | 15 |
| Общие | Показывать секретные | Вкл |
| Общие | Звёздочки у несделанного | Выкл |
| Всплывающие счётчики | Для всех достижений (иначе — только для отслеживаемых) | Вкл |
| Всплывающие счётчики | Не показывать для отслеживаемых | Вкл |
| Всплывающие счётчики | Длительность, позиция по вертикали, непрозрачность фона | 3 с, −120, 60 % |
| Отслеживание на экране | Показывать на экране | Вкл |
| Отслеживание на экране | Позиция по горизонтали и вертикали, непрозрачность фона | −20, −300, 45 % |
| Отслеживание на экране | Длина списка | 5 |
| Отслеживание на экране | Показывать все условия (иначе — только доступные сейчас) | Выкл |

## Примечания

- Мод сделан с помощью ИИ (Claude) и проверен автором в игре.
- Мод читает данные игры во время работы и не содержит ресурсов игры.
- Исходный код: [github.com/slikfoul/AchievementTracker](https://github.com/slikfoul/AchievementTracker) (лицензия MIT).
