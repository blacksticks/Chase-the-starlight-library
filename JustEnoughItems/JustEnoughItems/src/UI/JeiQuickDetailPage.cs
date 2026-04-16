using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JustEnoughItems.Config;

namespace JustEnoughItems.UI
{
    public class JeiQuickDetailPage : MonoBehaviour
    {
        public RectTransform Panel;   // 根面板
        public string ItemId;

        public void Build(string itemId)
        {
            ItemId = itemId;
            // 根面板
            Panel = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            Panel.anchorMin = new Vector2(0, 0);
            Panel.anchorMax = new Vector2(1, 1);
            Panel.offsetMin = Vector2.zero;
            Panel.offsetMax = Vector2.zero;

            // 背景
            var bg = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.raycastTarget = true;

            // 容器
            var box = NewRt("Box", Panel);
            box.anchorMin = new Vector2(0.1f, 0.1f);
            box.anchorMax = new Vector2(0.9f, 0.9f);
            box.offsetMin = box.offsetMax = Vector2.zero;
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            // 关闭按钮
            var closeRt = NewRt("Close", box);
            closeRt.anchorMin = new Vector2(1, 1);
            closeRt.anchorMax = new Vector2(1, 1);
            closeRt.pivot = new Vector2(1, 1);
            closeRt.sizeDelta = new Vector2(28, 28);
            closeRt.anchoredPosition = new Vector2(-8, -8);
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => { try { Destroy(this.gameObject); } catch { } });

            // 标题区：图标 + 名称
            var header = NewRt("Header", box);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0, 1);
            header.sizeDelta = new Vector2(0, 92);
            header.anchoredPosition = new Vector2(0, -8);

            var iconRt = NewRt("Icon", header);
            iconRt.anchorMin = new Vector2(0, 0);
            iconRt.anchorMax = new Vector2(0, 1);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.sizeDelta = new Vector2(92, 92);
            var iconImg = iconRt.gameObject.AddComponent<Image>();

            var nameRt = NewRt("Name", header);
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(100, 0);
            nameRt.offsetMax = new Vector2(-8, 0);
            var nameText = nameRt.gameObject.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontSize = 24;
            nameText.color = Color.white;

            // 两列内容：获得方式 / 用途
            var body = NewRt("Body", box);
            body.anchorMin = new Vector2(0, 0);
            body.anchorMax = new Vector2(1, 1);
            body.offsetMin = new Vector2(8, 8);
            body.offsetMax = new Vector2(-8, -108);

            var left = NewRt("LeftGroup", body);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0.5f, 1);
            left.offsetMin = left.offsetMax = Vector2.zero;

            var right = NewRt("RightGroup", body);
            right.anchorMin = new Vector2(0.5f, 0);
            right.anchorMax = new Vector2(1, 1);
            right.offsetMin = right.offsetMax = Vector2.zero;

            // 小节标题
            AddSectionTitle(left, "获得方式");
            AddSectionTitle(right, "用途");

            // 数据
            try { JeiDataStore.TryGetItem(itemId, out var data); FillHeader(iconImg, nameText, itemId); FillList(left, data?.Source); FillList(right, data?.Usage); } catch { }
        }

        private RectTransform NewRt(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private void AddSectionTitle(RectTransform parent, string text)
        {
            var rt = NewRt("SectionTitle", parent);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(0, 28);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleLeft;
            t.fontSize = 16;
            t.color = Color.white;
            t.text = text;
        }

        private void FillHeader(Image iconImg, Text nameText, string id)
        {
            var sprite = GetItemSprite(id);
            if (sprite != null)
            {
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
            }
            nameText.text = GetDisplayName(id);
        }

        private void FillList(RectTransform parent, List<JeiRecipe> list)
        {
            float y = 28f; // 跳过标题
            if (list == null || list.Count == 0)
            {
                var empty = NewRt("Empty", parent);
                empty.anchorMin = new Vector2(0, 1);
                empty.anchorMax = new Vector2(1, 1);
                empty.pivot = new Vector2(0, 1);
                empty.anchoredPosition = new Vector2(0, -y);
                empty.sizeDelta = new Vector2(0, 22);
                var t = empty.gameObject.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.MiddleLeft;
                t.color = new Color(1,1,1,0.85f);
                t.fontSize = 14;
                t.text = "无";
                return;
            }
            foreach (var r in list)
            {
                var row = NewRt("Row", parent);
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0, 1);
                row.anchoredPosition = new Vector2(0, -y);
                row.sizeDelta = new Vector2(0, 22);
                var t = row.gameObject.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.MiddleLeft;
                t.color = new Color(1,1,1,0.9f);
                t.fontSize = 13;
                t.text = RecipeToLine(r);
                y += 24f;
            }
        }

        private string RecipeToLine(JeiRecipe r)
        {
            try
            {
                if (r == null) return "";
                if (r.IfFabricator)
                {
                    var fab = string.IsNullOrEmpty(r.Fabricator) ? "工作台" : GetDisplayName(r.Fabricator);
                    var ings = r.Ingredient != null ? string.Join(" + ", r.Ingredient.Select(GetDisplayName)) : "";
                    return $"{fab}: {ings}";
                }
                // 非工作台配方：显示文本或目标
                if (!string.IsNullOrEmpty(r.Text)) return r.Text;
                if (!string.IsNullOrEmpty(r.Target)) return GetDisplayName(r.Target);
                return "";
            }
            catch { return ""; }
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
                string norm = id;
                if (norm.Contains("/") || norm.Contains("\\"))
                {
                    try { norm = System.IO.Path.GetFileNameWithoutExtension(norm); } catch { }
                }
                if (norm.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)) norm = norm.Substring("TechType.".Length);
                var dict = ConfigService.ChineseNames;
                if (dict != null && dict.TryGetValue(norm, out var name) && !string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return id;
        }
    }
}
