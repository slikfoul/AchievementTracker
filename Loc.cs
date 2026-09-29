using System;

namespace AchievementTracker
{
    public enum UiLanguage
    {
        Game,
        English,
        Russian
    }

    /// <summary>На каком языке показывать названия достижений.</summary>
    public enum AchNameMode
    {
        Game,
        English,
        Russian,
        Both
    }

    /// <summary>Строки интерфейса мода. Названия достижений, предметов и монстров берутся из локализации игры.</summary>
    internal static class Loc
    {
        public static event Action Changed;

        public static bool Ru
        {
            get
            {
                switch (Plugin.Language.Value)
                {
                    case UiLanguage.Russian: return true;
                    case UiLanguage.English: return false;
                    default:
                        string lang = Localization.instance?.GetSelectedLanguage();
                        return lang == "Russian" || lang == "Ukrainian";
                }
            }
        }

        public static string S(string ru, string en) => Ru ? ru : en;

        public static void RaiseChanged() => Changed?.Invoke();
    }
}
