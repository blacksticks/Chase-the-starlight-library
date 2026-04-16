using JustEnoughItems.Config;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JustEnoughItems
{
    // 规则：
    // - 扫描结果为主，用于填充 Source（产物侧）。
    // - JSON 用于填充 Usage（用途侧），以及补充 Items 的显示信息（名称/图标/描述等），不覆盖扫描的语义数据。
    // - 合并阶段仅做扫描配方去重与数据打包；具体落位在 JeiModelBuilder 中完成。
    public class RecipeMerger
    {
        public class MergedData
        {
            // 扫描得到的“配方条目”
            public List<RecipeEntry> ScannedRecipes { get; set; } = new List<RecipeEntry>();
            // JSON 追加的“显示用配方”（会在导出为 JEI 模型时按 Source/Usage 填充）
            public JeiConfig JsonConfig { get; set; } = new JeiConfig();
        }

        public MergedData Merge(ScanResult scan, JeiConfig json)
        {
            var merged = new MergedData();
            merged.JsonConfig = json ?? new JeiConfig();

            // 去重扫描配方
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in (scan?.Recipes ?? Enumerable.Empty<RecipeEntry>()))
            {
                var key = MakeKey(r);
                if (set.Add(key)) merged.ScannedRecipes.Add(r);
            }
            try { UnityEngine.Debug.Log($"[JEI][Merge] Scanned dedup count={merged.ScannedRecipes.Count}"); } catch { }

            // 基于扫描结果：为每个“原料物品”构建 Usage，并合并到 JsonConfig.Items
            try
            {
                var map = new Dictionary<string, JeiItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in (merged.JsonConfig?.Items ?? new List<JeiItem>()))
                {
                    if (it == null || string.IsNullOrEmpty(it.ItemId)) continue;
                    var id = Normalize(it.ItemId);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!map.TryGetValue(id, out var exist))
                    {
                        // 复制基本字段与 JSON 的 Usage
                        exist = new JeiItem
                        {
                            ItemId = id,
                            DisplayName = it.DisplayName,
                            Icon = it.Icon,
                            Patch = it.Patch,
                            Description = it.Description,
                            Source = new List<JeiSourceTab>(),
                            Usage = new List<JeiUsageTab>()
                        };
                        foreach (var s in (it.Source ?? new List<JeiSourceTab>())) if (s != null) exist.Source.Add(CloneSource(s));
                        foreach (var u in (it.Usage ?? new List<JeiUsageTab>())) if (u != null) exist.Usage.Add(CloneUsage(u));
                        map[id] = exist;
                    }
                    else
                    {
                        // 合并 JSON 的 Source/Usage（去重稍后统一做）
                        foreach (var s in (it.Source ?? new List<JeiSourceTab>())) if (s != null) exist.Source.Add(CloneSource(s));
                        foreach (var u in (it.Usage ?? new List<JeiUsageTab>())) if (u != null) exist.Usage.Add(CloneUsage(u));
                    }
                }

                // 从扫描配方生成 Usage：每条配方把每个“原料”挂到该原料物品的 Usage，Target=产物列表
                foreach (var r in merged.ScannedRecipes)
                {
                    var products = (r.Products ?? new List<string>()).Select(Normalize).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    var ingredients = (r.Ingredients ?? new List<RecipeIngredient>())
                        .Where(x => x != null && !string.IsNullOrEmpty(x.TechId))
                        .Select(x => new { Id = Normalize(x.TechId), x.Amount })
                        .Where(x => !string.IsNullOrEmpty(x.Id))
                        .ToList();
                    if (products.Count == 0 || ingredients.Count == 0) continue;

                    var ingredientList = ingredients.SelectMany(x => RepeatId(x.Id, x.Amount)).ToList();
                    foreach (var ing in ingredients)
                    {
                        var iid = ing.Id;
                        if (string.IsNullOrEmpty(iid)) continue;
                        if (!map.TryGetValue(iid, out var item))
                        {
                            item = new JeiItem { ItemId = iid, Source = new List<JeiSourceTab>(), Usage = new List<JeiUsageTab>() };
                            map[iid] = item;
                        }
                        var tab = new JeiUsageTab
                        {
                            IfFabricator = true,
                            Fabricator = r.FabricatorId,
                            FabricatorDisplayName = r.FabricatorDisplayName,
                            Ingredient = new List<string>(ingredientList),
                            Text = string.Empty,
                            Patch = string.Empty,
                            Image = string.Empty,
                            TabIcon = string.Empty,
                            Target = new List<string>(products)
                        };
                        item.Usage.Add(tab);
                    }
                }

                // 去重每个物品的 Usage（以 Fabricator/DisplayName/Ingredient/Target 归一化作为键）
                foreach (var kv in map)
                {
                    var it = kv.Value;
                    if (it?.Usage != null && it.Usage.Count > 1)
                    {
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var dedup = new List<JeiUsageTab>();
                        foreach (var u in it.Usage)
                        {
                            if (u == null) continue;
                            var keyU = string.Join("|", new[]
                            {
                                u.IfFabricator ? "F" : "N",
                                u.Fabricator ?? string.Empty,
                                u.FabricatorDisplayName ?? string.Empty,
                                string.Join("+", (u.Ingredient ?? new List<string>()).Select(Normalize).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
                                string.Join("+", (u.Target ?? new List<string>()).Select(Normalize).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            });
                            if (seen.Add(keyU)) dedup.Add(u);
                        }
                        it.Usage = dedup;
                    }
                }

                // 统计 Usage 总数
                int totalUsage = 0; foreach (var v in map.Values) totalUsage += (v?.Usage?.Count ?? 0);
                try { UnityEngine.Debug.Log($"[JEI][Merge] Items(before)={(merged.JsonConfig?.Items?.Count ?? 0)}, Items(after)={map.Count}, TotalUsageTabs={totalUsage}"); } catch { }
                // 回写为 JsonConfig.Items，以便 JeiModelBuilder 直接读取 Usage
                merged.JsonConfig.Items = map.Values.ToList();
            }
            catch { }
            return merged;
        }

        private static string MakeKey(RecipeEntry r)
        {
            var ingr = string.Join("+", (r.Ingredients ?? new List<RecipeIngredient>())
                .OrderBy(x => x.TechId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Amount)
                .Select(x => $"{x.TechId}*{x.Amount}"));
            var prods = string.Join("+", (r.Products ?? new List<string>()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            return $"{r.FabricatorId}|{ingr}|{prods}";
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Trim();
            const string p = "TechType.";
            if (t.StartsWith(p, StringComparison.OrdinalIgnoreCase)) t = t.Substring(p.Length);
            return t.Trim();
        }

        private static IEnumerable<string> RepeatId(string id, int amount)
        {
            if (amount <= 0) amount = 1;
            for (int i = 0; i < amount; i++) yield return id;
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
    }
}
