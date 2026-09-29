using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace AchievementTracker
{
    /// <summary>
    /// Перевод игровых токенов ($item_*, $enemy_* и т.п.) на язык мода, даже если сама игра на другом языке.
    /// Все языки лежат в CSV игры; игра загружает только выбранный, а мы читаем нужную колонку отдельно.
    /// </summary>
    internal static class GameText
    {
        private static readonly char[] EndChars = " (){}[]+-!?/\\&%,.:-=<>\n".ToCharArray();
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>Язык, на который нужно переводить, или null — тогда годится обычная локализация игры.</summary>
        private static string TargetLanguage()
        {
            if (Plugin.Language.Value == UiLanguage.Game) return null;
            string want = Plugin.Language.Value == UiLanguage.Russian ? "Russian" : "English";
            return Localization.instance?.GetSelectedLanguage() == want ? null : want;
        }

        public static string Localize(string text) => LocalizeIn(text, TargetLanguage());

        /// <summary>Перевод на конкретный язык игры ("English", "Russian"); null — язык самой игры.</summary>
        public static string LocalizeIn(string text, string language)
        {
            Localization game = Localization.instance;
            if (game == null || string.IsNullOrEmpty(text)) return text;
            string lang = language != null && game.GetSelectedLanguage() != language ? language : null;
            Dictionary<string, string> table = lang != null ? GetTable(lang) : null;
            if (table == null || text.IndexOf('$') < 0) return game.Localize(text);

            var sb = new StringBuilder();
            int pos = 0;
            while (pos < text.Length)
            {
                int start = text.IndexOf('$', pos);
                if (start < 0) break;
                int end = text.IndexOfAny(EndChars, start);
                if (end < 0) end = text.Length;
                string word = text.Substring(start + 1, end - start - 1);
                sb.Append(text, pos, start - pos);
                // Токенов модов нет в CSV игры — для них остаётся перевод игры
                sb.Append(table.TryGetValue(word, out string tr) ? tr : game.Localize("$" + word));
                pos = end;
            }
            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        private static Dictionary<string, string> GetTable(string language)
        {
            if (Tables.TryGetValue(language, out Dictionary<string, string> cached)) return cached;
            Dictionary<string, string> table = null;
            try
            {
                table = Load(language);
                Plugin.Log.LogInfo($"Loaded {table.Count} game strings for {language}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to load game strings for {language}, using game language: {e.Message}");
            }
            Tables[language] = table;
            return table;
        }

        private static Dictionary<string, string> Load(string language)
        {
            var result = new Dictionary<string, string>();
            var settings = (LocalizationSettings)AccessTools.Field(typeof(Localization), "m_localizationSettings").GetValue(null);
            var split = AccessTools.Method(typeof(Localization), "DoQuoteLineSplit");
            if (settings == null || split == null) throw new InvalidOperationException("localization internals not found");
            foreach (TextAsset file in settings.Localizations)
            {
                if (file == null) continue;
                var reader = new StringReader(file.text);
                string[] header = reader.ReadLine().Split(',');
                int col = Array.FindIndex(header, h => h.Trim().Trim('"') == language);
                if (col < 0) continue;
                var rows = (List<List<string>>)split.Invoke(Localization.instance, new object[] { reader });
                foreach (List<string> row in rows)
                {
                    if (row.Count == 0 || row.Count <= col) continue;
                    string key = row[0];
                    if (key.Length == 0 || key.StartsWith("//")) continue;
                    string value = row[col].Trim();
                    if (string.IsNullOrEmpty(value) || value[0] == '\r') value = row.Count > 1 ? row[1].Trim() : "";
                    if (value.Length > 0) result[key] = value;
                }
            }
            return result;
        }
    }
}
