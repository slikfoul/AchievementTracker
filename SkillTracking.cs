using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AchievementTracker
{
    /// <summary>Выбор навыков хранится отдельно от достижений в данных каждого персонажа.</summary>
    internal static class TrackedSkills
    {
        private const string Key = "AchievementTracker.skills";
        private static Player s_player;
        private static string s_raw;
        private static readonly List<Skills.SkillType> Selected = new List<Skills.SkillType>();

        public static event Action Changed;

        public static IReadOnlyList<Skills.SkillType> Ids()
        {
            Player player = Player.m_localPlayer;
            string raw = "";
            if (player != null && player.m_customData.TryGetValue(Key, out string saved)) raw = saved ?? "";
            if (ReferenceEquals(player, s_player) && raw == s_raw) return Selected;
            s_player = player;
            s_raw = raw;
            Selected.Clear();
            foreach (string value in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ||
                    id <= 0 || id == (int)Skills.SkillType.All) continue;
                var type = (Skills.SkillType)id;
                if (!Selected.Contains(type)) Selected.Add(type);
            }
            return Selected;
        }

        public static bool Contains(Skills.SkillType type) => Ids().Contains(type);

        public static void Toggle(Skills.SkillType type) => Set(type, !Contains(type));

        public static void Set(Skills.SkillType type, bool tracked)
        {
            Player player = Player.m_localPlayer;
            if (player == null || type == Skills.SkillType.None || type == Skills.SkillType.All) return;
            var ids = new List<Skills.SkillType>(Ids());
            bool changed;
            if (tracked)
            {
                changed = !ids.Contains(type);
                if (changed) ids.Add(type);
            }
            else changed = ids.Remove(type);
            if (!changed) return;
            player.m_customData[Key] = string.Join(";", ids.Select(id => ((int)id).ToString(CultureInfo.InvariantCulture)));
            Changed?.Invoke();
        }

        public static string Name(Skills.SkillType type) =>
            GameText.Localize("$skill_" + type.ToString().ToLowerInvariant());
    }

    internal sealed class SkillTrackTarget : MonoBehaviour
    {
        public Skills.SkillType Type;
    }

    /// <summary>Галочки в левом отступе окна; их вертикальная обрезка совпадает со списком навыков.</summary>
    internal sealed class SkillTrackingPanel : MonoBehaviour, IScrollHandler
    {
        private sealed class Row
        {
            public GameObject Element;
            public RectTransform Icon;
            public Skills.SkillType Type;
            public Toggle Toggle;
            public UITooltip Tooltip;
        }

        private readonly List<Row> _rows = new List<Row>();
        private RectTransform _layer;
        private RectTransform _viewport;
        private RectTransform _dialog;
        private ScrollRect _scroll;

        private void OnEnable()
        {
            TrackedSkills.Changed += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            TrackedSkills.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        public void Setup(Player player, List<GameObject> elements, ScrollRect scroll)
        {
            List<Skills.Skill> skills = player.GetSkills().GetSkillList();
            // Alt + ЛКМ работает и при нестандартной геометрии окна или ошибке создания галочек.
            for (int i = 0; i < skills.Count && i < elements.Count; i++)
            {
                if (skills[i]?.m_info == null) continue;
                SkillTrackTarget target = elements[i].GetComponent<SkillTrackTarget>() ??
                    elements[i].AddComponent<SkillTrackTarget>();
                target.Type = skills[i].m_info.m_skill;
            }
            _scroll = scroll;
            _dialog = transform as RectTransform;
            // ScrollRect допускает пустой viewport: тогда границы задаёт его собственный RectTransform.
            // Так же выбирает viewRect сама Unity; отсутствие отдельного viewport не является ошибкой.
            _viewport = scroll != null ? scroll.viewport ?? scroll.transform as RectTransform : null;
            if (_dialog == null || _viewport == null)
                throw new InvalidOperationException("Skills window or scroll bounds not found; Alt-click targets are bound");
            if (_layer == null)
            {
                // Этот слой лежит рядом с viewport, а не внутри него: левый отступ окна
                // иначе обрезал бы галочки. По вертикали обрезаем ровно по краям viewport.
                _layer = UiKit.Rect("SkillTrackingCheckboxes", transform);
                _layer.gameObject.AddComponent<RectMask2D>();
            }
            for (int i = 0; i < skills.Count && i < elements.Count; i++)
            {
                if (skills[i]?.m_info == null) continue;
                Row row = _rows.FirstOrDefault(r => r.Element == elements[i]);
                if (row == null)
                {
                    var icon = Utils.FindChild(elements[i].transform, "icon") as RectTransform;
                    if (icon == null) continue;
                    row = new Row { Element = elements[i], Icon = icon };
                    CreateToggle(row);
                    _rows.Add(row);
                }
                row.Type = skills[i].m_info.m_skill;
            }
            Refresh();
            PositionCheckboxes();
        }

        private void CreateToggle(Row row)
        {
            GameObject go = GUIManager.Instance.CreateToggle(_layer, 36f, 36f);
            go.name = "TrackSkill";
            go.transform.localScale = Vector3.one;
            Text label = go.GetComponentInChildren<Text>();
            if (label != null) Destroy(label.gameObject);
            row.Toggle = go.GetComponent<Toggle>();
            row.Toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            UiKit.Stretch((RectTransform)row.Toggle.targetGraphic.transform);
            UiKit.Stretch((RectTransform)row.Toggle.graphic.transform);
            row.Tooltip = go.AddComponent<UITooltip>();
            row.Toggle.onValueChanged.AddListener(value => TrackedSkills.Set(row.Type, value));
        }

        private void Refresh()
        {
            foreach (Row row in _rows)
            {
                if (row.Toggle == null) continue;
                bool tracked = TrackedSkills.Contains(row.Type);
                row.Toggle.SetIsOnWithoutNotify(tracked);
                row.Tooltip.Set(Loc.S("Отслеживание навыка", "Skill tracking"),
                    tracked
                        ? Loc.S("Навык отслеживается. Нажми галочку или Alt + ЛКМ по навыку, чтобы снять отслеживание.",
                            "This skill is tracked. Click the checkbox or Alt + left-click the skill to stop tracking.")
                        : Loc.S("Нажми галочку или Alt + ЛКМ по навыку, чтобы отслеживать уровень и опыт на экране.",
                            "Click the checkbox or Alt + left-click the skill to track its level and experience on screen."));
            }
        }

        // Галочки находятся рядом со ScrollRect: пересылаем ему колесо мыши из левого отступа.
        public void OnScroll(PointerEventData eventData)
        {
            if (_scroll != null) _scroll.OnScroll(eventData);
        }

        private readonly Vector3[] _corners = new Vector3[4];

        private void LateUpdate()
        {
            try { PositionCheckboxes(); }
            catch (Exception e)
            {
                enabled = false;
                if (_layer != null) _layer.gameObject.SetActive(false);
                Plugin.Log.LogWarning("Skill tracking controls disabled: " + e);
            }
        }

        private void PositionCheckboxes()
        {
            if (_layer == null || _viewport == null || _dialog == null) return;
            _viewport.GetWorldCorners(_corners);
            float bottom = _dialog.InverseTransformPoint(_corners[0]).y;
            float top = _dialog.InverseTransformPoint(_corners[1]).y;
            _layer.anchorMin = _layer.anchorMax = _dialog.pivot;
            _layer.pivot = new Vector2(0.5f, 0.5f);
            _layer.anchoredPosition = new Vector2(_dialog.rect.center.x, (bottom + top) / 2f);
            _layer.sizeDelta = new Vector2(_dialog.rect.width, Mathf.Max(0f, top - bottom));
            foreach (Row row in _rows)
            {
                if (row.Toggle == null) continue;
                bool visible = row.Element != null && row.Element.activeInHierarchy && row.Icon != null;
                row.Toggle.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 left = row.Icon.TransformPoint(new Vector3(row.Icon.rect.xMin, row.Icon.rect.center.y, 0f));
                Vector3 local = _layer.InverseTransformPoint(left);
                RectTransform rt = (RectTransform)row.Toggle.transform;
                float box = Mathf.Clamp(row.Icon.rect.height * 0.6f, 24f, 36f);
                rt.anchorMin = rt.anchorMax = _layer.pivot;
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(local.x - 5f, local.y);
                rt.sizeDelta = new Vector2(box, box);
            }
        }

        private void OnDestroy()
        {
            if (_layer != null) Destroy(_layer.gameObject);
        }
    }

    [HarmonyPatch]
    internal static class SkillTrackingUi
    {
        private static bool s_errorLogged;

        private static void Report(Exception e)
        {
            if (s_errorLogged) return;
            s_errorLogged = true;
            Plugin.Log.LogWarning("Could not update skill tracking controls: " + e);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
        private static void Setup(SkillsDialog __instance, Player player, List<GameObject> ___m_elements,
            ScrollRect ___skillListScrollRect)
        {
            try
            {
                if (player == null || player != Player.m_localPlayer || GUIManager.IsHeadless()) return;
                SkillTrackingPanel panel = __instance.GetComponent<SkillTrackingPanel>() ??
                    __instance.gameObject.AddComponent<SkillTrackingPanel>();
                ScrollRect scroll = ___skillListScrollRect != null ? ___skillListScrollRect :
                    __instance.m_listRoot.GetComponentInParent<ScrollRect>();
                panel.Setup(player, ___m_elements, scroll);
            }
            catch (Exception e) { Report(e); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.SkillClicked))]
        private static void SkillClicked(GameObject selectedObject)
        {
            try
            {
                if (!ZInput.GetKey(KeyCode.LeftAlt, false) && !ZInput.GetKey(KeyCode.RightAlt, false)) return;
                SkillTrackTarget target = selectedObject != null ? selectedObject.GetComponent<SkillTrackTarget>() : null;
                if (target != null) TrackedSkills.Toggle(target.Type);
            }
            catch (Exception e) { Report(e); }
        }

        public static void Dispose()
        {
            foreach (SkillTrackingPanel panel in UnityEngine.Object.FindObjectsByType<SkillTrackingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(panel);
            foreach (SkillTrackTarget target in UnityEngine.Object.FindObjectsByType<SkillTrackTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(target);
        }
    }
}
