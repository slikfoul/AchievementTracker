# Changelog

## 1.1.0

- Added skill tracking: Alt + left-click a skill in the game's Skills window, or click its checkbox in the left margin, to track or untrack it.
- Tracked skills show their current level, temporary bonuses and live progress toward the next level in the on-screen tracker.
- Skill selection is saved per character and survives death, skill resets and level 100; skill values are read directly from the game.
- Added English and Russian skill tracking labels and tooltips.
- Skill progress refreshes once per second; tracking selections update immediately.
- Supports skill lists without a separate ScrollRect viewport; Alt-click targets initialize independently of the checkbox layout.

## 1.0.1

- Fish requirements are now labeled "Catch" instead of "Pick".
- README: added screenshots, English only.

## 1.0.0

- First public release.
- Achievement window (F7) with filters: doable now, partly doable, locked, completed, tracked, all. Resizable (bottom-right corner, size is remembered) with adjustable font size.
- Full requirements with real numbers and what blocks each one (biome, boss, tool tier, materials, recipe).
- "How to get it" guide for every achievement; names of bosses, items and places come from the game's own localization.
- "How the game checks it" rules for non-obvious achievements (building height and foundation, house/village categories, consecutive days, comfort, kill credit, sailing, treasure and more), taken from the game's code.
- Where creatures live: spawn biomes plus dungeons and locations (scanned once, cached until the game updates).
- On-screen tracking with a list of requirements you can complete right now; completed achievements untrack themselves after 10 seconds.
- Progress popups when an achievement advances.
- Optional stars on unmade build pieces, uncrafted items and needed trophies/food/fish/ingredients/seeds; hold Alt over a star to see which achievements need it.
- English and Russian interface; achievement names can be shown in the game's language, English, Russian or both.
