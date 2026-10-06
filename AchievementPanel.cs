using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AchievementTracker
{
    internal static class AchievementPanel
    {
        private enum Filter { Available, Partial, Locked, Done, Tracked, All }

        private static string[] FilterNames => Loc.Ru
            ? new[] { "Можно выполнить", "Частично", "Недоступно", "Выполнено", "Отслеживается", "Все" }
            : new[] { "Doable now", "Partly", "Locked", "Completed", "Tracked", "All" };

        // Минимум — чтобы помещался ряд кнопок фильтров
        private const float MinW = 960f;
        private const float MinH = 520f;
        // Доля ширины под список, остальное — подробности
        private const float Split = 0.56f;

        /// <summary>Масштаб текста и строк списка по настройке «Размер шрифта окна» (15 — обычный).</summary>
        private static float F => Mathf.Max(0.5f, Plugin.FontSize.Value / 15f);

        private static int Fs(float basePx) => Mathf.RoundToInt(basePx * F);

        private static GameObject s_root;
        private static bool s_open;
        private static int s_closedFrame = -1;
        private static Filter s_filter = Filter.Available;
        private static string s_selectedId;

        private static Text s_summary;
        private static Button[] s_filterButtons;
        private static RectTransform s_listContent;
        private static ScrollRect s_listScroll;
        private static RectTransform s_detailsContent;
        private static ScrollRect s_detailsScroll;
        private static Button s_trackButton;
        private static List<AchState> s_states = new List<AchState>();

        public static bool IsOpen => s_open && s_root != null;

        /// <summary>Esc в этом кадре закрыл панель — меню игры открывать не нужно.</summary>
        public static bool JustClosed => Time.frameCount - s_closedFrame <= 1;

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (!Analyzer.Ready || Player.m_localPlayer == null || GUIManager.CustomGUIFront == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Loc.S("Достижения ещё не загрузились", "Achievements are not loaded yet"));
                return;
            }
            if (s_root == null) Build();
            s_root.SetActive(true);
            if (!s_open) GUIManager.BlockInput(true);
            s_open = true;
            Refresh();
        }

        /// <summary>Язык сменился — пересоздаём окно с новыми подписями.</summary>
        public static void Rebuild()
        {
            bool wasOpen = IsOpen;
            if (wasOpen) Close();
            if (s_root != null) UnityEngine.Object.Destroy(s_root);
            s_root = null;
            if (wasOpen) Open();
        }

        public static void Close()
        {
            if (s_root != null) s_root.SetActive(false);
            if (s_open) GUIManager.BlockInput(false);
            s_open = false;
            s_closedFrame = Time.frameCount;
        }

        /// <summary>
        /// Чувствительность колеса берём у родного списка достижений игры, чтобы прокрутка была как в игровых окнах.
        /// </summary>
        private static float NativeScrollSensitivity()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null) return -1f;
            ScrollRect sr = null;
            if (gui.m_achievementsListRoot != null) sr = gui.m_achievementsListRoot.GetComponentInParent<ScrollRect>(true);
            if (sr == null && gui.m_recipeListRoot != null) sr = gui.m_recipeListRoot.GetComponentInParent<ScrollRect>(true);
            return sr != null ? sr.scrollSensitivity : -1f;
        }

        private static bool s_nativeScroll;

        /// <summary>
        /// Запасной вариант, если родной список не нашёлся: через новый Input System ScrollRect получает
        /// крошечные дельты, поэтому крутим сами фиксированным шагом.
        /// </summary>
        public static void HandleScroll()
        {
            if (!IsOpen || s_nativeScroll) return;
            float d = ZInput.GetMouseScrollWheel();
            if (Mathf.Abs(d) < 0.0001f) return;
            Vector2 mouse = ZInput.pointerPosition;
            ScrollRect target = null;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s_listScroll.transform, mouse, null)) target = s_listScroll;
            else if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s_detailsScroll.transform, mouse, null)) target = s_detailsScroll;
            if (target == null) return;

            RectTransform content = target.content;
            float max = Mathf.Max(0f, content.rect.height - target.viewport.rect.height);
            if (max <= 0f) return;
            float y = content.anchoredPosition.y - Mathf.Sign(d) * 80f;
            target.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
        }

        /// <summary>Сцена сменилась (выход в меню) — UI уничтожен вместе с CustomGUIFront.</summary>
        public static void CheckAlive()
        {
            if (s_open && s_root == null)
            {
                s_open = false;
                GUIManager.BlockInput(false);
            }
        }

        private static void Build()
        {
            float w = Mathf.Max(MinW, Plugin.WindowWidth.Value);
            float h = Mathf.Max(MinH, Plugin.WindowHeight.Value);
            s_root = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w, h, true);
            s_root.name = "AchievementTrackerPanel";
            Transform t = s_root.transform;

            // Всё привязано к краям окна, чтобы его можно было растягивать
            Text title = UiKit.Label(t, Names.L("$inventory_achievements"), 34, UiKit.Orange, TextAnchor.MiddleCenter, title: true);
            TopStretch(title.rectTransform, 0, 14, 0, 44);

            s_summary = UiKit.Label(t, "", Fs(16), UiKit.Beige, TextAnchor.MiddleCenter);
            TopStretch(s_summary.rectTransform, 0, 58, 0, 26);

            s_filterButtons = new Button[FilterNames.Length];
            // Ширина под длину подписи вместе со счётчиком «(12)»; ряд держится по центру
            float[] widths = { 200f, 130f, 145f, 140f, 185f, 100f };
            float gap = 8f;
            float x = -(widths.Sum() + gap * (FilterNames.Length - 1)) / 2f;
            for (int i = 0; i < FilterNames.Length; i++)
            {
                Filter f = (Filter)i;
                s_filterButtons[i] = UiKit.Button(t, FilterNames[i], 0, 0, widths[i], 34, () =>
                {
                    s_filter = f;
                    s_selectedId = null;
                    Refresh();
                    s_listScroll.verticalNormalizedPosition = 1f;
                });
                RectTransform brt = (RectTransform)s_filterButtons[i].transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(x, -92f);
                x += widths[i] + gap;
            }

            RectTransform list = UiKit.Region(UiKit.Rect("List", t), new Vector2(0f, 0f), new Vector2(Split, 1f),
                new Vector2(26f, 68f), new Vector2(-6f, -138f));
            s_listScroll = UiKit.ScrollList(list, out s_listContent);

            s_trackButton = UiKit.Button(t, Loc.S("Отслеживать", "Track"), 0, 0, 100, 36, () =>
            {
                if (string.IsNullOrEmpty(s_selectedId)) return;
                Tracked.Toggle(s_selectedId);
                Refresh();
            });
            UiKit.Region((RectTransform)s_trackButton.transform, new Vector2(Split, 1f), new Vector2(1f, 1f),
                new Vector2(8f, -174f), new Vector2(-26f, -138f));

            RectTransform details = UiKit.Region(UiKit.Rect("Details", t), new Vector2(Split, 0f), new Vector2(1f, 1f),
                new Vector2(8f, 68f), new Vector2(-26f, -182f));
            s_detailsScroll = UiKit.ScrollList(details, out s_detailsContent);
            s_detailsContent.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(8, 8, 8, 8);

            float native = NativeScrollSensitivity();
            s_nativeScroll = native > 0f;
            if (s_nativeScroll)
            {
                s_listScroll.scrollSensitivity = native;
                s_detailsScroll.scrollSensitivity = native;
            }

            string key = Plugin.PanelKey.Value.ToString();
            Text hint = UiKit.Label(t, Loc.S($"{key} — открыть/закрыть · клик по достижению — подробности · уголок справа внизу — размер окна",
                $"{key} — open/close · click an achievement for details · bottom-right corner — resize"), 13, UiKit.Grey, TextAnchor.MiddleLeft);
            UiKit.Pin(hint.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 22f), new Vector2(640f, 30f));

            Button close = UiKit.Button(t, Loc.S("Закрыть", "Close"), 0, 0, 160, 36, Close);
            UiKit.Pin((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(-34f, 18f), new Vector2(160f, 36f));

            // Уголок для растягивания; новый размер запоминаем в настройках
            RectTransform grip = UiKit.Pin(UiKit.Rect("ResizeGrip", t), new Vector2(1f, 0f), new Vector2(-6f, 6f), new Vector2(24f, 24f));
            Image gripImg = grip.gameObject.AddComponent<Image>();
            gripImg.sprite = UiKit.GripSprite();
            ResizeHandle handle = grip.gameObject.AddComponent<ResizeHandle>();
            handle.Target = (RectTransform)s_root.transform;
            handle.Min = new Vector2(MinW, MinH);
            handle.OnResized = size =>
            {
                Plugin.WindowWidth.Value = Mathf.RoundToInt(size.x);
                Plugin.WindowHeight.Value = Mathf.RoundToInt(size.y);
            };
        }

        public static void Refresh()
        {
            if (!IsOpen || !Analyzer.Ready) return;
            s_states = Analyzer.All().Select(a => Analyzer.Evaluate(a, withAvailability: true)).ToList();
            List<string> tracked = Tracked.Ids();

            int total = s_states.Count;
            int done = s_states.Count(s => s.Status == AchStatus.Done);
            int avail = s_states.Count(s => s.Status == AchStatus.Available);
            int partial = s_states.Count(s => s.Status == AchStatus.Partial);
            int locked = s_states.Count(s => s.Status == AchStatus.Locked);
            s_summary.text = $"{Loc.S("Выполнено", "Completed")} <color=#{UiKit.Hex(UiKit.Green)}>{done}</color> {Loc.S("из", "of")} {total}   ·   " +
                             $"{Loc.S("можно выполнить", "doable now")} <color=#{UiKit.Hex(UiKit.Yellow)}>{avail}</color>   ·   " +
                             $"{Loc.S("частично", "partly")} <color=#{UiKit.Hex(UiKit.Orange)}>{partial}</color>   ·   " +
                             $"{Loc.S("недоступно", "locked")} <color=#{UiKit.Hex(UiKit.Grey)}>{locked}</color>";

            int[] counts = { avail, partial, locked, done, tracked.Count, total };
            for (int i = 0; i < s_filterButtons.Length; i++)
            {
                bool active = (int)s_filter == i;
                UiKit.SetButtonText(s_filterButtons[i], $"{FilterNames[i]} ({counts[i]})", active ? UiKit.Yellow : UiKit.Beige);
            }

            List<AchState> shown = s_states.Where(s => Matches(s, tracked))
                .OrderByDescending(s => tracked.Contains(s.Ach.m_id))
                .ThenByDescending(s => s.Status == AchStatus.Done ? -1f : s.Fraction)
                .ToList();

            foreach (Transform child in s_listContent) UnityEngine.Object.Destroy(child.gameObject);
            if (shown.Count == 0)
            {
                Text empty = UiKit.Label(s_listContent, Loc.S("Здесь пусто", "Nothing here"), Fs(16), UiKit.Grey, TextAnchor.MiddleCenter);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            }
            if (s_selectedId == null || shown.All(s => s.Ach.m_id != s_selectedId))
            {
                s_selectedId = shown.FirstOrDefault()?.Ach.m_id;
            }
            foreach (AchState st in shown) BuildRow(st, tracked.Contains(st.Ach.m_id));

            UpdateDetails();
        }

        private static bool Matches(AchState s, List<string> tracked)
        {
            switch (s_filter)
            {
                case Filter.Available: return s.Status == AchStatus.Available;
                case Filter.Partial: return s.Status == AchStatus.Partial;
                case Filter.Locked: return s.Status == AchStatus.Locked;
                case Filter.Done: return s.Status == AchStatus.Done;
                case Filter.Tracked: return tracked.Contains(s.Ach.m_id);
                default: return true;
            }
        }

        private static void BuildRow(AchState st, bool tracked)
        {
            Achievement a = st.Ach;
            bool selected = a.m_id == s_selectedId;
            Color statusColor = UiKit.StatusColor(st.Status);

            // Размеры строки растут вместе со шрифтом, чтобы текст не налезал
            float f = F;
            float iconSize = 48f * f;
            float textLeft = 16f + iconSize;
            float nameTop = 6f * f, nameH = 24f * f;
            float subTop = nameTop + nameH - 1f, subH = 20f * f;
            float rowH = Mathf.Ceil(subTop + subH + 15f);

            RectTransform row = UiKit.Rect("Row", s_listContent);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = rowH;
            Image bg = UiKit.Bg(row.gameObject, selected ? new Color(0.5f, 0.33f, 0.1f, 0.8f) : new Color(0f, 0f, 0f, 0.35f));
            Button btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            string id = a.m_id;
            btn.onClick.AddListener(() =>
            {
                s_selectedId = id;
                Refresh();
            });

            Sprite sprite = st.Hidden && InventoryGui.instance != null ? InventoryGui.instance.m_secretAchievementIcon
                : a.m_unlocked ? a.m_icon : (a.m_iconLocked != null ? a.m_iconLocked : a.m_icon);
            if (sprite != null)
            {
                RectTransform icon = UiKit.TopLeft(UiKit.Rect("Icon", row), 7, (rowH - iconSize) / 2f - 2f, iconSize, iconSize);
                Image ii = icon.gameObject.AddComponent<Image>();
                ii.sprite = sprite;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }

            string pin = tracked ? $"<color=#{UiKit.Hex(UiKit.Yellow)}>[{Loc.S("отслеживается", "tracked")}]</color> " : "";
            Text name = UiKit.Label(row, pin + Names.Achievement(a), Fs(17), statusColor, TextAnchor.UpperLeft);
            TopStretch(name.rectTransform, textLeft, nameTop, 110f * f, nameH);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            string line2 = UiKit.StatusText(st.Status) + (string.IsNullOrEmpty(st.Summary) ? "" : " — " + st.Summary);
            Text sub = UiKit.Label(row, line2, Fs(13), UiKit.Grey, TextAnchor.UpperLeft);
            TopStretch(sub.rectTransform, textLeft, subTop, 12, subH);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            sub.verticalOverflow = VerticalWrapMode.Truncate;

            Text prog = UiKit.Label(row, st.Status == AchStatus.Done ? Loc.S("готово", "done") : st.ProgressText, Fs(16), statusColor, TextAnchor.UpperRight);
            prog.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Pin(prog.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -nameTop), new Vector2(110f * f, nameH));

            RectTransform bar = UiKit.ProgressBar(row, out RectTransform fill);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(textLeft, 6f);
            bar.offsetMax = new Vector2(-10f, 11f);
            UiKit.SetFill(fill, st.Fraction, statusColor);
        }

        private static void TopStretch(RectTransform rt, float left, float top, float right, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void UpdateDetails()
        {
            AchState st = s_states.FirstOrDefault(s => s.Ach.m_id == s_selectedId);
            if (st == null)
            {
                SetDetails(new List<string> { Loc.S("Выбери достижение слева.", "Select an achievement on the left.") });
                s_trackButton.interactable = false;
                UiKit.SetButtonText(s_trackButton, Loc.S("Отслеживать", "Track"));
                return;
            }

            bool tracked = Tracked.Contains(st.Ach.m_id);
            // Выполненное достижение отслеживать незачем, но если оно уже отслеживается — снять можно всегда
            s_trackButton.interactable = tracked || st.Status != AchStatus.Done;
            UiKit.SetButtonText(s_trackButton, tracked ? Loc.S("Не отслеживать", "Untrack") : Loc.S("Отслеживать (показывать на экране)", "Track (show on screen)"));

            SetDetails(BuildDetails(st));
            s_detailsScroll.verticalNormalizedPosition = 1f;
        }

        private static string C(Color c, string s) => $"<color=#{UiKit.Hex(c)}>{s}</color>";

        /// <summary>
        /// Каждый абзац — отдельный Text: у одного Text лимит 65000 вершин (с обводкой это ~3000 символов),
        /// а при переполнении Unity ломает перестройку шрифта и портит остальные надписи.
        /// </summary>
        private static void SetDetails(List<string> blocks)
        {
            foreach (Transform child in s_detailsContent) UnityEngine.Object.Destroy(child.gameObject);
            foreach (string b in blocks) UiKit.Label(s_detailsContent, b, Fs(15), UiKit.Beige, TextAnchor.UpperLeft);
        }

        private static List<string> BuildDetails(AchState st)
        {
            Achievement a = st.Ach;
            var sb = new StringBuilder();
            sb.AppendLine($"<size={Fs(22)}>{C(UiKit.Orange, Names.Achievement(a))}</size>");
            string desc = Names.Description(a);
            if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"<i>{desc}</i>");
            string guide = st.Hidden ? "" : Guide.Get(st);
            if (!string.IsNullOrEmpty(guide))
            {
                sb.AppendLine();
                sb.AppendLine(C(UiKit.Orange, Loc.S("<b>Как выполнить</b>", "<b>How to get it</b>")));
                sb.AppendLine(guide);
            }
            sb.AppendLine();
            sb.AppendLine(Loc.S("Статус: ", "Status: ") + C(UiKit.StatusColor(st.Status), UiKit.StatusText(st.Status)));
            if (st.Status != AchStatus.Done) sb.AppendLine(Loc.S("Прогресс: ", "Progress: ") + st.ProgressText);
            if (!string.IsNullOrEmpty(st.DifficultyLock) && st.Status != AchStatus.Done) sb.AppendLine(C(UiKit.Red, st.DifficultyLock));
            if (a.m_difficultyRequirement > DifficultyRequirement.Any) sb.AppendLine(Loc.S("Сложность: ", "Difficulty: ") + Names.Difficulty(a.m_difficultyRequirement));

            if (st.Hidden)
            {
                sb.AppendLine();
                sb.AppendLine(C(UiKit.Grey, Loc.S("Секретное достижение. Чтобы видеть условия, включи RevealSecrets в конфиге мода.", "Secret achievement. Enable RevealSecrets in the mod config to see its requirements.")));
                return new List<string> { sb.ToString().TrimEnd() };
            }

            if (st.Reqs.Count == 0) return new List<string> { sb.ToString().TrimEnd() };
            sb.AppendLine();
            sb.Append(C(UiKit.Beige, Loc.S("<b>Условия:</b>", "<b>Requirements:</b>")));
            var blocks = new List<string> { sb.ToString() };

            IEnumerable<ReqState> ordered = st.Reqs
                .OrderBy(r => r.Met ? 3 : r.Avail == Avail.Available ? 0 : r.Avail == Avail.Unknown ? 1 : 2)
                .ThenByDescending(r => Analyzer.ReqFraction(r.Req, r.Current));
            // Показываем все условия целиком, без сокращений
            foreach (ReqState r in ordered) blocks.Add(ReqLine(r));
            return blocks;
        }

        private static string ReqLine(ReqState r)
        {
            string value = "";
            if (r.Req.Kind != ReqKind.OtherAchievement)
            {
                if (r.Req.Op == RequirementOperator.AboveEquals)
                {
                    if (r.Req.Target > 1f) value = $"  {Analyzer.Num(Math.Min(r.Current, r.Req.Target))} / {Analyzer.Num(r.Req.Target)}";
                }
                else
                {
                    string op = r.Req.Op == RequirementOperator.BelowEquals ? "<=" : r.Req.Op == RequirementOperator.Equals ? "=" : "!=";
                    value = Loc.S($"  (сейчас {(r.HasValue ? Analyzer.Num(r.Current) : "—")}, нужно {op} {Analyzer.Num(r.Req.Target)})", $"  (now {(r.HasValue ? Analyzer.Num(r.Current) : "—")}, need {op} {Analyzer.Num(r.Req.Target)})");
                }
            }

            if (r.Met) return C(UiKit.Green, "+ " + r.Name + value);
            string reason = string.IsNullOrEmpty(r.Reason) ? "" : "\n      " + C(UiKit.Grey, $"<size={Fs(13)}>{r.Reason}</size>");
            switch (r.Avail)
            {
                case Avail.Available: return C(UiKit.Yellow, "• " + r.Name + value) + reason;
                case Avail.Unknown: return C(UiKit.Orange, "? " + r.Name + value) + reason;
                default: return C(UiKit.Grey, "× " + r.Name + value) + reason;
            }
        }
    }
}
