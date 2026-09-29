using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AchievementTracker
{
    /// <summary>Текст подсказки, привязанный к звёздочке: для каких достижений нужен этот предмет.</summary>
    internal sealed class StarInfo : MonoBehaviour
    {
        public string Text = "";
    }

    /// <summary>
    /// Звёздочки на иконках: в меню строительства — непостроенное, на станках — нескрафченное,
    /// в инвентаре и сундуках — трофеи, еда, рыба, сырьё и семена, которые ещё нужны для достижений.
    /// </summary>
    [HarmonyPatch]
    internal static class CraftMarks
    {
        private const string StarName = "AchievementTrackerStar";

        // ключ (имя постройки/предмета) → строки «Достижение: что сделать»
        private static readonly Dictionary<string, List<string>> PendingPieces = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, List<string>> PendingItems = new Dictionary<string, List<string>>();
        // Предметы в инвентаре и сундуках: трофеи (засчитываются, когда попадают в инвентарь игрока),
        // ещё не съеденная еда, ещё не пойманная рыба, сырьё для блюд, семена для посадок
        private static readonly Dictionary<string, List<string>> PendingInventory = new Dictionary<string, List<string>>();

        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> ElementsRef =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");
        private static float s_nextRebuild;
        private static Sprite s_star;

        private static FieldInfo s_pieceIconsField;
        private static FieldInfo s_iconField;
        private static bool s_broken;

        public static void MarkDirty() => s_nextRebuild = 0f;

        private static void Add(Dictionary<string, List<string>> map, string key, string line)
        {
            if (!map.TryGetValue(key, out List<string> list)) map[key] = list = new List<string>();
            if (!list.Contains(line)) list.Add(line);
        }

        private static string Action(ReqKind kind)
        {
            switch (kind)
            {
                case ReqKind.PiecePlaced: return Loc.S("построить", "build");
                case ReqKind.ItemCraft: return Loc.S("создать", "craft");
                case ReqKind.ItemPickup: return Loc.S("взять в свой инвентарь", "take into your inventory");
                case ReqKind.FoodEaten: return Loc.S("съесть", "eat");
                default: return Loc.S("поймать или собрать самому", "catch or gather yourself");
            }
        }

        private static void EnsurePending()
        {
            if (Time.time < s_nextRebuild) return;
            s_nextRebuild = Time.time + 1f;
            PendingPieces.Clear();
            PendingItems.Clear();
            PendingInventory.Clear();
            if (!Analyzer.Ready) return;
            foreach (Achievement a in Analyzer.All())
            {
                if (a.m_unlocked) continue;
                PlayerProfile.PlayerStats stats = Analyzer.StatsFor(a);
                string ach = Names.Achievement(a);
                foreach (Requirement r in Analyzer.GetRequirements(a))
                {
                    Dictionary<string, List<string>> target;
                    switch (r.Kind)
                    {
                        case ReqKind.PiecePlaced: target = PendingPieces; break;
                        case ReqKind.ItemCraft: target = PendingItems; break;
                        case ReqKind.ItemPickup:
                        case ReqKind.FoodEaten:
                        case ReqKind.Pickable: target = PendingInventory; break;
                        default: continue;
                    }
                    bool has = Analyzer.TryGetValue(r, stats, out float v);
                    if (Analyzer.IsMet(r, has, v)) continue;
                    string key = r.Kind == ReqKind.Pickable ? Names.PickableItem(r.Key) : r.Key;
                    Add(target, key, $"{ach}: {Action(r.Kind)}");
                }
            }

            EnsureSources();
            // Сырьё для вертела/решётки: готовое блюдо ещё не делали → звезда на сыром продукте
            foreach (KeyValuePair<string, List<string>> kv in CookedFrom)
            {
                if (!PendingItems.TryGetValue(kv.Key, out List<string> why)) continue;
                string dish = Names.L(kv.Key);
                foreach (string raw in kv.Value)
                {
                    foreach (string line in why)
                    {
                        string ach = line.Substring(0, line.LastIndexOf(':'));
                        Add(PendingInventory, raw, $"{ach}: " + Loc.S($"приготовить «{dish}»", $"cook \"{dish}\""));
                    }
                }
            }
            // Семена и саженцы: посадку ещё не делали → звезда на семенах, которые для неё нужны
            foreach (KeyValuePair<string, List<string>> kv in PlantSeeds)
            {
                if (!PendingPieces.TryGetValue(kv.Key, out List<string> why)) continue;
                string plant = Names.L(kv.Key);
                foreach (string seed in kv.Value)
                {
                    foreach (string line in why)
                    {
                        string ach = line.Substring(0, line.LastIndexOf(':'));
                        Add(PendingInventory, seed, $"{ach}: " + Loc.S($"посадить «{plant}»", $"plant \"{plant}\""));
                    }
                }
            }
        }

        private static string Tooltip(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return "";
            return $"<color=#{UiKit.Hex(UiKit.Orange)}><b>{Loc.S("Нужно для достижений:", "Needed for achievements:")}</b></color>\n" +
                   string.Join("\n", lines.Select(l => "• " + l));
        }

        // готовое блюдо → сырьё, из которого его делают на вертеле/решётке/в печи (CookingStation)
        private static readonly Dictionary<string, List<string>> CookedFrom = new Dictionary<string, List<string>>();
        // посадка (Piece с Plant) → семена/саженцы, которые она тратит
        private static readonly Dictionary<string, List<string>> PlantSeeds = new Dictionary<string, List<string>>();
        private static ZNetScene s_sourcesFor;

        private static void AddTo(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out List<string> list)) map[key] = list = new List<string>();
            if (!list.Contains(value)) list.Add(value);
        }

        private static void EnsureSources()
        {
            if (s_sourcesFor != null && s_sourcesFor == ZNetScene.instance) return;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return;
            s_sourcesFor = ZNetScene.instance;
            CookedFrom.Clear();
            PlantSeeds.Clear();

            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                CookingStation cs = prefab != null ? prefab.GetComponent<CookingStation>() : null;
                if (cs == null) continue;
                foreach (CookingStation.ItemConversion conv in cs.m_conversion)
                {
                    if (conv?.m_from == null || conv.m_to == null) continue;
                    AddTo(CookedFrom, conv.m_to.m_itemData.m_shared.m_name, conv.m_from.m_itemData.m_shared.m_name);
                }
            }

            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                PieceTable table = go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                if (table == null) continue;
                foreach (GameObject pgo in table.m_pieces)
                {
                    Piece p = pgo != null ? pgo.GetComponent<Piece>() : null;
                    if (p == null || pgo.GetComponent<Plant>() == null) continue;
                    foreach (Piece.Requirement res in p.m_resources)
                    {
                        if (res?.m_resItem != null) AddTo(PlantSeeds, p.m_name, res.m_resItem.m_itemData.m_shared.m_name);
                    }
                }
            }
        }

        /// <summary>Показать/спрятать звезду на иконке; text — подсказка по Alt.</summary>
        private static void SetStar(Transform icon, List<string> why, bool topLeft = false)
        {
            Transform star = icon.Find(StarName);
            if (why == null)
            {
                if (star != null) star.gameObject.SetActive(false);
                return;
            }
            if (star == null)
            {
                RectTransform rt = UiKit.Rect(StarName, icon);
                // В меню строительства правый верх занят звёздочкой «избранного» самой игры — там ставим слева
                rt.anchorMin = rt.anchorMax = topLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rt.pivot = topLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rt.anchoredPosition = topLeft ? new Vector2(-4f, 4f) : new Vector2(4f, 4f);
                rt.sizeDelta = new Vector2(16f, 16f);
                Image img = rt.gameObject.AddComponent<Image>();
                img.sprite = StarSprite();
                img.color = new Color(1f, 0.85f, 0.2f);
                img.raycastTarget = false;
                Outline o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                o.effectDistance = new Vector2(1f, -1f);
                rt.gameObject.AddComponent<StarInfo>();
                star = rt;
            }
            star.GetComponent<StarInfo>().Text = Tooltip(why);
            star.gameObject.SetActive(true);
            star.SetAsLastSibling();
        }

        /// <summary>Пятиконечная звезда, нарисованная в текстуру: символа ★ может не быть в шрифтах игры.</summary>
        private static Sprite StarSprite()
        {
            if (s_star != null) return s_star;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                pts[i] = new Vector2(size / 2f + Mathf.Cos(ang) * rad, size / 2f + Mathf.Sin(ang) * rad - size * 0.03f);
            }
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 4×4 подвыборки на пиксель — сглаженные края
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            if (Inside(pts, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s_star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_star;
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static List<string> Why(Dictionary<string, List<string>> map, string key) =>
            Plugin.MarkUncrafted.Value && key != null && map.TryGetValue(key, out List<string> lines) ? lines : null;

        /// <summary>Старая сетка меню молота (на случай, если её вернут или включит другой мод).</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "UpdatePieceList")]
        private static void HudUpdatePieceList(Hud __instance, Player player)
        {
            if (s_broken) return;
            try
            {
                if (Plugin.MarkUncrafted.Value) EnsurePending();
                s_pieceIconsField = s_pieceIconsField ?? AccessTools.Field(typeof(Hud), "m_pieceIcons");
                if (!(s_pieceIconsField?.GetValue(__instance) is IList icons)) return;
                List<Piece> pieces = player.GetBuildPieces();
                for (int i = 0; i < icons.Count; i++)
                {
                    object data = icons[i];
                    s_iconField = s_iconField ?? AccessTools.Field(data.GetType(), "m_icon");
                    if (!(s_iconField?.GetValue(data) is Image icon) || icon == null) continue;
                    SetStar(icon.transform, i < pieces.Count ? Why(PendingPieces, pieces[i]?.m_name) : null);
                }
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Build menu stars disabled until restart: " + e);
            }
        }

        private static readonly AccessTools.FieldRef<BuildUiPieceButton, Image> BuildButtonIconRef =
            AccessTools.FieldRefAccess<BuildUiPieceButton, Image>("m_icon");

        private static void MarkBuildButton(BuildUiPieceButton button)
        {
            if (s_broken || button == null) return;
            try
            {
                Image icon = BuildButtonIconRef(button);
                if (icon == null) return;
                if (Plugin.MarkUncrafted.Value) EnsurePending();
                SetStar(icon.transform, Why(PendingPieces, button.Piece?.m_name), topLeft: true);
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Build menu stars disabled until restart: " + e);
            }
        }

        /// <summary>Новое меню строительства (BuildUi): кнопка получает постройку.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUiPieceButton), nameof(BuildUiPieceButton.Setup))]
        private static void BuildButtonSetup(BuildUiPieceButton __instance) => MarkBuildButton(__instance);

        private static readonly AccessTools.FieldRef<BuildUi, List<BuildUiPieceButton>> BuildButtonsRef =
            AccessTools.FieldRefAccess<BuildUi, List<BuildUiPieceButton>>("m_pieceButtons");
        private static float s_nextBuildRefresh;

        /// <summary>Пока меню открыто, раз в полсекунды обновляем звёзды — чтобы после постройки звезда пропадала сразу.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "Update")]
        private static void BuildUiUpdate(BuildUi __instance)
        {
            if (s_broken || Time.time < s_nextBuildRefresh) return;
            s_nextBuildRefresh = Time.time + 0.5f;
            List<BuildUiPieceButton> buttons = BuildButtonsRef(__instance);
            if (buttons == null) return;
            foreach (BuildUiPieceButton b in buttons) MarkBuildButton(b);
        }

        private static readonly Dictionary<int, List<string>> MarkedCells = new Dictionary<int, List<string>>();

        /// <summary>Инвентарь игрока и сундуков: игра обновляет сетку каждый кадр, пока окно открыто.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static void InventoryGridUpdateGui(InventoryGrid __instance)
        {
            if (s_broken) return;
            try
            {
                List<InventoryElement> elements = ElementsRef(__instance);
                Inventory inv = __instance.GetInventory();
                if (elements == null || inv == null) return;
                if (Plugin.MarkUncrafted.Value) EnsurePending();

                int width = inv.GetWidth();
                MarkedCells.Clear();
                foreach (ItemDrop.ItemData item in inv.GetAllItems())
                {
                    List<string> why = Why(PendingInventory, item.m_shared.m_name);
                    if (why == null) continue;
                    int idx = item.m_gridPos.y * width + item.m_gridPos.x;
                    if (idx >= 0 && idx < elements.Count) MarkedCells[idx] = why;
                }
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i] == null || elements[i].m_icon == null) continue;
                    SetStar(elements[i].m_icon.transform, MarkedCells.TryGetValue(i, out List<string> w) ? w : null);
                }
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Inventory stars disabled until restart: " + e);
            }
        }

        private static float s_nextRecipeRefresh;

        /// <summary>
        /// После крафта игра не перестраивает список рецептов, пока не переоткроешь станок, — поэтому, пока окно открыто,
        /// раз в полсекунды пересчитываем звёзды у уже показанных строк.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        private static void InventoryGuiUpdate(InventoryGui __instance)
        {
            if (s_broken || Time.time < s_nextRecipeRefresh || !InventoryGui.IsVisible()) return;
            s_nextRecipeRefresh = Time.time + 0.5f;
            try
            {
                if (Plugin.MarkUncrafted.Value) EnsurePending();
                var list = Traverse.Create(__instance).Field("m_availableRecipes").GetValue() as IList;
                if (list == null) return;
                foreach (object pair in list)
                {
                    Traverse t = Traverse.Create(pair);
                    var element = t.Property("InterfaceElement").GetValue() as GameObject;
                    Transform icon = element != null ? element.transform.Find("icon") : null;
                    if (icon == null) continue;
                    var recipe = t.Property("Recipe").GetValue() as Recipe;
                    var item = t.Property("ItemData").GetValue() as ItemDrop.ItemData;
                    string name = item == null ? recipe?.m_item?.m_itemData?.m_shared?.m_name : null;
                    SetStar(icon, Why(PendingItems, name));
                }
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Crafting stars disabled until restart: " + e);
            }
        }

        /// <summary>Список рецептов станка: звезда у нового предмета, если его ещё не создавали.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "AddRecipeToList")]
        private static void AddRecipeToList(InventoryGui __instance, Recipe recipe, ItemDrop.ItemData item)
        {
            if (s_broken) return;
            try
            {
                if (Plugin.MarkUncrafted.Value) EnsurePending();
                var list = Traverse.Create(__instance).Field("m_availableRecipes").GetValue() as IList;
                if (list == null || list.Count == 0) return;
                var element = Traverse.Create(list[list.Count - 1]).Property("InterfaceElement").GetValue() as GameObject;
                Transform icon = element != null ? element.transform.Find("icon") : null;
                if (icon == null) return;
                // item != null — это улучшение уже имеющегося предмета, для достижений оно не считается
                string name = item == null ? recipe?.m_item?.m_itemData?.m_shared?.m_name : null;
                SetStar(icon, Why(PendingItems, name));
            }
            catch (Exception e)
            {
                s_broken = true;
                Plugin.Log.LogError("Crafting stars disabled until restart: " + e);
            }
        }
    }
}
