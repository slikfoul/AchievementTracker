using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AchievementTracker
{
    /// <summary>
    /// Alt + наведение на иконку со звёздочкой — подсказка у курсора: для каких достижений нужен этот предмет.
    /// Ищем через UI-рейкаст всё под мышью и поднимаемся до ячейки/кнопки (небольшого прямоугольника) со звездой.
    /// </summary>
    internal static class StarTooltip
    {
        // Ячейка инвентаря, кнопка постройки, строка рецепта — все меньше этой площади; сетка целиком — больше
        private const float MaxCellArea = 30000f;

        private static RectTransform s_root;
        private static Text s_text;
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static void Tick()
        {
            string text = AltHeld() ? FindUnderMouse() : null;
            if (string.IsNullOrEmpty(text))
            {
                if (s_root != null) s_root.gameObject.SetActive(false);
                return;
            }
            if (!EnsureBuilt()) return;
            s_text.text = text;
            s_root.gameObject.SetActive(true);
            s_root.SetAsLastSibling();

            // Левее-выше курсора, чтобы не перекрывать всплывающую подсказку самой игры (она справа-снизу)
            Vector2 mouse = ZInput.pointerPosition;
            s_root.pivot = new Vector2(1f, 0f);
            s_root.position = mouse + new Vector2(-18f, 18f);
            ClampToScreen();
        }

        private static bool AltHeld() =>
            ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false);

        private static string FindUnderMouse()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return null;
            var ped = new PointerEventData(es) { position = ZInput.pointerPosition };
            Hits.Clear();
            es.RaycastAll(ped, Hits);
            foreach (RaycastResult hit in Hits)
            {
                Transform t = hit.gameObject != null ? hit.gameObject.transform : null;
                for (int depth = 0; t != null && depth < 5; depth++, t = t.parent)
                {
                    if (t is RectTransform rt && rt.rect.width * rt.rect.height > MaxCellArea) break;
                    StarInfo info = t.GetComponentInChildren<StarInfo>(false);
                    if (info != null && !string.IsNullOrEmpty(info.Text)) return info.Text;
                }
            }
            return null;
        }

        private static bool EnsureBuilt()
        {
            if (s_root != null) return true;
            if (GUIManager.CustomGUIFront == null) return false;
            s_root = UiKit.Rect("AchievementTrackerStarTooltip", GUIManager.CustomGUIFront.transform);
            s_root.sizeDelta = new Vector2(380f, 0f);
            UiKit.Bg(s_root.gameObject, new Color(0f, 0f, 0f, 0.85f)).raycastTarget = false;
            VerticalLayoutGroup v = s_root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 8, 8);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            s_root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            s_text = UiKit.Label(s_root, "", 15, UiKit.Beige, TextAnchor.UpperLeft);
            return true;
        }

        private static void ClampToScreen()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(s_root);
            var corners = new Vector3[4];
            s_root.GetWorldCorners(corners);
            Vector3 shift = Vector3.zero;
            if (corners[0].x < 0f) shift.x = -corners[0].x;
            if (corners[1].y > Screen.height) shift.y = Screen.height - corners[1].y;
            if (corners[0].y < 0f) shift.y = -corners[0].y;
            s_root.position += shift;
        }
    }
}
