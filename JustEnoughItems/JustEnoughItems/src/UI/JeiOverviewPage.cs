using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JustEnoughItems.Config;

namespace JustEnoughItems.UI
{
    public class JeiOverviewPage : MonoBehaviour
    {
        public RectTransform Holder;   // ScrollView RectTransform
        public RectTransform Content;  // ScrollView/Content

        private float _accumY;

        public void Build()
        {
            if (Content == null)
            {
                try
                {
                    Holder = transform as RectTransform;
                    Content = Holder?.Find("Content") as RectTransform;
                }
                catch { }
            }
            if (Content == null) return;

            // 清空
            for (int i = Content.childCount - 1; i >= 0; i--) Destroy(Content.GetChild(i).gameObject);
            _accumY = 0f;
            try { Content.anchorMin = new Vector2(0, 1); Content.anchorMax = new Vector2(1, 1); Content.pivot = new Vector2(0, 1); } catch { }
            try { Content.sizeDelta = new Vector2(Content.sizeDelta.x, 0f); } catch { }

            // 数据就绪
            try { JeiDataStore.BuildIfNeeded(); } catch { }
            try { ConfigService.EnsureNamesLoaded(); } catch { }

            // 读取工作台分组
            var groups = FabricatorOverridesService.Current ?? new List<FabricatorOverride>();
            foreach (var g in groups)
            {
                string title = ResolveFabricatorTitle(g);
                var itemIds = (g?.IncludeItems ?? new List<string>())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(NormalizeId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                // 组块：标题
                var titleRt = CreateTitle(title);
                PositionNext(titleRt, 32f);

                // 组块：网格
                var gridRt = CreateGrid();
                int created = 0;
                foreach (var id in itemIds)
                {
                    var cell = CreateItemCell(gridRt, id);
                    if (cell != null) created++;
                }
                // 估算网格高度
                var gl = gridRt.GetComponent<GridLayoutGroup>();
                int columns = Mathf.Max(1, gl.constraintCount);
                int rows = (created + columns - 1) / columns;
                float cellH = gl.cellSize.y;
                float vSpace = gl.spacing.y;
                float padding = gl.padding.top + gl.padding.bottom;
                float h = padding + rows * cellH + Math.Max(0, rows - 1) * vSpace;
                PositionNext(gridRt, h);
            }

            try { Content.sizeDelta = new Vector2(Content.sizeDelta.x, _accumY); } catch { }
        }

        private RectTransform CreateTitle(string text)
        {
            var go = new GameObject("GroupTitle", typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(Content, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(1f, 1f, 1f, 0.95f);
            t.fontSize = 20;
            t.text = text ?? "";
            return rt;
        }

        private RectTransform CreateGrid()
        {
            var go = new GameObject("GroupGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(Content, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            var gl = go.GetComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(64, 64);
            gl.spacing = new Vector2(8, 8);
            gl.padding = new RectOffset(8, 8, 8, 8);
            gl.startAxis = GridLayoutGroup.Axis.Horizontal;
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 8;
            return rt;
        }

        private GameObject CreateItemCell(RectTransform parent, string id)
        {
            var sprite = GetItemSprite(id);
            if (sprite == null) return null;

            var go = new GameObject($"Cell_{id}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(64, 64);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.SetParent(rt, false);
            iconRt.anchorMin = new Vector2(0, 0);
            iconRt.anchorMax = new Vector2(1, 1);
            iconRt.offsetMin = new Vector2(4, 16);
            iconRt.offsetMax = new Vector2(-4, -4);
            var img = iconGo.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = true;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.SetParent(rt, false);
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0);
            nameRt.sizeDelta = new Vector2(0, 14);
            nameRt.anchoredPosition = new Vector2(0, 0);
            var txt = nameGo.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 12;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            txt.text = GetDisplayName(id);

            // 点击进入详情
            BindClick(go, id);
            return go;
        }

        private void BindClick(GameObject cell, string id)
        {
            var trig = cell.GetComponent<EventTrigger>() ?? cell.AddComponent<EventTrigger>();
            trig.triggers ??= new List<EventTrigger.Entry>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            e.callback.AddListener(_ =>
            {
                try
                {
                    // 在 JEI_EmbedRoot 下创建一个覆盖层用于显示快速详情
                    var root = (Holder != null ? Holder.parent as RectTransform : null) ?? (transform.parent as RectTransform);
                    if (root == null) root = Holder; // 兜底
                    if (root != null)
                    {
                        var overlay = new GameObject("JEI_QuickDetailOverlay", typeof(RectTransform), typeof(CanvasGroup));
                        var rt = overlay.GetComponent<RectTransform>();
                        rt.SetParent(root, false);
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        overlay.transform.SetAsLastSibling();

                        var page = overlay.AddComponent<JeiQuickDetailPage>();
                        page.Build(id);
                    }
                }
                catch { }
            });
            trig.triggers.Add(e);
        }

        private void PositionNext(RectTransform rt, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -_accumY);
            rt.sizeDelta = new Vector2(0, h);
            _accumY += h;
        }

        private string NormalizeId(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Trim();
            if (t.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)) t = t.Substring("TechType.".Length);
            return t.Trim();
        }

        private Sprite GetItemSprite(string idOrTechType)
        {
            try
            {
                if (string.IsNullOrEmpty(idOrTechType)) return null;
                var token = idOrTechType.Trim();
                if (token.Contains("/") || token.Contains("\\") || token.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    if (!token.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)) return null;
                    return JustEnoughItems.IconCache.GetByIconsRelative(token);
                }
                return JustEnoughItems.IconCache.GetById(token);
            }
            catch { return null; }
        }

        private string GetDisplayName(string id)
        {
            try
            {
                var norm = NormalizeId(id);
                var dict = ConfigService.ChineseNames;
                if (dict != null && dict.TryGetValue(norm, out var name) && !string.IsNullOrEmpty(name))
                {
                    try { JustEnoughItems.Plugin.NameCache[norm] = name; } catch { }
                    return name;
                }
            }
            catch { }
            return id;
        }

        private string ResolveFabricatorTitle(FabricatorOverride fo)
        {
            try
            {
                if (fo != null && !string.IsNullOrEmpty(fo.DisplayName)) return fo.DisplayName;
                var id = fo?.Id ?? "Fabricator";
                var fromNames = GetDisplayName(id);
                return string.IsNullOrEmpty(fromNames) ? id : fromNames;
            }
            catch { return "Fabricator"; }
        }
    }
}
