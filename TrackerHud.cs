using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AchievementTracker
{
    /// <summary>Постоянный список закреплённых достижений справа и всплывающие счётчики сверху.</summary>
    internal static class TrackerHud
    {
        private class Toast
        {
            public string Id;
            public RectTransform Rt;
            public CanvasGroup Group;
            public Text Text;
            public float Born;
        }

        private class PinnedRow
        {
            public RectTransform Rt;
            public Text Title;
            public Text Sub;
            public RectTransform Fill;
        }

        private const int MaxToasts = 4;

        private static RectTransform s_root;
        private static RectTransform s_pinned;
        private static RectTransform s_toasts;
        private static readonly List<Toast> Toasts = new List<Toast>();
        private static readonly Dictionary<string, PinnedRow> Rows = new Dictionary<string, PinnedRow>();
        private static bool s_dirty = true;
        private static float s_nextRefresh;

        public static void MarkDirty() => s_dirty = true;

        /// <summary>Язык сменился — пересоздаём HUD при следующем кадре.</summary>
        public static void Rebuild()
        {
            if (s_root != null) Object.Destroy(s_root.gameObject);
            s_root = null;
            Rows.Clear();
            Toasts.Clear();
            s_dirty = true;
        }

        private static bool EnsureBuilt()
        {
            if (s_root != null) return true;
            if (Hud.instance == null || Hud.instance.m_rootObject == null) return false;
            Rows.Clear();
            Toasts.Clear();

            s_root = UiKit.Stretch(UiKit.Rect("AchievementTrackerHud", Hud.instance.m_rootObject.transform));

            s_pinned = UiKit.Rect("Pinned", s_root);
            s_pinned.anchorMin = s_pinned.anchorMax = new Vector2(1f, 1f);
            s_pinned.pivot = new Vector2(1f, 1f);
            s_pinned.sizeDelta = new Vector2(310f, 0f);
            UiKit.Bg(s_pinned.gameObject, BgColor(Plugin.HudOpacity.Value)).raycastTarget = false;
            VerticalLayoutGroup vlg = s_pinned.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 6, 8);
            vlg.spacing = 5f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            s_pinned.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text header = UiKit.Label(s_pinned, Loc.S("Отслеживаемые достижения", "Tracked achievements"), 14, UiKit.Orange, TextAnchor.MiddleLeft, title: true);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            s_toasts = UiKit.Rect("Toasts", s_root);
            s_toasts.anchorMin = s_toasts.anchorMax = new Vector2(0.5f, 1f);
            s_toasts.pivot = new Vector2(0.5f, 1f);
            s_toasts.sizeDelta = new Vector2(480f, 0f);
            VerticalLayoutGroup tv = s_toasts.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 6f;
            tv.childAlignment = TextAnchor.UpperCenter;
            tv.childControlWidth = true;
            tv.childControlHeight = true;
            tv.childForceExpandWidth = true;
            tv.childForceExpandHeight = false;
            s_toasts.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ApplyPositions();
            s_dirty = true;
            return true;
        }

        public static void ApplyPositions()
        {
            if (s_root == null) return;
            s_pinned.anchoredPosition = new Vector2(Plugin.HudX.Value, Plugin.HudY.Value);
            s_toasts.anchoredPosition = new Vector2(0f, Plugin.ToastY.Value);
            // Прозрачность меняется только у фона — текст и полоски остаются чёткими
            UiKit.Bg(s_pinned.gameObject, BgColor(Plugin.HudOpacity.Value));
            foreach (Toast t in Toasts)
            {
                if (t.Rt != null) UiKit.Bg(t.Rt.gameObject, BgColor(Plugin.ToastOpacity.Value));
            }
        }

        private static Color BgColor(int opacityPercent) => new Color(0f, 0f, 0f, Mathf.Clamp01(opacityPercent / 100f));

        public static void Tick()
        {
            if (Player.m_localPlayer == null || !Analyzer.Ready)
            {
                return;
            }
            if (!EnsureBuilt()) return;

            UpdateToasts();

            if (Time.time >= s_nextRefresh || s_dirty)
            {
                // Грязный флаг ставится на каждое изменение статистики — не пересчитываем чаще 4 раз в секунду
                if (s_dirty && Time.time < s_nextRefresh - 0.75f) return;
                s_dirty = false;
                s_nextRefresh = Time.time + 1f;
                RefreshPinned();
            }
        }

        // Выполненное отслеживаемое достижение висит на экране с «готово» столько секунд, потом снимается само
        private const float AutoUntrackDelay = 10f;

        // Когда отслеживаемое достижение впервые увидели выполненным
        private static readonly Dictionary<string, float> DoneSince = new Dictionary<string, float>();

        private static void AutoUntrackCompleted()
        {
            const float delay = AutoUntrackDelay;
            List<string> ids = Tracked.Ids();
            foreach (string id in DoneSince.Keys.ToList())
            {
                if (!ids.Contains(id)) DoneSince.Remove(id);
            }
            foreach (string id in ids)
            {
                Achievement a = Analyzer.Find(id);
                if (a == null || !a.m_unlocked) continue;
                if (!DoneSince.TryGetValue(id, out float since)) DoneSince[id] = since = Time.time;
                if (Time.time - since >= delay)
                {
                    DoneSince.Remove(id);
                    Tracked.Remove(id);
                }
            }
        }

        private static void RefreshPinned()
        {
            AutoUntrackCompleted();
            List<Achievement> achs = Plugin.HudEnabled.Value
                ? Tracked.Ids().Select(Analyzer.Find).Where(a => a != null).ToList()
                : new List<Achievement>();
            s_pinned.gameObject.SetActive(achs.Count > 0);

            foreach (string id in Rows.Keys.ToList())
            {
                if (achs.Any(a => a.m_id == id)) continue;
                Object.Destroy(Rows[id].Rt.gameObject);
                Rows.Remove(id);
            }

            foreach (Achievement a in achs)
            {
                if (!Rows.TryGetValue(a.m_id, out PinnedRow row)) Rows[a.m_id] = row = CreateRow();
                AchState st = Analyzer.Evaluate(a, withAvailability: !a.m_unlocked);
                Color c = UiKit.StatusColor(st.Status == AchStatus.Done ? AchStatus.Done : AchStatus.Available);
                string progress = a.m_unlocked ? Loc.S("готово", "done") : st.ProgressText;
                row.Title.text = $"{Names.Achievement(a)}  <color=#{UiKit.Hex(c)}>{progress}</color>";
                row.Sub.text = SubLine(st);
                row.Sub.gameObject.SetActive(!string.IsNullOrEmpty(row.Sub.text));
                UiKit.SetFill(row.Fill, st.Fraction, c);
            }
        }

        /// <summary>Под достижением — условия, которые можно выполнить прямо сейчас (не больше ListLength).</summary>
        private static string SubLine(AchState st)
        {
            if (st.Ach.m_unlocked || st.Hidden) return "";
            if (st.DifficultyLock != null) return st.DifficultyLock;
            int limit = Plugin.HudListLength.Value;
            if (limit <= 0) return "";
            if (st.Reqs.Count == 1)
            {
                ReqState only = st.Reqs[0];
                if (only.Met) return "";
                return IsExplore(only) ? only.Name + ": " + only.Reason : only.Name;
            }

            // По умолчанию — только то, что можно сделать сейчас; с галочкой ShowAllRequirements — все невыполненные,
            // доступные сверху, недоступные ниже с пометкой «×»
            bool showAll = Plugin.HudShowAll.Value;
            List<ReqState> shown = st.Reqs.Where(r => !r.Met && (showAll || r.Avail != Avail.Locked))
                .OrderBy(r => r.Avail == Avail.Locked ? 1 : 0)
                .ThenByDescending(r => Analyzer.ReqFraction(r.Req, r.Current)).ToList();
            if (!showAll && shown.Count == 0)
            {
                string why = st.Reqs.FirstOrDefault(r => !r.Met && !string.IsNullOrEmpty(r.Reason))?.Reason ?? "";
                return Loc.S("сейчас нечего выполнить", "nothing doable right now") + (why.Length > 0 ? ": " + why : "");
            }

            var lines = new List<string>();
            foreach (ReqState r in shown.Take(limit))
            {
                // Для сторон света полезнее живое расстояние до цели, чем «0/1»
                string v = IsExplore(r) ? ": " + r.Reason
                    : r.Req.Target > 1f ? $"  {Analyzer.Num(r.Current)}/{Analyzer.Num(r.Req.Target)}" : "";
                lines.Add(r.Avail == Avail.Locked
                    ? $"<color=#{UiKit.Hex(UiKit.Grey)}>× {r.Name}{v}</color>"
                    : "• " + r.Name + v);
            }
            int more = shown.Count - limit;
            if (more > 0) lines.Add(Loc.S($"…и ещё {more}", $"…and {more} more"));
            return string.Join("\n", lines);
        }

        private static bool IsExplore(ReqState r) =>
            r.Req.Kind == ReqKind.Stat && r.Req.Key.StartsWith("Explore") && !string.IsNullOrEmpty(r.Reason);

        private static PinnedRow CreateRow()
        {
            var row = new PinnedRow();
            row.Rt = UiKit.Rect("Row", s_pinned);
            VerticalLayoutGroup v = row.Rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            row.Title = UiKit.Label(row.Rt, "", 15, UiKit.Beige, TextAnchor.MiddleLeft);
            row.Sub = UiKit.Label(row.Rt, "", 12, UiKit.Grey, TextAnchor.MiddleLeft);
            RectTransform bar = UiKit.ProgressBar(row.Rt, out row.Fill);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 5f;
            return row;
        }

        public static void ShowToast(Achievement a, Requirement req, float cur)
        {
            if (!EnsureBuilt()) return;
            AchState st = Analyzer.Evaluate(a, withAvailability: false);
            string name = Names.Achievement(a);
            string text;
            if (st.Reqs.Count == 1)
            {
                text = $"<b>{name}</b>    <color=#{UiKit.Hex(UiKit.Yellow)}>{st.ProgressText}</color>";
            }
            else
            {
                string sub = Names.Requirement(req);
                string subVal = req.Target > 1f
                    ? $"  {Analyzer.Num(Mathf.Min(cur, req.Target))} / {Analyzer.Num(req.Target)}"
                    : (cur >= req.Target ? "  +" : "");
                text = $"<b>{name}</b>    <color=#{UiKit.Hex(UiKit.Yellow)}>{st.ProgressText}</color>\n<size=13><color=#{UiKit.Hex(UiKit.Grey)}>{sub}{subVal}</color></size>";
            }

            Toast t = Toasts.FirstOrDefault(x => x.Id == a.m_id);
            if (t == null)
            {
                if (Toasts.Count >= MaxToasts)
                {
                    Object.Destroy(Toasts[0].Rt.gameObject);
                    Toasts.RemoveAt(0);
                }
                t = CreateToast(a.m_id);
                Toasts.Add(t);
            }
            t.Text.text = text;
            t.Born = Time.time;
        }

        private static Toast CreateToast(string id)
        {
            var t = new Toast { Id = id };
            t.Rt = UiKit.Rect("Toast", s_toasts);
            UiKit.Bg(t.Rt.gameObject, BgColor(Plugin.ToastOpacity.Value)).raycastTarget = false;
            t.Group = t.Rt.gameObject.AddComponent<CanvasGroup>();
            t.Group.blocksRaycasts = false;
            t.Group.interactable = false;
            VerticalLayoutGroup v = t.Rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 6, 7);
            v.spacing = 4f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            t.Text = UiKit.Label(t.Rt, "", 17, UiKit.Beige, TextAnchor.MiddleCenter);
            return t;
        }

        private static void UpdateToasts()
        {
            float dur = Mathf.Max(1f, Plugin.ToastDuration.Value);
            for (int i = Toasts.Count - 1; i >= 0; i--)
            {
                Toast t = Toasts[i];
                if (t.Rt == null)
                {
                    Toasts.RemoveAt(i);
                    continue;
                }
                float age = Time.time - t.Born;
                if (age >= dur)
                {
                    Object.Destroy(t.Rt.gameObject);
                    Toasts.RemoveAt(i);
                    continue;
                }
                float alpha = age < 0.15f ? age / 0.15f : age > dur - 0.6f ? (dur - age) / 0.6f : 1f;
                t.Group.alpha = alpha;
            }
        }
    }
}
