using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AchievementTracker
{
    /// <summary>
    /// Подробные пояснения «как выполнить» для каждого достижения: { ru, en }.
    /// Имена собственные — только через {$токен} из локализации игры, чтобы совпадали с тем, что видно в игре.
    /// </summary>
    internal static class Guide
    {
        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            ["AllBosses"] = new[]
            {
                "Убей всех главных боссов мира, сложность не важна. Засчитывается, если ты участвовал в убийстве. Боссов призывают на алтарях подношениями, а где искать алтари, подсказывают камни «{$piece_vegvisir}».",
                "Kill every main boss of the world, on any difficulty. It counts if you took part in the kill. Bosses are summoned at their altars with offerings; {$piece_vegvisir} stones show where the altars are."
            },
            ["AllBossesHard"] = new[]
            {
                "То же, что «{ach:AllBosses}», но каждый босс должен быть убит при сложности боя «{$menu_modifier_hard}» или выше. Убийства на более лёгкой сложности сюда не идут — их придётся повторить.",
                "Same as {ach:AllBosses}, but each boss must be killed while the world's combat difficulty is {$menu_modifier_hard} or higher. Kills on easier settings don't count and have to be repeated."
            },
            ["AllBossesNormal"] = new[]
            {
                "Убей всех главных боссов при сложности боя «{$menu_modifier_normal}» или выше. Убийства на сложности «{$menu_modifier_easy}», «{$menu_modifier_veryeasy}» и «{$menu_modifier_casual}» не засчитываются.",
                "Kill every main boss on {$menu_modifier_normal} combat difficulty or higher. Kills on {$menu_modifier_easy}, {$menu_modifier_veryeasy} or {$menu_modifier_casual} don't count."
            },
            ["AllBuildPieces"] = new[]
            {
                "Поставь хотя бы по одному разу каждую постройку из всех инструментов: «{$item_hammer}», «{$item_hoe}» и «{$item_cultivator}» (посадки саженцев и семян тоже считаются). Большая часть открывается по мере того, как находишь новые материалы.",
                "Place every build piece at least once, from all tools: {$item_hammer}, {$item_hoe} and {$item_cultivator} (planting saplings and seeds counts too). Most of them unlock as you find new materials."
            },
            ["AllFoodCooked"] = new[]
            {
                "Приготовь каждое блюдо хотя бы раз: на огне и на кухонных станциях вроде «{$piece_cauldron}» и «{$piece_oven}». Промежуточные заготовки (например, тесто или сырые пироги) тоже входят в список.",
                "Cook every dish at least once: on the fire and at kitchen stations like the {$piece_cauldron} and {$piece_oven}. Intermediate items like dough or uncooked pies are on the list too."
            },
            ["AllFoodEaten"] = new[]
            {
                "Съешь хотя бы раз каждый съедобный предмет — и готовые блюда, и сырые продукты вроде ягод, грибов и предмета «{$item_honey}».",
                "Eat every edible item at least once — both cooked dishes and raw food like berries, mushrooms and {$item_honey}."
            },
            ["AllItemCraft"] = new[]
            {
                "Создай на станциях каждый предмет, у которого есть рецепт: оружие, броню, инструменты, боеприпасы, материалы и прочее. Улучшение уже созданного предмета не считается новым крафтом.",
                "Craft every item that has a station recipe: weapons, armor, tools, ammo, materials and more. Upgrading an item you already made doesn't count as a new craft."
            },
            ["AllMiniBosses"] = new[]
            {
                "Победи всех мини-боссов: {$enemy_skeletonfire}, {$enemy_fenringcultist_hildir}, {$enemy_goblinbrute_hildircombined} и {$enemy_charred_melee_Dyrnwyn}. Первые трое охраняют сундуки из заданий торговки {$npc_hildir} — их логова появляются на карте, когда берёшь задание.",
                "Defeat all minibosses: {$enemy_skeletonfire}, {$enemy_fenringcultist_hildir}, {$enemy_goblinbrute_hildircombined} and {$enemy_charred_melee_Dyrnwyn}. The first three guard the chests from {$npc_hildir}'s quests — their lairs appear on the map when you take the quest."
            },
            ["AllWeaponCraft"] = new[]
            {
                "Создай каждое оружие, включая все варианты одного оружия с разными эффектами (например, алебарды с разной магией). Многие варианты требуют материалов из поздних биомов.",
                "Craft every weapon, including every variant of the same weapon with different effects (for example atgeirs with different magic). Many variants need late-biome materials."
            },
            ["Arrived"] = new[]
            {
                "Просто появись в мире — засчитывается при первом входе.",
                "Just spawn into a world — it counts on your first entry."
            },
            ["BigFish"] = new[]
            {
                "Поймай предметом «{$item_fishingrod}» рыбу с четырьмя звёздами (качество 4). Рыба, пойманная руками, не считается.",
                "Catch a four-star fish (quality 4) with a {$item_fishingrod}. Fish grabbed by hand don't count."
            },
            ["Boss1Eikthyr"] = new[]
            {
                "Убей босса «{$enemy_eikthyr}». Алтарь находится в биоме «{$biome_meadows}», для призыва нужны два предмета «{$item_trophy_deer}».",
                "Kill {$enemy_eikthyr}. The altar is in the {$biome_meadows} biome; summon with {$item_trophy_deer} ×2."
            },
            ["Boss2Elder"] = new[]
            {
                "Убей босса «{$enemy_gdking}». Алтарь находится в биоме «{$biome_blackforest}», для призыва нужны три предмета «{$item_ancientseed}», оно выпадает при разрушении объекта «{$enemy_greydwarfspawner}».",
                "Kill {$enemy_gdking}. The altar is in the {$biome_blackforest} biome; summon with {$item_ancientseed} ×3 — it drops when you destroy a {$enemy_greydwarfspawner}."
            },
            ["Boss3Bonemass"] = new[]
            {
                "Убей босса «{$enemy_bonemass}». Алтарь находится в биоме «{$biome_swamp}», для призыва нужны десять предметов «{$item_witheredbone}» из подземелий «{$location_sunkencrypt}» (вход открывает «{$item_cryptkey}»). Босс устойчив к рубящему и колющему урону — лучше всего работает дробящее оружие.",
                "Kill {$enemy_bonemass}. The altar is in the {$biome_swamp} biome; summon with {$item_witheredbone} ×10 from the {$location_sunkencrypt} (opened with a {$item_cryptkey}). It resists slash and pierce — blunt weapons work best."
            },
            ["Boss4Moder"] = new[]
            {
                "Убей босса «{$enemy_dragon}». Алтарь находится в биоме «{$biome_mountain}», для призыва нужны три предмета «{$item_dragonegg}».",
                "Kill {$enemy_dragon}. The altar is in the {$biome_mountain} biome; summon with {$item_dragonegg} ×3."
            },
            ["Boss5Yagluth"] = new[]
            {
                "Убей босса «{$enemy_goblinking}». Алтарь находится в биоме «{$biome_plains}», для призыва нужны пять предметов «{$item_goblintotem}».",
                "Kill {$enemy_goblinking}. The altar is in the {$biome_plains} biome; summon with {$item_goblintotem} ×5."
            },
            ["Boss6Queen"] = new[]
            {
                "Убей босса «{$enemy_seekerqueen}» — она в локации «{$location_dvergrboss}» ({$biome_mistlands}). Вход открывает «{$item_dvergrkey}», его собирают из нескольких предметов «{$item_dvergrkeyfragment}», которые находят в локациях «{$location_dvergrtown}».",
                "Kill {$enemy_seekerqueen} in the {$location_dvergrboss} ({$biome_mistlands} biome). The entrance opens with a {$item_dvergrkey}, assembled from several {$item_dvergrkeyfragment} items found in {$location_dvergrtown} locations."
            },
            ["Boss7Fader"] = new[]
            {
                "Убей босса «{$enemy_fader}» ({$biome_ashlands}). Для призыва нужно несколько предметов «{$item_bellfragment}».",
                "Kill {$enemy_fader} in the {$biome_ashlands} biome. Summoning needs several {$item_bellfragment} items."
            },
            ["Boss8FrozenKing"] = new[]
            {
                "Убей босса «{$enemy_frozenking}» в биоме «{$biome_deepnorth}» (засчитывается последняя фаза боя).",
                "Kill {$enemy_frozenking} in the {$biome_deepnorth} biome (the final phase of the fight counts)."
            },
            ["BuildDyrnwyn"] = new[]
            {
                "Собери меч «{$item_sword_dyrnwyn}» из трёх частей: «{$item_Dyrnwyn_blade}», «{$item_Dyrnwyn_hilt}» и «{$item_Dyrnwyn_tip}» ({$biome_ashlands}). Засчитывается сам крафт меча.",
                "Assemble {$item_sword_dyrnwyn} from three parts: {$item_Dyrnwyn_blade}, {$item_Dyrnwyn_hilt} and {$item_Dyrnwyn_tip} ({$biome_ashlands} biome). Crafting the sword is what counts."
            },
            ["BuildHigh"] = new[]
            {
                "Поставь постройку на высоте не меньше 49 м над землёй прямо под ней. Засчитываются только детали на твоём собственном фундаменте — строй высокую башню от земли.",
                "Place a piece at least 49 m above the ground directly below it. Only pieces on your own foundation count — build a tall tower from the ground up."
            },
            ["BuildHighWorld"] = new[]
            {
                "Поставь постройку на абсолютной высоте больше 432 м (около 400 м над уровнем моря). Проще всего — на вершине высокой горы. Деталь должна стоять на твоём фундаменте и не внутри подземелья.",
                "Place a piece at a world height above 432 m (about 400 m above sea level). A tall mountain peak is easiest. It must stand on your own foundation and not inside a dungeon."
            },
            ["BuildHouse"] = new[]
            {
                "Построй дом, в котором есть все нужные категории деталей: полы, стены, крыша, мебель, освещение и так далее. Точные количества по каждой категории — в условиях ниже.",
                "Build a house containing all required piece categories: floors, walls, roof, furniture, lighting and so on. Exact counts per category are listed in the requirements below."
            },
            ["BuildVillage"] = new[]
            {
                "Как «{ach:BuildHouse}», но в масштабе деревни: много зданий, полов, стен, крыш и архитектурных элементов. Точные количества — в условиях ниже.",
                "Like {ach:BuildHouse}, but village-sized: many buildings, floors, walls, roofs and architecture pieces. Exact counts are listed in the requirements below."
            },
            ["ChildOfOdin"] = new[]
            {
                "Мета-достижение: получи «{ach:ExploreNSEWNoMap}», «{ach:AllBossesHard}», «{ach:KillAllCreaturesHard}» и «{ach:GrindDays}».",
                "Meta achievement: unlock {ach:ExploreNSEWNoMap}, {ach:AllBossesHard}, {ach:KillAllCreaturesHard} and {ach:GrindDays}."
            },
            ["DeathByAllTypes"] = new[]
            {
                "Погибни от каждой причины из списка ниже: враг, падение, утопление, огонь, холод, яд, дым, край мира и другие. Засчитывается только тот урон, который тебя добил.",
                "Die from every cause in the list below: enemy, falling, drowning, fire, freezing, poison, smoke, the world's edge and more. Only the damage that actually kills you counts."
            },
            ["DeathByObliterator"] = new[]
            {
                "Погибни внутри постройки «{$piece_incinerator}», когда она срабатывает.",
                "Die inside the {$piece_incinerator} when it activates."
            },
            ["DeathByTree"] = new[]
            {
                "Погибни от падающего дерева или бревна.",
                "Get killed by a falling tree or log."
            },
            ["DeathByTreeAll"] = new[]
            {
                "Погибни от каждого вида дерева из списка ниже. Удобнее всего с малым запасом здоровья, стоя там, куда падает ствол.",
                "Get killed by every kind of tree in the list below. Easiest with low health, standing where the trunk falls."
            },
            ["ExploreNSEW"] = new[]
            {
                "Уйди дальше 10 350 м от центра мира на север, юг, восток и запад. Это у самого края мира, так что понадобится корабль. Сколько осталось в каждую сторону — в условиях ниже.",
                "Travel more than 10,350 m from the world centre to the north, south, east and west. That's right at the world's edge, so you'll need a ship. The distance left in each direction is shown in the requirements below."
            },
            ["ExploreNSEWNoMap"] = new[]
            {
                "То же, что «{ach:ExploreNSEW}», но в мире с модификатором «{$menu_nomap}». Путешествия в обычном мире сюда не засчитываются.",
                "Same as {ach:ExploreNSEW}, but in a world with the {$menu_nomap} modifier. Travel in a normal world doesn't count."
            },
            ["FindAllTrophies"] = new[]
            {
                "Подбери хотя бы по одному трофею каждого вида. Трофеи выпадают не всегда — иногда придётся убить нескольких существ. Где кого искать — в условиях ниже.",
                "Pick up at least one trophy of every kind. Creatures don't always drop them, so you may need several kills. Where to find each creature is shown in the requirements below."
            },
            ["FirstDeath"] = new[]
            {
                "Погибни в первый раз.",
                "Die for the first time."
            },
            ["GrindBuild"] = new[]
            {
                "Поставь 500 построек.",
                "Place 500 build pieces."
            },
            ["GrindComfort"] = new[]
            {
                "Достигни уровня «{$se_rested_comfort}» 20. Его дают укрытие, огонь, кровать, стулья, ковры, баннеры и другие предметы рядом; из каждой группы учитывается только лучший предмет.",
                "Reach {$se_rested_comfort} level 20. It comes from shelter, fire, a bed, chairs, rugs, banners and other nearby items; only the best item from each group counts."
            },
            ["GrindCook"] = new[]
            {
                "Приготовь 100 блюд в сумме, любых.",
                "Cook 100 dishes in total, any kind."
            },
            ["GrindDays"] = new[]
            {
                "Проживи 30 игровых дней подряд без смерти. Смерть обнуляет счётчик, засчитывается лучший результат.",
                "Survive 30 in-game days in a row without dying. Death resets the counter; your best streak counts."
            },
            ["GrindEat"] = new[]
            {
                "Съешь 500 единиц еды в сумме.",
                "Eat 500 food items in total."
            },
            ["GrindFish"] = new[]
            {
                "Поймай каждый вид рыбы. Разные виды водятся в разных биомах и клюют на разную наживку.",
                "Catch every kind of fish. Different kinds live in different biomes and bite on different bait."
            },
            ["GrindForager"] = new[]
            {
                "Собери 250 ягод и 250 грибов (два отдельных счётчика).",
                "Pick 250 berries and 250 mushrooms (two separate counters)."
            },
            ["GrindHarvest"] = new[]
            {
                "Собери 500 единиц урожая с грядок.",
                "Harvest 500 crops from your fields."
            },
            ["GrindHoney"] = new[]
            {
                "Собери 500 единиц предмета «{$item_honey}» из построек «{$piece_beehive}». Чтобы построить «{$piece_beehive}», нужна «{$item_queenbee}».",
                "Collect 500 {$item_honey} from a {$piece_beehive}. Building a {$piece_beehive} needs a {$item_queenbee}."
            },
            ["GrindLeviathan"] = new[]
            {
                "Потревожь огромное морское существо, похожее на поросший ракушками остров посреди биома «{$biome_ocean}»: добывай на нём киркой, пока оно не уйдёт под воду.",
                "Disturb the huge sea creature that looks like a barnacle-covered island in the {$biome_ocean}: mine it with a pickaxe until it dives."
            },
            ["GrindSail"] = new[]
            {
                "Проплыви в сумме около 132 км (дважды вокруг мира). Засчитывается и когда ты пассажир.",
                "Sail about 132 km in total (twice around the world). It counts as a passenger too."
            },
            ["GrindSailHelm"] = new[]
            {
                "Проплыви около 66 км, стоя за штурвалом.",
                "Sail about 66 km while manning the helm."
            },
            ["GrindTreasure"] = new[]
            {
                "Найди и раскопай 10 закопанных сундуков с сокровищами.",
                "Find and dig up 10 buried treasure chests."
            },
            ["GrindTrees"] = new[]
            {
                "Сруби 500 деревьев. Засчитывается каждое срубленное дерево; разрубание упавших брёвен на части не считается.",
                "Cut down 500 trees. Every tree you cut down counts; chopping the fallen logs into pieces does not."
            },
            ["KillAllCreatures"] = new[]
            {
                "Убей хотя бы по одному существу каждого вида, на любой сложности. Некоторые встречаются только в подземельях, во время набегов или в особых местах — подсказки в условиях ниже.",
                "Kill at least one creature of every kind, on any difficulty. Some only appear in dungeons, during raids or in special places — see the hints in the requirements below."
            },
            ["KillAllCreaturesHard"] = new[]
            {
                "То же, что «{ach:KillAllCreatures}», но убийства засчитываются только при сложности боя «{$menu_modifier_hard}» или выше.",
                "Same as {ach:KillAllCreatures}, but kills only count on {$menu_modifier_hard} combat difficulty or higher."
            },
            ["KingsQueens"] = new[]
            {
                "Мета-достижение: получи «{ach:AllBosses}», «{ach:KillAllCreatures}», «{ach:AllItemCraft}», «{ach:AllFoodCooked}», «{ach:AllBuildPieces}» и «{ach:ExploreNSEW}».",
                "Meta achievement: unlock {ach:AllBosses}, {ach:KillAllCreatures}, {ach:AllItemCraft}, {ach:AllFoodCooked}, {ach:AllBuildPieces} and {ach:ExploreNSEW}."
            },
            ["MultiplayerBoss"] = new[]
            {
                "Убей босса вместе с другом: урон боссу должны нанести хотя бы два игрока.",
                "Kill a boss together with a friend: at least two players must damage the boss."
            },
            ["SailEdge"] = new[]
            {
                "Уплыви за край мира и упади с него.",
                "Sail past the edge of the world and fall off."
            },
            ["SoloBoss"] = new[]
            {
                "Убей босса в одиночку: урон ему должен нанести только ты.",
                "Kill a boss all by yourself: you must be the only player who damaged it."
            },
        };

        // Как засчитывается убийство — общее для всех достижений «убей» (Character.OnDeath / Character.Damage)
        private static readonly string[] KillCredit =
        {
            "Убийство засчитывается всем игрокам, которые хоть раз сами ранили существо (оружием, стрелами, магией) и были в игре в момент его смерти — добивать не обязательно. Урон от прирученных и призванных существ не в счёт.",
            "A kill counts for every player who damaged the creature themselves at least once (weapons, arrows, magic) and was in the game when it died — the last hit isn't required. Damage from tamed or summoned creatures doesn't count."
        };

        private static readonly string[] Foundation =
        {
            "луч от детали вниз (до 200 м) должен пройти минимум через {cfg:foundationPieces} построек, и больше {cfg:foundationPercent}% из них должны быть поставлены тобой; земля и скалы постройками не считаются",
            "a line straight down from the piece (up to 200 m) has to pass through at least {cfg:foundationPieces} build pieces, and more than {cfg:foundationPercent}% of them have to be placed by you; terrain and rock don't count as pieces"
        };

        private static readonly string[] Cluster =
        {
            "Считаются все твои постройки в загруженной области вокруг тебя, а не одно здание, — по категориям, и для каждой категории запоминается лучший результат. Проверка мягкая: каждая категория засчитывается не больше чем на 115% своей нормы, а сумма должна превысить сумму норм, так что перебор в одних категориях покрывает небольшой недобор в других (прогресс в этом окне считается так же). Пересчёт идёт не после каждой детали, а примерно раз в 20 поставленных деталей и при входе в игру — если всё уже стоит, поставь ещё пару десятков деталей.",
            "All your pieces in the loaded area around you count, not a single building — per category, and the best result for each category is remembered. The check is lenient: each category counts up to 115% of its target and the total has to exceed the sum of targets, so extra pieces in some categories cover a small shortfall in others (progress in this window uses the same rule). It's rechecked not after every piece but about every 20 pieces you place and when you log in — if everything is already built, place a couple dozen more pieces."
        };

        // «Как игра это проверяет» — точные правила из кода игры для неочевидных достижений: { ru, en }
        private static readonly Dictionary<string, string[]> Rules = new Dictionary<string, string[]>
        {
            ["BuildHigh"] = new[]
            {
                "Высота — это расстояние от детали до земли прямо под ней: игра меряет лучом вниз, но не дальше 200 м (если земли в пределах 200 м нет, высота не засчитается). Кроме того, деталь должна стоять на твоём фундаменте: " + Foundation[0] + ". Засчитывается рекорд.",
                "Height is the distance from the piece down to the terrain directly below it: the game measures it with a line straight down, but no further than 200 m (no terrain within 200 m means no reading). The piece also has to sit on your own foundation: " + Foundation[1] + ". Your record counts."
            },
            ["BuildHighWorld"] = new[]
            {
                "Берётся абсолютная высота детали (уровень моря — 30), она должна быть больше 432; внутри подземелий не считается. Нужен и твой фундамент: " + Foundation[0] + ". Поэтому одна деталь на вершине горы не сработает — поставь на вершине столб минимум из {cfg:foundationPieces} своих деталей, а последнюю — выше 432.",
                "The piece's world height is used (sea level is 30) and must be above 432; inside dungeons doesn't count. It also has to sit on your own foundation: " + Foundation[1] + ". So a single piece on a mountain peak won't work — build a column of at least {cfg:foundationPieces} of your own pieces on the peak and place the last one above 432."
            },
            ["BuildHouse"] = Cluster,
            ["BuildVillage"] = Cluster,
            ["GrindDays"] = new[]
            {
                "День засчитывается в момент наступления утра, когда появляется надпись о новом дне, — если ты в этот момент в игре и жив (сон до утра тоже подходит). Пока тебя нет в игре, дни для тебя не идут. Смерть обнуляет счётчик, в достижение идёт лучший результат.",
                "A day counts at the moment morning comes and the new-day message appears — if you're in the game and alive at that moment (sleeping through the night works too). Days don't count for you while you're offline. Death resets the counter; your best streak counts."
            },
            ["GrindComfort"] = new[]
            {
                "Комфорт проверяется каждые 2 секунды там, где ты стоишь, и запоминается рекорд. Базовое значение — 1, укрытие даёт +1, а предметы комфорта в радиусе 10 м учитываются только когда ты под крышей. Из каждой группы (огонь, кровать, сиденья и т.п.) засчитывается лучший предмет, одинаковые предметы не складываются.",
                "Comfort is checked every 2 seconds where you stand, and your record is kept. The base is 1, shelter adds 1, and comfort pieces within 10 m only count while you're sheltered. Only the best piece from each group (fire, bed, seating and so on) counts, and identical pieces don't stack."
            },
            ["AllBosses"] = KillCredit,
            ["AllBossesHard"] = KillCredit,
            ["AllBossesNormal"] = KillCredit,
            ["AllMiniBosses"] = KillCredit,
            ["KillAllCreatures"] = KillCredit,
            ["KillAllCreaturesHard"] = KillCredit,
            ["SoloBoss"] = new[]
            {
                KillCredit[0] + " «В одиночку» — значит, урон боссу нанёс ровно один игрок.",
                KillCredit[1] + " \"All by yourself\" means exactly one player damaged the boss."
            },
            ["MultiplayerBoss"] = new[]
            {
                KillCredit[0] + " «Вместе» — значит, урон боссу нанесли хотя бы двое игроков.",
                KillCredit[1] + " \"Together\" means at least two players damaged the boss."
            },
            ["GrindTrees"] = new[]
            {
                "Дерево засчитывается тому, чей удар его свалил: если последним ударил друг, дерево пойдёт ему.",
                "A tree counts for whoever landed the hit that felled it: if a friend hits last, it goes to them."
            },
            ["GrindTreasure"] = new[]
            {
                "Сундук засчитывается, когда ты впервые открываешь его сам, — каждому игроку отдельно.",
                "A chest counts the first time you open it yourself — separately for each player."
            },
            ["GrindLeviathan"] = new[]
            {
                "Засчитывается всем игрокам рядом, когда существо уходит под воду, — не только тому, кто бил киркой.",
                "It counts for every player nearby when the creature dives — not only for the one who mined it."
            },
            ["GrindSail"] = new[]
            {
                "Считается расстояние, пройденное, пока ты на борту корабля, в том числе пассажиром.",
                "Distance counts while you're aboard a ship, including as a passenger."
            },
            ["GrindSailHelm"] = new[]
            {
                "Считается только расстояние, пройденное, пока ты сам управляешь кораблём за штурвалом.",
                "Only distance covered while you're steering the ship at the helm counts."
            },
            ["BigFish"] = new[]
            {
                "Качество — это цифра на иконке пойманной рыбы; нужна рыба качества 4. Засчитывается только улов на удочку.",
                "Quality is the number on the caught fish's icon; you need a quality 4 fish. Only fish caught with a rod count."
            },
            ["AllItemCraft"] = new[]
            {
                "В список входят только предметы, для крафта которых нужна станция (верстак, кузница и т.п.); то, что делается без станции, не входит. Улучшение уже созданного предмета не считается.",
                "Only items whose recipe needs a crafting station (workbench, forge and so on) are on the list; things made without a station aren't. Upgrading an item you already made doesn't count."
            },
            ["FindAllTrophies"] = new[]
            {
                "Трофей засчитывается, когда впервые попадает в твой инвентарь, — подойдёт и трофей из сундука, который туда положил кто-то другой.",
                "A trophy counts the first time it enters your inventory — even one taken from a chest where someone else put it."
            },
            ["ExploreNSEW"] = new[]
            {
                "Засчитывается время, проведённое за границей: достаточно пробыть там хотя бы секунду.",
                "Time spent past the line counts: staying there for at least a second is enough."
            },
            ["ExploreNSEWNoMap"] = new[]
            {
                "Засчитывается время, проведённое за границей: достаточно пробыть там хотя бы секунду.",
                "Time spent past the line counts: staying there for at least a second is enough."
            },
        };

        public static string Get(AchState st)
        {
            if (!Texts.TryGetValue(st.Ach.m_id, out string[] t)) return Generic(st);
            string text = Fill(Loc.S(t[0], t[1]));
            if (Rules.TryGetValue(st.Ach.m_id, out string[] r))
            {
                text += "\n\n" + $"<color=#{UiKit.Hex(UiKit.Beige)}><b>{Loc.S("Как игра это проверяет:", "How the game checks it:")}</b></color> " + Fill(Loc.S(r[0], r[1]));
            }
            return text;
        }

        /// <summary>
        /// {$token} — название из локализации игры (босс, предмет, биом), {ach:Id} — название другого достижения.
        /// Так имена совпадают с тем, что игрок видит в игре, на любом языке. Цветовые теги из локализации убираем.
        /// </summary>
        private static string Fill(string text) =>
            Regex.Replace(text, @"\{(ach:(?<id>\w+)|cfg:(?<cfg>\w+)|(?<tok>\$[A-Za-z0-9_]+))\}", m =>
                m.Groups["id"].Success ? Names.Achievement(Analyzer.Find(m.Groups["id"].Value))
                : m.Groups["cfg"].Success ? GameValue(m.Groups["cfg"].Value)
                : Regex.Replace(Names.L(m.Groups["tok"].Value), "<[^>]+>", ""));

        /// <summary>{cfg:…} — числа, которые игра реально использует (могут отличаться от значений по умолчанию в коде).</summary>
        private static string GameValue(string key)
        {
            Achievements a = Achievements.m_instance;
            switch (key)
            {
                case "foundationPieces": return a != null ? a.m_buildFoundationMinPieces.ToString() : "5";
                case "foundationPercent": return a != null ? Mathf.RoundToInt(a.m_buildFoundationMinCreatorPercent * 100f).ToString() : "45";
                default: return key;
            }
        }

        /// <summary>Для достижений, которых нет в списке (новые версии игры, моды) — пояснение по типам условий.</summary>
        private static string Generic(AchState st)
        {
            var kinds = new HashSet<ReqKind>(st.Reqs.Select(r => r.Req.Kind));
            var parts = new List<string>();
            if (kinds.Contains(ReqKind.Enemy)) parts.Add(Loc.S("убить указанных существ", "kill the listed creatures"));
            if (kinds.Contains(ReqKind.ItemCraft)) parts.Add(Loc.S("создать указанные предметы", "craft the listed items"));
            if (kinds.Contains(ReqKind.PiecePlaced)) parts.Add(Loc.S("поставить указанные постройки", "place the listed pieces"));
            if (kinds.Contains(ReqKind.ItemPickup)) parts.Add(Loc.S("подобрать указанные предметы", "pick up the listed items"));
            if (kinds.Contains(ReqKind.FoodEaten)) parts.Add(Loc.S("съесть указанную еду", "eat the listed food"));
            if (kinds.Contains(ReqKind.Pickable)) parts.Add(Loc.S("собрать указанное", "gather the listed things"));
            if (kinds.Contains(ReqKind.Stat)) parts.Add(Loc.S("набрать нужные значения статистики", "reach the listed stat values"));
            if (kinds.Contains(ReqKind.OtherAchievement)) parts.Add(Loc.S("получить указанные достижения", "unlock the listed achievements"));
            if (parts.Count == 0) return "";
            return Loc.S("Нужно ", "You need to ") + string.Join(", ", parts) + Loc.S(". Подробности — в условиях ниже.", ". Details are in the requirements below.");
        }
    }
}
