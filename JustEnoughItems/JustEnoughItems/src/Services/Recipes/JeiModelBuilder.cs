using JustEnoughItems.Config;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JustEnoughItems
{
    public class JeiModelBuilder
    {
        public Dictionary<string, JeiItem> Build(RecipeMerger.MergedData data)
        {
            var map = new Dictionary<string, JeiItem>(StringComparer.OrdinalIgnoreCase);
            if (data == null) return map;

            // 1) 先放入 JSON 的 Items（Usage 全来自 JSON；Source 稍后由扫描填充）
            foreach (var it in (data.JsonConfig?.Items ?? new List<JeiItem>()))
            {
                if (it == null || string.IsNullOrEmpty(it.ItemId)) continue;
                var key = Normalize(it.ItemId);
                var copy = new JeiItem
                {
                    ItemId = key,
                    DisplayName = it.DisplayName,
                    Icon = it.Icon,
                    Patch = it.Patch,
                    Description = it.Description,
                    Source = new List<JeiSourceTab>(),
                    Usage = new List<JeiUsageTab>()
                };
                // Usage: 完全来自 JSON
                foreach (var u in it.Usage ?? Enumerable.Empty<JeiUsageTab>())
                {
                    if (u == null) continue;
                    copy.Usage.Add(CloneUsage(u));
                }
                map[key] = copy;
            }

            // 准备工作台覆盖（独立 JSON）：includeItems -> FabricatorOverride
            var overrides = FabricatorOverridesService.Current ?? new List<FabricatorOverride>();
            var itemToOverride = new Dictionary<string, FabricatorOverride>(StringComparer.OrdinalIgnoreCase);
            foreach (var ov in overrides)
            {
                if (ov?.IncludeItems == null) continue;
                foreach (var iid in ov.IncludeItems)
                {
                    var key = Normalize(iid);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!itemToOverride.ContainsKey(key)) itemToOverride[key] = ov;
                }
            }

            // 2) 使用扫描结果构建 Source：对每条配方，把“每个产物”挂到该产物对应物品的 Source
            foreach (var r in data.ScannedRecipes ?? Enumerable.Empty<RecipeEntry>())
            {
                var products = (r.Products ?? new List<string>()).Select(Normalize).Where(x => !string.IsNullOrEmpty(x)).ToList();
                var ingredients = (r.Ingredients ?? new List<RecipeIngredient>())
                    .Where(x => x != null && !string.IsNullOrEmpty(x.TechId))
                    .Select(x => new { Id = Normalize(x.TechId), x.Amount })
                    .Where(x => !string.IsNullOrEmpty(x.Id))
                    .ToList();
                if (products.Count == 0 || ingredients.Count == 0) continue;

                // 对该配方的每个“产物”建立来源页签（左：材料，右：当前产物由 UI 推断，无需 Target 字段）
                var ingredientList = ingredients.Select(x => RepeatId(x.Id, x.Amount)).SelectMany(x => x).ToList();
                foreach (var prod in products)
                {
                    if (!map.TryGetValue(prod, out var item))
                    {
                        item = new JeiItem { ItemId = prod, Source = new List<JeiSourceTab>(), Usage = new List<JeiUsageTab>() };
                        map[prod] = item;
                    }
                    // 应用工作台覆盖（若该产物被明确归属到某工作台）
                    string fabId = r.FabricatorId;
                    string fabDisplay = r.FabricatorDisplayName;
                    string fabIconPatch = null;
                    if (itemToOverride.TryGetValue(prod, out var ov))
                    {
                        if (!string.IsNullOrEmpty(ov.Id)) fabId = ov.Id;
                        if (!string.IsNullOrEmpty(ov.DisplayName)) fabDisplay = ov.DisplayName;
                        if (!string.IsNullOrEmpty(ov.Icon)) fabIconPatch = ov.Icon;
                    }

                    var tab = new JeiSourceTab
                    {
                        IfFabricator = true,
                        Fabricator = fabId,
                        FabricatorDisplayName = fabDisplay,
                        Ingredient = ingredientList,
                        Text = string.Empty,
                        Patch = string.IsNullOrEmpty(fabIconPatch) ? string.Empty : fabIconPatch,
                        Image = string.Empty,
                        TabIcon = string.Empty,
                    };
                    item.Source.Add(tab);
                }
            }

            // 3) 追加 JSON 的 Source（不覆盖、不替换，只追加到现有 Source 列表）
            foreach (var it in (data.JsonConfig?.Items ?? new List<JeiItem>()))
            {
                if (it == null || string.IsNullOrEmpty(it.ItemId)) continue;
                var key = Normalize(it.ItemId);
                if (string.IsNullOrEmpty(key)) continue;
                if (!map.TryGetValue(key, out var item))
                {
                    item = new JeiItem { ItemId = key, Source = new List<JeiSourceTab>(), Usage = new List<JeiUsageTab>() };
                    // Usage：仍然完全来自 JSON
                    foreach (var u in it.Usage ?? Enumerable.Empty<JeiUsageTab>())
                    {
                        if (u == null) continue;
                        item.Usage.Add(CloneUsage(u));
                    }
                    map[key] = item;
                }
                foreach (var s in it.Source ?? Enumerable.Empty<JeiSourceTab>())
                {
                    if (s == null) continue;
                    item.Source.Add(CloneSource(s));
                }
            }

            return map;
        }

        private static IEnumerable<string> RepeatId(string id, int amount)
        {
            if (amount <= 0) amount = 1;
            for (int i = 0; i < amount; i++) yield return id;
        }

        private static JeiSourceTab CloneSource(JeiSourceTab s)
        {
            return new JeiSourceTab
            {
                IfFabricator = s.IfFabricator,
                Text = s.Text,
                Patch = s.Patch,
                Image = s.Image,
                TabIcon = s.TabIcon,
                Fabricator = s.Fabricator,
                FabricatorDisplayName = s.FabricatorDisplayName,
                Ingredient = new List<string>(s.Ingredient ?? new List<string>())
            };
        }

        private static JeiUsageTab CloneUsage(JeiUsageTab u)
        {
            return new JeiUsageTab
            {
                IfFabricator = u.IfFabricator,
                Text = u.Text,
                Patch = u.Patch,
                Image = u.Image,
                TabIcon = u.TabIcon,
                Fabricator = u.Fabricator,
                FabricatorDisplayName = u.FabricatorDisplayName,
                Ingredient = new List<string>(u.Ingredient ?? new List<string>()),
                Target = new List<string>(u.Target ?? new List<string>())
            };
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Trim();
            const string p = "TechType.";
            if (t.StartsWith(p, StringComparison.OrdinalIgnoreCase)) t = t.Substring(p.Length);
            return t.Trim();
        }
    }
}
