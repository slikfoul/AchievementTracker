using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AchievementTracker
{
    /// <summary>Человекочитаемые названия условий через локализацию игры.</summary>
    public static class Names
    {
        // У этих статистик нет своего названия $stat_* в игре — берём официальное описание достижения,
        // которое проверяет только эту статистику
        private static readonly Dictionary<PlayerStatType, string> StatFromAchievement = new Dictionary<PlayerStatType, string>
        {
            { PlayerStatType.FishCaughtTier4, "$ach_bigfish_desc" },
            { PlayerStatType.BossKillSolo, "$ach_soloboss_desc" },
            { PlayerStatType.BossKillMultiplayer, "$ach_multiplayerboss_desc" },
            { PlayerStatType.LeviathanSink, "$ach_grindleviathan_desc" },
            { PlayerStatType.Deaths, "$ach_firstdeath_desc" },
            { PlayerStatType.PlayerSpawn, "$ach_arrived_desc" },
        };

        /// <summary>Игровой токен на языке мода (может отличаться от языка игры).</summary>
        public static string L(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            return GameText.Localize(token);
        }

        /// <summary>Игровой токен строго на языке игры — для сравнения с тем, что игра сама сохранила.</summary>
        public static string GameL(string token)
        {
            Localization loc = Localization.instance;
            return loc == null || string.IsNullOrEmpty(token) ? token : loc.Localize(token);
        }

        private static bool Missing(string token, string localized) =>
            string.IsNullOrEmpty(localized) || localized == token || (localized.StartsWith("[") && localized.EndsWith("]"));

        public static string Achievement(Achievement a)
        {
            if (a == null) return "?";
            if (Analyzer.IsHidden(a)) return L("$inventory_achievement_secret");
            // Названия бывает удобно видеть на другом языке, чем интерфейс мода (например, игра на английском)
            switch (Plugin.AchievementNames.Value)
            {
                case AchNameMode.English: return AchName(a, "English");
                case AchNameMode.Russian: return AchName(a, "Russian");
                case AchNameMode.Both:
                    string ru = AchName(a, "Russian"), en = AchName(a, "English");
                    return ru == en ? ru : $"{ru} ({en})";
                default: return AchName(a, null);
            }
        }

        private static string AchName(Achievement a, string language)
        {
            string n = GameText.LocalizeIn(a.m_name, language);
            return Missing(a.m_name, n) ? a.m_id : n;
        }

        public static string Description(Achievement a)
        {
            if (a == null) return "";
            if (Analyzer.IsHidden(a)) return L("$inventory_achievement_secret_description");
            string d = L(a.m_description);
            return Missing(a.m_description, d) ? "" : d;
        }

        public static string Stat(PlayerStatType stat)
        {
            string token = "$stat_" + stat;
            string s = L(token);
            if (!Missing(token, s)) return s;
            if (StatFromAchievement.TryGetValue(stat, out string achToken))
            {
                string d = L(achToken);
                if (!Missing(achToken, d)) return d.TrimEnd('.');
            }

            // Единственная статистика, названия которой в игре нет нигде (смотрели всю таблицу переводов).
            // Строим его из официальной строки-соседа: «Смерть от вод океана Пепельных земель» → лава вместо вод океана.
            if (stat == PlayerStatType.DeathByAshlandsLava)
            {
                return Loc.S("Смерть от лавы Пепельных земель", "Death by Ashlands lava");
            }

            // Категории построек (дом, деревня) — названия категорий есть в игре как метки $tag_*
            string name = stat.ToString();
            if (name.StartsWith("BuildCluster"))
            {
                string cat = name.Substring("BuildCluster".Length).ToLowerInvariant();
                if (cat == "meads") cat = "mead";
                string tag = "$tag_" + cat;
                string tagName = L(tag);
                if (!Missing(tag, tagName)) return Loc.S("Постройки категории «", "Pieces of category \"") + tagName + Loc.S("»", "\"");
            }
            return name;
        }

        public static string Requirement(Requirement r)
        {
            switch (r.Kind)
            {
                case ReqKind.Stat: return Stat(r.Stat);
                case ReqKind.Enemy: return Loc.S("Убить: ", "Kill: ") + L(r.Key) + Modifier(r.Modifier);
                case ReqKind.ItemPickup: return Loc.S("Найти: ", "Find: ") + L(r.Key);
                case ReqKind.ItemCraft: return Loc.S("Создать: ", "Craft: ") + L(r.Key);
                case ReqKind.FoodEaten: return Loc.S("Съесть: ", "Eat: ") + L(r.Key);
                case ReqKind.Pickable: return Loc.S("Собрать: ", "Pick: ") + L(PickableItem(r.Key));
                case ReqKind.PiecePlaced: return Loc.S("Построить: ", "Build: ") + L(r.Key);
                case ReqKind.KnownWorld: return Loc.S("Мир: ", "World: ") + r.Key;
                case ReqKind.KnownWorldKey: return Loc.S("Событие мира: ", "World event: ") + r.Key;
                case ReqKind.KnownCommand: return Loc.S("Команда: ", "Command: ") + r.Key;
                case ReqKind.OtherAchievement: return Loc.S("Достижение: ", "Achievement: ") + Achievement(r.Other);
            }
            return r.Key;
        }

        public static string Difficulty(DifficultyRequirement d)
        {
            switch (d)
            {
                case DifficultyRequirement.Any: return Loc.S("любая", "any");
                case DifficultyRequirement.Hammer: return L("$menu_modifier_hammer");
                case DifficultyRequirement.VeryEasy: return L("$menu_modifier_veryeasy");
                case DifficultyRequirement.Easy: return L("$menu_modifier_easy");
                case DifficultyRequirement.Default: return L("$menu_modifier_normal");
                case DifficultyRequirement.Hard: return L("$menu_modifier_hard");
                case DifficultyRequirement.VeryHard: return L("$menu_modifier_veryhard");
                case DifficultyRequirement.Hardcore: return L("$menu_modifier_hardcore");
                default: return d.ToString();
            }
        }

        private static string Modifier(KillModifiers m)
        {
            switch (m)
            {
                case KillModifiers.Unarmed: return Loc.S(" (без оружия)", " (unarmed)");
                case KillModifiers.Magic: return Loc.S(" (магией)", " (magic)");
                case KillModifiers.Ranged: return Loc.S(" (дальним боем)", " (ranged)");
                case KillModifiers.Melee: return Loc.S(" (в ближнем бою)", " (melee)");
                default: return "";
            }
        }

        /// <summary>Ключ сбора бывает и именем префаба (Raspberry), и готовым токеном ($animal_fish1).</summary>
        public static string PickableItem(string key) =>
            key.StartsWith("$") ? key : PrefabToShared(key) ?? key;

        public static string PrefabToShared(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop id = go != null ? go.GetComponent<ItemDrop>() : null;
            return id?.m_itemData?.m_shared?.m_name;
        }

        public static IEnumerable<Heightmap.Biome> Split(Heightmap.Biome flags)
        {
            foreach (Heightmap.Biome b in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                int v = (int)b;
                if (v != 0 && (v & (v - 1)) == 0 && (flags & b) != 0) yield return b;
            }
        }

        public static string Biomes(Heightmap.Biome flags) =>
            string.Join(", ", Split(flags).Select(b => L(BiomeSector.GetBiomeName(b))));
    }
}
