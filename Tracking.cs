using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker
{
    /// <summary>Отслеживаемые достижения. Хранятся в данных персонажа, поэтому у каждого персонажа свои.</summary>
    public static class Tracked
    {
        private const string Key = "AchievementTracker.tracked";

        public static event Action Changed;

        public static List<string> Ids()
        {
            Player p = Player.m_localPlayer;
            if (p == null || !p.m_customData.TryGetValue(Key, out string raw) || string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static bool Contains(string id) => Ids().Contains(id);

        public static void Remove(string id)
        {
            if (Contains(id)) Toggle(id);
        }

        public static void Toggle(string id)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            List<string> ids = Ids();
            if (!ids.Remove(id)) ids.Add(id);
            p.m_customData[Key] = string.Join(";", ids);
            Changed?.Invoke();
        }
    }

    /// <summary>Реакция на изменение статистики: решаем, показывать ли всплывающий счётчик.</summary>
    public static class StatEvents
    {
        // Без ValueTuple: в Mono игры нет System.ValueTuple.dll
        private class Hit
        {
            public Achievement Ach;
            public Requirement Req;
        }

        private static readonly Dictionary<string, List<Hit>> Index = new Dictionary<string, List<Hit>>();
        private static int s_indexedReqCount = -1;
        private static Achievements s_indexedFor;
        private static readonly Dictionary<string, float> LastSetValues = new Dictionary<string, float>();

        private static string K(ReqKind kind, string key) => kind + "|" + key;

        private static void EnsureIndex()
        {
            // Динамические достижения (скрафтить всё и т.п.) заполняются при инициализации — следим за размером
            int count = 0;
            foreach (Achievement a in Analyzer.All()) count += Analyzer.RequirementCount(a);
            if (s_indexedFor == Achievements.m_instance && count == s_indexedReqCount) return;
            Index.Clear();
            foreach (Achievement a in Analyzer.All())
            {
                foreach (Requirement r in Analyzer.GetRequirements(a))
                {
                    if (r.Kind == ReqKind.OtherAchievement) continue;
                    string k = K(r.Kind, r.Key);
                    if (!Index.TryGetValue(k, out List<Hit> list)) Index[k] = list = new List<Hit>();
                    list.Add(new Hit { Ach = a, Req = r });
                }
            }
            s_indexedFor = Achievements.m_instance;
            s_indexedReqCount = count;
        }

        private static float s_lastIndexCheck;

        public static void OnStat(ReqKind kind, string key, float amount, bool isSet, bool cheated)
        {
            if (!Analyzer.Ready || Player.m_localPlayer == null || string.IsNullOrEmpty(key)) return;
            if (kind != ReqKind.Stat && kind != ReqKind.Enemy) CraftMarks.MarkDirty();
            // Игра не засчитала это в достижения (читы, «грязный» мир) — прогресс не изменился
            if (!Achievements.CanGetAchievements(cheated)) return;
            try
            {
                // Проверка размера индекса стоит денег — не чаще раза в пару секунд
                if (s_indexedFor != Achievements.m_instance || UnityEngine.Time.time - s_lastIndexCheck > 2f)
                {
                    s_lastIndexCheck = UnityEngine.Time.time;
                    EnsureIndex();
                }
                if (!Index.TryGetValue(K(kind, key), out List<Hit> hits)) return;

                TrackerHud.MarkDirty();
                bool showAll = Plugin.ToastsForAll.Value;
                List<string> tracked = Tracked.Ids();
                foreach (Hit hit in hits)
                {
                    Achievement ach = hit.Ach;
                    Requirement req = hit.Req;
                    if (ach.m_unlocked || Analyzer.IsHidden(ach)) continue;
                    if (req.Op != RequirementOperator.AboveEquals) continue;
                    bool isTracked = tracked.Contains(ach.m_id);
                    if (!isTracked && !showAll) continue;
                    // Прогресс отслеживаемых и так виден в панели на экране
                    if (isTracked && Plugin.ToastsSkipTracked.Value) continue;

                    Analyzer.TryGetValue(req, Analyzer.StatsFor(ach), out float cur);
                    float before;
                    if (isSet)
                    {
                        string lk = ach.m_id + "|" + key;
                        before = LastSetValues.TryGetValue(lk, out float prev) ? prev : cur;
                        LastSetValues[lk] = cur;
                        if (Math.Abs(before - cur) < 0.001f) continue;
                    }
                    else
                    {
                        before = cur - amount;
                    }
                    if (before >= req.Target) continue; // это условие уже было выполнено

                    // Дробные приращения (дистанции и т.п.) — показываем только каждый процент
                    bool whole = amount >= 1f && Math.Abs(amount - Math.Round(amount)) < 0.001f;
                    if (!whole && req.Target > 0f)
                    {
                        int b0 = (int)(before / req.Target * 100f);
                        int b1 = (int)(cur / req.Target * 100f);
                        if (b0 == b1) continue;
                    }

                    if (Analyzer.DifficultyLock(ach, Analyzer.GetRequirements(ach)) != null) continue;
                    TrackerHud.ShowToast(ach, req, cur);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to handle stat {kind}/{key}: {e}");
            }
        }
    }
}
