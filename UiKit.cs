using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AchievementTracker
{
    /// <summary>
    /// Уголок для растягивания окна мышью. Окно Jötunn с центральной опорной точкой, поэтому при изменении размера
    /// сдвигаем позицию так, чтобы левый верхний угол оставался на месте.
    /// </summary>
    internal sealed class ResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Target;
        public Vector2 Min;
        public System.Action<Vector2> OnResized;

        public void OnBeginDrag(PointerEventData e)
        {
        }

        public void OnDrag(PointerEventData e)
        {
            if (Target == null) return;
            Canvas canvas = Target.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;
            Vector2 d = e.delta / scale;
            Vector2 size = Target.sizeDelta;
            var max = new Vector2(Screen.width / scale, Screen.height / scale);
            var wanted = new Vector2(Mathf.Clamp(size.x + d.x, Min.x, max.x), Mathf.Clamp(size.y - d.y, Min.y, max.y));
            Vector2 grow = wanted - size;
            Target.sizeDelta = wanted;
            Target.anchoredPosition += new Vector2(grow.x * Target.pivot.x, -grow.y * (1f - Target.pivot.y));
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (Target != null) OnResized?.Invoke(Target.sizeDelta);
        }
    }

    internal static class UiKit
    {
        public static readonly Color Orange = new Color(1f, 0.631f, 0.235f);
        public static readonly Color Beige = new Color(0.853f, 0.725f, 0.533f);
        public static readonly Color Green = new Color(0.45f, 0.9f, 0.4f);
        public static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);
        public static readonly Color Red = new Color(0.95f, 0.45f, 0.4f);
        public static readonly Color Grey = new Color(0.62f, 0.62f, 0.62f);

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color StatusColor(AchStatus s)
        {
            switch (s)
            {
                case AchStatus.Done: return Green;
                case AchStatus.Available: return Yellow;
                case AchStatus.Partial: return Orange;
                default: return Grey;
            }
        }

        public static string StatusText(AchStatus s)
        {
            switch (s)
            {
                case AchStatus.Done: return Loc.S("Выполнено", "Completed");
                case AchStatus.Available: return Loc.S("Можно выполнить", "Doable now");
                case AchStatus.Partial: return Loc.S("Частично доступно", "Partly doable");
                default: return Loc.S("Пока недоступно", "Not yet available");
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Позиция от левого верхнего угла родителя.</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Привязка к части родителя: anchorMin/anchorMax в долях, отступы в пикселях.</summary>
        public static RectTransform Region(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>Фиксированный размер, привязка к углу/краю: anchor и pivot совпадают, pos — отступ от него.</summary>
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Sprite s_grip;

        /// <summary>Штриховка уголка для растягивания окна.</summary>
        public static Sprite GripSprite()
        {
            if (s_grip != null) return s_grip;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Правый нижний треугольник с диагональными полосками
                    bool inside = x > y + 2;
                    bool stripe = (x - y) % 8 < 3;
                    px[y * size + x] = inside && stripe ? new Color32(230, 190, 120, 220) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s_grip = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_grip;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Bg(GameObject go, Color c)
        {
            Image img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public static Text Label(Transform parent, string s, int size, Color c, TextAnchor anchor, bool title = false)
        {
            RectTransform rt = Rect("Text", parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = title ? GUIManager.Instance.NorseBold : GUIManager.Instance.AveriaSerifBold;
            t.fontSize = size;
            t.color = c;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        public static Button Button(Transform parent, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, w, h);
            TopLeft((RectTransform)go.transform, x, y, w, h);
            Button b = go.GetComponent<Button>();
            b.onClick.AddListener(onClick);
            return b;
        }

        public static void SetButtonText(Button b, string text, Color? color = null)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t == null) return;
            t.text = text;
            if (color.HasValue) t.color = color.Value;
        }

        /// <summary>Вертикальный список с прокруткой колесом и полосой справа.</summary>
        public static ScrollRect ScrollList(RectTransform root, out RectTransform content)
        {
            Bg(root.gameObject, new Color(0f, 0f, 0f, 0.38f));
            ScrollRect sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 0f; // колесо обрабатывает AchievementPanel.HandleScroll
            sr.inertia = false;

            RectTransform viewport = Stretch(Rect("Viewport", root), 4, 4, 18, 4);
            viewport.gameObject.AddComponent<RectMask2D>();
            Bg(viewport.gameObject, new Color(0f, 0f, 0f, 0.001f));

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(2, 2, 2, 2);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport;
            sr.content = content;

            GameObject sbGo = DefaultControls.CreateScrollbar(GUIManager.Instance.ValheimControlResources);
            sbGo.transform.SetParent(root, false);
            RectTransform sbrt = (RectTransform)sbGo.transform;
            sbrt.anchorMin = new Vector2(1f, 0f);
            sbrt.anchorMax = new Vector2(1f, 1f);
            sbrt.pivot = new Vector2(1f, 0.5f);
            sbrt.sizeDelta = new Vector2(12f, -8f);
            sbrt.anchoredPosition = new Vector2(-3f, 0f);
            Scrollbar sb = sbGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            GUIManager.Instance.ApplyScrollbarStyle(sb);
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return sr;
        }

        /// <summary>Полоска прогресса: фон + заливка, заливку меняем через anchorMax.x.</summary>
        public static RectTransform ProgressBar(Transform parent, out RectTransform fill)
        {
            RectTransform bar = Rect("Bar", parent);
            Bg(bar.gameObject, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = false;
            fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            Bg(fill.gameObject, Orange).raycastTarget = false;
            return bar;
        }

        public static void SetFill(RectTransform fill, float frac, Color color)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            Image img = fill.GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }
}
