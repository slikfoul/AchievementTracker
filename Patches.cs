using System;
using HarmonyLib;

namespace AchievementTracker
{
    /// <summary>Перехватываем все места, где игра пишет статистику достижений.</summary>
    [HarmonyPatch]
    internal static class Patches
    {
        private static bool s_broken;

        /// <summary>
        /// Статистику игра пишет в том числе посреди спавна персонажа. Исключение отсюда прервёт код игры
        /// (так однажды персонаж заспавнился без загруженного инвентаря), поэтому наружу не выпускаем ничего,
        /// а после первой ошибки просто перестаём реагировать.
        /// </summary>
        private static void Fire(ReqKind kind, string key, float amount, bool isSet, bool cheated)
        {
            if (s_broken) return;
            try
            {
                StatEvents.OnStat(kind, key, amount, isSet, cheated);
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Stat popups disabled until restart due to an error: " + e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat))]
        private static void IncrementStat(PlayerStatType stat, float amount, bool cheated) =>
            Fire(ReqKind.Stat, stat.ToString(), amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SetStat))]
        private static void SetStat(PlayerStatType stat, float amount, bool cheated) =>
            Fire(ReqKind.Stat, stat.ToString(), amount, isSet: true, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatEnemy))]
        private static void IncrementStatEnemy(string name, float amount, bool cheated) =>
            Fire(ReqKind.Enemy, name, amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemPickup))]
        private static void IncrementStatItemPickup(string name, float amount, bool cheated) =>
            Fire(ReqKind.ItemPickup, name, amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemCraft))]
        private static void IncrementStatItemCraft(string name, float amount, bool cheated) =>
            Fire(ReqKind.ItemCraft, name, amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatPickable))]
        private static void IncrementStatPickable(string name, float amount, bool cheated) =>
            Fire(ReqKind.Pickable, name, amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatFoodEaten))]
        private static void IncrementStatFoodEaten(string name, float amount, bool cheated) =>
            Fire(ReqKind.FoodEaten, name, amount, isSet: false, cheated);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatBuildPiecePlaced))]
        private static void IncrementStatBuildPiecePlaced(string name, float amount, bool cheated) =>
            Fire(ReqKind.PiecePlaced, name, amount, isSet: false, cheated);

        /// <summary>Пока открыто окно достижений, Esc закрывает его, а не открывает меню игры.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Menu), "Update")]
        private static bool MenuUpdate()
        {
            try
            {
                return !AchievementPanel.IsOpen && !AchievementPanel.JustClosed;
            }
            catch
            {
                return true;
            }
        }
    }
}
