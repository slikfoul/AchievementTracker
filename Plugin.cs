using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace AchievementTracker
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "Slikfoul.AchievementTracker";
        private const string OldGuid = "valheim.achievementtracker";
        public const string ModName = "AchievementTracker";
        public const string ModVersion = "1.1.0";

        public static ManualLogSource Log;

        public static ConfigEntry<KeyboardShortcut> PanelKey;
        public static ConfigEntry<bool> ToastsForAll;
        public static ConfigEntry<int> ToastDuration;
        public static ConfigEntry<int> ToastY;
        public static ConfigEntry<bool> HudEnabled;
        public static ConfigEntry<int> HudX;
        public static ConfigEntry<int> HudY;
        public static ConfigEntry<bool> RevealSecrets;
        public static ConfigEntry<UiLanguage> Language;
        public static ConfigEntry<AchNameMode> AchievementNames;
        public static ConfigEntry<int> ToastOpacity;
        public static ConfigEntry<int> HudOpacity;
        public static ConfigEntry<int> HudListLength;
        public static ConfigEntry<bool> HudShowAll;
        public static ConfigEntry<int> FontSize;
        public static ConfigEntry<int> WindowWidth;
        public static ConfigEntry<int> WindowHeight;
        public static ConfigEntry<bool> ToastsSkipTracked;
        public static ConfigEntry<bool> MarkUncrafted;

        private bool _configTextApplied;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            MigrateOldConfig();

            // Подписи и разделы в Configuration Manager задаёт ConfigText на языке мода;
            // диапазоны нужны, чтобы вместо полей ввода были ползунки
            Language = Config.Bind("General", "Language", UiLanguage.Game, ConfigText.Describe("General", "Language"));
            AchievementNames = Config.Bind("General", "AchievementNames", AchNameMode.Game, ConfigText.Describe("General", "AchievementNames"));
            AchievementNames.SettingChanged += (_, __) => Loc.RaiseChanged();
            PanelKey = Config.Bind("General", "PanelKey", new KeyboardShortcut(KeyCode.F7), ConfigText.Describe("General", "PanelKey"));
            FontSize = Config.Bind("General", "FontSize", 15,
                ConfigText.Describe("General", "FontSize", new AcceptableValueRange<int>(10, 24)));
            FontSize.SettingChanged += (_, __) => AchievementPanel.Rebuild();
            WindowWidth = Config.Bind("General", "WindowWidth", 1000, ConfigText.Describe("General", "WindowWidth", hidden: true));
            WindowHeight = Config.Bind("General", "WindowHeight", 700, ConfigText.Describe("General", "WindowHeight", hidden: true));
            RevealSecrets = Config.Bind("General", "RevealSecrets", true, ConfigText.Describe("General", "RevealSecrets"));
            MarkUncrafted = Config.Bind("General", "MarkUncrafted", false, ConfigText.Describe("General", "MarkUncrafted"));
            MarkUncrafted.SettingChanged += (_, __) => CraftMarks.MarkDirty();

            ToastsForAll = Config.Bind("Popups", "ToastsForAll", true, ConfigText.Describe("Popups", "ToastsForAll"));
            ToastsSkipTracked = Config.Bind("Popups", "SkipTracked", true, ConfigText.Describe("Popups", "SkipTracked"));
            ToastDuration = Config.Bind("Popups", "ToastDuration", 3,
                ConfigText.Describe("Popups", "ToastDuration", new AcceptableValueRange<int>(1, 15)));
            ToastY = Config.Bind("Popups", "ToastY", -120,
                ConfigText.Describe("Popups", "ToastY", new AcceptableValueRange<int>(-1000, 0)));
            ToastOpacity = Config.Bind("Popups", "ToastOpacity", 60,
                ConfigText.Describe("Popups", "ToastOpacity", new AcceptableValueRange<int>(0, 100)));

            HudEnabled = Config.Bind("Tracker", "Enabled", true, ConfigText.Describe("Tracker", "Enabled"));
            HudX = Config.Bind("Tracker", "PositionX", -20,
                ConfigText.Describe("Tracker", "PositionX", new AcceptableValueRange<int>(-1800, 0)));
            HudY = Config.Bind("Tracker", "PositionY", -300,
                ConfigText.Describe("Tracker", "PositionY", new AcceptableValueRange<int>(-1000, 0)));
            HudOpacity = Config.Bind("Tracker", "Opacity", 45,
                ConfigText.Describe("Tracker", "Opacity", new AcceptableValueRange<int>(0, 100)));
            HudListLength = Config.Bind("Tracker", "ListLength", 5,
                ConfigText.Describe("Tracker", "ListLength", new AcceptableValueRange<int>(0, 20)));
            HudListLength.SettingChanged += (_, __) => TrackerHud.MarkDirty();
            HudShowAll = Config.Bind("Tracker", "ShowAllRequirements", false, ConfigText.Describe("Tracker", "ShowAllRequirements"));
            HudShowAll.SettingChanged += (_, __) => TrackerHud.MarkDirty();

            HudX.SettingChanged += (_, __) => TrackerHud.ApplyPositions();
            HudY.SettingChanged += (_, __) => TrackerHud.ApplyPositions();
            ToastY.SettingChanged += (_, __) => TrackerHud.ApplyPositions();
            ToastOpacity.SettingChanged += (_, __) => TrackerHud.ApplyPositions();
            HudOpacity.SettingChanged += (_, __) => TrackerHud.ApplyPositions();
            Loc.Changed += ConfigText.Apply;
            Loc.Changed += CraftMarks.MarkDirty;
            HudEnabled.SettingChanged += (_, __) => TrackerHud.MarkDirty();
            Tracked.Changed += TrackerHud.MarkDirty;
            TrackedSkills.Changed += TrackerHud.MarkSkillsDirty;
            Language.SettingChanged += (_, __) => Loc.RaiseChanged();
            Localization.OnLanguageChange += Loc.RaiseChanged;
            Loc.Changed += AchievementPanel.Rebuild;
            Loc.Changed += TrackerHud.Rebuild;

            CommandManager.Instance.AddConsoleCommand(new AchCommand());

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(typeof(Patches));
            _harmony.PatchAll(typeof(CraftMarks));
            _harmony.PatchAll(typeof(SkillTrackingUi));
            Log.LogInfo($"{ModName} {ModVersion} loaded");
        }

        /// <summary>Плагин раньше назывался valheim.achievementtracker — переносим старые настройки в новый файл.</summary>
        private void MigrateOldConfig()
        {
            try
            {
                string oldPath = Path.Combine(BepInEx.Paths.ConfigPath, OldGuid + ".cfg");
                if (!File.Exists(oldPath) || File.Exists(Config.ConfigFilePath)) return;
                File.Copy(oldPath, Config.ConfigFilePath);
                Config.Reload();
                Log.LogInfo("Settings migrated from " + oldPath);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not migrate old settings: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            SkillTrackingUi.Dispose();
        }

        private float _nextErrorLog;

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                if (Time.time >= _nextErrorLog)
                {
                    _nextErrorLog = Time.time + 30f;
                    Log.LogError(e);
                }
            }
        }

        private void Tick()
        {
            if (GUIManager.IsHeadless()) return;
            // Язык игры известен только к главному меню — тогда и переводим подписи настроек
            if (!_configTextApplied && (FejdStartup.instance != null || Player.m_localPlayer != null))
            {
                _configTextApplied = true;
                ConfigText.Apply();
            }
            AchievementPanel.CheckAlive();
            LocationScanner.Tick(this);
            if (Player.m_localPlayer == null) return;

            if (AchievementPanel.IsOpen && ZInput.GetKeyDown(KeyCode.Escape, false))
            {
                AchievementPanel.Close();
            }
            else if (Pressed(PanelKey.Value) && CanUseHotkey())
            {
                AchievementPanel.Toggle();
            }

            AchievementPanel.HandleScroll();
            TrackerHud.Tick();
            StarTooltip.Tick();
        }

        private static bool Pressed(KeyboardShortcut s)
        {
            if (s.MainKey == KeyCode.None || !ZInput.GetKeyDown(s.MainKey, false)) return false;
            return s.Modifiers.All(m => ZInput.GetKey(m, false));
        }

        private static bool CanUseHotkey()
        {
            if (AchievementPanel.IsOpen) return true;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible()) return false;
            return true;
        }

        public static void Dump()
        {
            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "AchievementTracker_dump.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"World difficulty: {Achievements.GetCurrentAchievementDifficulty()}  CanGetAchievements: {Achievements.CanGetAchievements()}");
            foreach (Achievement a in Analyzer.All())
            {
                AchState st = Analyzer.Evaluate(a, withAvailability: true);
                sb.AppendLine();
                sb.AppendLine($"[{a.m_id}] {Names.L(a.m_name)} — {Names.L(a.m_description)}");
                sb.AppendLine($"  secret={a.m_isSecret} difficulty={a.m_difficultyRequirement} unlocked={a.m_unlocked} status={st.Status} progress={st.ProgressText} summary={st.Summary}");
                foreach (ReqState r in st.Reqs)
                {
                    sb.AppendLine($"  - {r.Req.Kind} '{r.Req.Key}' mod={r.Req.Modifier} op={r.Req.Op} target={r.Req.Target} cur={(r.HasValue ? r.Current.ToString() : "-")} met={r.Met} avail={r.Avail} | {r.Name} | {r.Reason}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("== Creature home biomes (as used for hints) ==");
            foreach (KeyValuePair<string, Heightmap.Biome> kv in Progression.CreatureHomes().OrderBy(k => k.Key))
            {
                sb.AppendLine($"  {kv.Key} ({Names.L(kv.Key)}): {kv.Value}");
            }
            sb.AppendLine();
            sb.AppendLine($"== Dungeon/location creatures (scan ready: {LocationScanner.Ready}) ==");
            foreach (KeyValuePair<string, List<Place>> kv in LocationScanner.All().OrderBy(k => k.Key))
            {
                sb.AppendLine($"  {kv.Key} ({Names.L(kv.Key)}): " +
                              string.Join("; ", kv.Value.Select(p => $"{p.Label ?? "-"} [{p.Biome}]")));
            }
            sb.AppendLine();
            sb.AppendLine("== Creature origins (fight / breed / hatch / grow up) ==");
            foreach (string line in Progression.OriginReport()) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine("== Boss altars ==");
            foreach (KeyValuePair<string, List<Place>> kv in LocationScanner.All())
            {
                string item = LocationScanner.AltarItem(kv.Key);
                if (item != null) sb.AppendLine($"  {kv.Key} ({Names.L(kv.Key)}): {item} ({Names.L(item)})");
            }
            sb.AppendLine();
            sb.AppendLine("== Raw spawn data ==");
            foreach (string line in Progression.SpawnReport()) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine("== Scanned locations (last scan in this session) ==");
            foreach (string line in LocationScanner.Debug) sb.AppendLine("  " + line);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Log.LogInfo("Achievements dumped to " + path);
            global::Console.instance?.Print(Loc.S("Достижения выгружены в ", "Achievements dumped to ") + path);
        }
    }

    internal class AchCommand : ConsoleCommand
    {
        public override string Name => "achtracker";

        public override string Help => "open the achievements window; 'achtracker dump' — dump all achievements to a file; 'achtracker rescan' — rescan dungeons and locations";

        public override void Run(string[] args)
        {
            if (!Analyzer.Ready)
            {
                global::Console.instance?.Print(Loc.S("Достижения ещё не загружены — зайди в мир", "Achievements are not loaded yet — enter a world"));
                return;
            }
            if (args.Length > 0 && args[0].Equals("rescan", StringComparison.OrdinalIgnoreCase))
            {
                LocationScanner.ForceRescan();
                global::Console.instance?.Print(Loc.S("Поиск по локациям запущен заново — это займёт около минуты", "Location scan restarted — it takes about a minute"));
                return;
            }
            if (args.Length > 0 && args[0].Equals("dump", StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Dump();
                return;
            }
            AchievementPanel.Toggle();
        }

        public override List<string> CommandOptionList() => new List<string> { "dump", "rescan" };
    }
}
