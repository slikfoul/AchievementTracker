using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace AchievementTracker
{
    /// <summary>
    /// Атрибуты, которые Configuration Manager читает по именам полей (утиная типизация,
    /// ссылаться на его сборку не нужно). Меняем поля — меняются подписи в окне настроек.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public string Description;
        public string Category;
        public int? Order;
        public bool? Browsable;
    }

    /// <summary>Подписи настроек на языке мода.</summary>
    internal static class ConfigText
    {
        // { ключ настройки: ru-имя, en-имя, ru-описание, en-описание }
        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            ["Language"] = new[] { "Язык", "Language", "Язык интерфейса мода. Game — как в игре.", "Mod interface language. Game = same as the game." },
            ["AchievementNames"] = new[] { "Названия достижений", "Achievement names", "На каком языке показывать названия достижений: Game — как в игре, English или Russian — на выбранном языке, Both — русское и английское сразу.", "Language of achievement names: Game — as in the game, English or Russian — in that language, Both — Russian and English at once." },
            ["PanelKey"] = new[] { "Клавиша окна", "Window key", "Клавиша, которая открывает и закрывает окно достижений.", "Key that opens and closes the achievements window." },
            ["FontSize"] = new[] { "Размер шрифта окна", "Window font size", "Размер текста в окне достижений; строки списка и иконки растут вместе с ним.", "Text size in the achievements window; list rows and icons grow with it." },
            ["WindowWidth"] = new[] { "Ширина окна", "Window width", "Ширина окна достижений — меняется уголком в правом нижнем углу окна.", "Achievements window width — change it with the bottom-right corner of the window." },
            ["WindowHeight"] = new[] { "Высота окна", "Window height", "Высота окна достижений — меняется уголком в правом нижнем углу окна.", "Achievements window height — change it with the bottom-right corner of the window." },
            ["RevealSecrets"] = new[] { "Показывать секретные", "Reveal secrets", "Показывать названия и условия секретных достижений до их получения.", "Show names and requirements of secret achievements before unlocking them." },
            ["MarkUncrafted"] = new[] { "Звёздочки у несделанного", "Mark unmade items", "Помечать звёздочкой то, что ещё нужно для достижений: в меню молота — непостроенное, на станках (верстак, кузница, котёл и т.д.) — нескрафченное, в инвентаре и сундуках — трофеи, которые ещё не подбирал, еду, которую ещё не ел, рыбу, которую ещё не ловил, сырьё для блюд с вертела и решётки, которые ещё не готовил, и семена для посадок, которые ещё не делал. Alt + наведение на предмет со звёздочкой — для каких достижений он нужен.", "Mark with a star what is still needed for achievements: unbuilt pieces in the hammer menu, uncrafted items at crafting stations (workbench, forge, cauldron etc.), and in inventory and chests — trophies not yet picked up, food not yet eaten, fish not yet caught, raw ingredients for spit/grill dishes not yet cooked and seeds for plantings not yet made. Hold Alt and hover a starred item to see which achievements need it." },
            ["ToastsForAll"] = new[] { "Для всех достижений", "For all achievements", "Всплывающий счётчик для всех достижений. Если выключено — только для отслеживаемых.", "Progress popups for all achievements. When off — only for tracked ones." },
            ["SkipTracked"] = new[] { "Не показывать для отслеживаемых", "Skip tracked", "Не показывать всплывающий счётчик для отслеживаемых достижений — их прогресс и так виден в панели на экране.", "Don't show progress popups for tracked achievements — their progress is already visible in the on-screen panel." },
            ["ToastDuration"] = new[] { "Длительность (сек)", "Duration (sec)", "Сколько секунд висит всплывающий счётчик.", "How many seconds a progress popup stays on screen." },
            ["ToastY"] = new[] { "Позиция по вертикали", "Vertical position", "Отступ всплывающих счётчиков от верхнего края экрана.", "Offset of progress popups from the top of the screen." },
            ["ToastOpacity"] = new[] { "Непрозрачность фона (%)", "Background opacity (%)", "Насколько плотный фон у всплывающих счётчиков: 0 — полностью прозрачный, 100 — сплошной.", "How solid the popup background is: 0 — fully transparent, 100 — solid." },
            ["Enabled"] = new[] { "Показывать на экране", "Show on screen", "Показывать отслеживаемые достижения на экране.", "Show tracked achievements on screen." },
            ["PositionX"] = new[] { "Позиция по горизонтали", "Horizontal position", "Отступ списка отслеживаемых достижений от правого края экрана.", "Offset of the tracked list from the right edge of the screen." },
            ["PositionY"] = new[] { "Позиция по вертикали", "Vertical position", "Отступ списка отслеживаемых достижений от верхнего края экрана.", "Offset of the tracked list from the top of the screen." },
            ["ListLength"] = new[] { "Длина списка", "List length", "Сколько условий, которые можно выполнить сейчас, показывать под каждым отслеживаемым достижением. 0 — не показывать.", "How many currently doable requirements to list under each tracked achievement. 0 — don't list." },
            ["ShowAllRequirements"] = new[] { "Показывать все условия", "Show all requirements", "Выключено — под отслеживаемым достижением только условия, которые можно выполнить сейчас. Включено — все невыполненные, недоступные помечены «×».", "Off — only requirements you can complete right now are listed under a tracked achievement. On — all unfinished ones, unavailable marked with \"×\"." },
            ["Opacity"] = new[] { "Непрозрачность фона (%)", "Background opacity (%)", "Насколько плотный фон у списка отслеживаемых достижений: 0 — полностью прозрачный, 100 — сплошной.", "How solid the tracked list background is: 0 — fully transparent, 100 — solid." },
        };

        private static readonly Dictionary<string, string[]> Sections = new Dictionary<string, string[]>
        {
            ["General"] = new[] { "1. Общие", "1. General" },
            ["Popups"] = new[] { "2. Всплывающие счётчики", "2. Progress popups" },
            ["Tracker"] = new[] { "3. Отслеживание на экране", "3. On-screen tracker" },
        };

        private static readonly List<KeyValuePair<ConfigDefinition, ConfigurationManagerAttributes>> Attributes =
            new List<KeyValuePair<ConfigDefinition, ConfigurationManagerAttributes>>();

        /// <summary>Описание для Config.Bind: пока язык игры неизвестен — по-английски, потом Apply() перепишет.</summary>
        public static ConfigDescription Describe(string section, string key, AcceptableValueBase range = null, bool hidden = false)
        {
            // Order: Configuration Manager сортирует по убыванию — сохраняем порядок объявления.
            // hidden — служебное значение (например, размер окна), в окне настроек его не показываем.
            var attrs = new ConfigurationManagerAttributes { Order = -Attributes.Count, Browsable = hidden ? false : (bool?)null };
            Attributes.Add(new KeyValuePair<ConfigDefinition, ConfigurationManagerAttributes>(new ConfigDefinition(section, key), attrs));
            Fill(section, key, attrs, ru: false);
            return new ConfigDescription(attrs.Description, range, attrs);
        }

        private static void Fill(string section, string key, ConfigurationManagerAttributes attrs, bool ru)
        {
            if (Texts.TryGetValue(key, out string[] t))
            {
                attrs.DispName = ru ? t[0] : t[1];
                attrs.Description = ru ? t[2] : t[3];
            }
            if (Sections.TryGetValue(section, out string[] s)) attrs.Category = ru ? s[0] : s[1];
        }

        /// <summary>Переписывает подписи под текущий язык и просит Configuration Manager перестроить список.</summary>
        public static void Apply()
        {
            bool ru = Loc.Ru;
            foreach (KeyValuePair<ConfigDefinition, ConfigurationManagerAttributes> kv in Attributes)
            {
                Fill(kv.Key.Section, kv.Key.Key, kv.Value, ru);
            }

            // У официального Configuration Manager и его форков разные GUID (например, _shudnal.ConfigurationManager),
            // поэтому ищем по имени и по наличию BuildSettingList. Нет метода — подписи обновятся при следующем открытии окна.
            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                if (info?.Instance == null || info.Metadata.GUID.IndexOf("configurationmanager", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var build = AccessTools.Method(info.Instance.GetType(), "BuildSettingList");
                if (build == null || build.GetParameters().Length != 0) continue;
                try
                {
                    build.Invoke(info.Instance, null);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"Could not refresh {info.Metadata.GUID}: {e.Message}");
                }
            }
        }
    }
}
