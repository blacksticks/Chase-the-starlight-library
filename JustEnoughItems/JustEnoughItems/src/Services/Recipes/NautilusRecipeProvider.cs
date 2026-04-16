using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JustEnoughItems
{
    public class NautilusRecipeProvider : IRecipeProvider
    {
        public string Name => "Nautilus";

        public ScanResult Scan()
        {
            var result = new ScanResult();
            try
            {
                var techType = AccessType("TechType");
                if (techType == null) return result;
                var cdh = AccessType("Nautilus.Handlers.CraftDataHandler");
                if (cdh == null) return result;
                var miGetRecipeData = cdh.GetMethod("GetRecipeData", BindingFlags.Public | BindingFlags.Static, null, new[] { techType }, null);
                if (miGetRecipeData == null) return result;

                // 1) 先构建“产物 -> 工作台代表 TechType 名称”的映射（通过遍历 CraftTree）
                var productToFabricator = BuildFabricatorMap(techType);

                Array all = Enum.GetValues(techType);
                foreach (var tt in all)
                {
                    object recipeData = null;
                    try { recipeData = miGetRecipeData.Invoke(null, new object[] { tt }); } catch { recipeData = null; }
                    if (recipeData == null) continue;

                    var entry = new RecipeEntry();
                    var mainId = SafeTechTypeToString(tt);
                    if (!string.IsNullOrEmpty(mainId) && productToFabricator.TryGetValue(mainId, out var fab))
                    {
                        entry.FabricatorId = fab;
                        entry.FabricatorDisplayName = string.Empty; // 置空以使用 UI 的游戏内本地化显示名
                    }
                    else
                    {
                        entry.FabricatorId = string.Empty;
                        entry.FabricatorDisplayName = string.Empty;
                    }

                    var rdType = recipeData.GetType();

                    int ingredientCount = GetIntProp(rdType, recipeData, "ingredientCount");
                    var ingredients = new List<RecipeIngredient>();
                    var miGetIngredient = rdType.GetMethod("GetIngredient", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (miGetIngredient != null && ingredientCount > 0)
                    {
                        for (int i = 0; i < ingredientCount; i++)
                        {
                            try
                            {
                                var ing = miGetIngredient.Invoke(recipeData, new object[] { i });
                                if (ing == null) continue;
                                var ingType = ing.GetType();
                                string id = GetTechTypeName(ingType, ing, techType, "techType");
                                int amount = GetIntFieldOrProp(ingType, ing, "amount");
                                if (!string.IsNullOrEmpty(id) && amount != 0)
                                {
                                    ingredients.Add(new RecipeIngredient { TechId = id, Amount = Math.Max(1, amount) });
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        var fldIngredients = rdType.GetField("Ingredients", BindingFlags.Public | BindingFlags.Instance);
                        var list = fldIngredients?.GetValue(recipeData) as System.Collections.IEnumerable;
                        if (list != null)
                        {
                            foreach (var ing in list)
                            {
                                try
                                {
                                    var ingType = ing.GetType();
                                    string id = GetTechTypeName(ingType, ing, techType, "techType");
                                    int amount = GetIntFieldOrProp(ingType, ing, "amount");
                                    if (!string.IsNullOrEmpty(id) && amount != 0)
                                        ingredients.Add(new RecipeIngredient { TechId = id, Amount = Math.Max(1, amount) });
                                }
                                catch { }
                            }
                        }
                    }

                    var products = new List<string>();
                    mainId = SafeTechTypeToString(tt);
                    if (!string.IsNullOrEmpty(mainId)) products.Add(mainId);

                    int linkedCount = GetIntProp(rdType, recipeData, "linkedItemCount");
                    var miGetLinked = rdType.GetMethod("GetLinkedItem", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (miGetLinked != null && linkedCount > 0)
                    {
                        for (int i = 0; i < linkedCount; i++)
                        {
                            try
                            {
                                var linked = miGetLinked.Invoke(recipeData, new object[] { i });
                                var lid = SafeTechTypeToString(linked);
                                if (!string.IsNullOrEmpty(lid)) products.Add(lid);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        var fldLinked = rdType.GetField("LinkedItems", BindingFlags.Public | BindingFlags.Instance);
                        var list = fldLinked?.GetValue(recipeData) as System.Collections.IEnumerable;
                        if (list != null)
                        {
                            foreach (var linked in list)
                            {
                                var lid = SafeTechTypeToString(linked);
                                if (!string.IsNullOrEmpty(lid)) products.Add(lid);
                            }
                        }
                    }

                    if (ingredients.Count == 0 || products.Count == 0) continue;
                    entry.Ingredients = ingredients;
                    entry.Products = products.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    result.Recipes.Add(entry);
                }
            }
            catch (Exception)
            {
            }
            return result;
        }

        private static Dictionary<string, string> BuildFabricatorMap(Type techType)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var tCraftTree = AccessType("CraftTree");
                if (tCraftTree == null) return map;
                // 嵌套枚举 CraftTree.Type
                var tTreeType = tCraftTree.GetNestedType("Type", BindingFlags.Public | BindingFlags.NonPublic);
                if (tTreeType == null || !tTreeType.IsEnum) return map;
                var miGetTree = tCraftTree.GetMethod("GetTree", BindingFlags.Public | BindingFlags.Static, null, new[] { tTreeType }, null);
                if (miGetTree == null) return map;

                foreach (var treeEnum in Enum.GetValues(tTreeType))
                {
                    object treeObj = null;
                    try { treeObj = miGetTree.Invoke(null, new object[] { treeEnum }); } catch { treeObj = null; }
                    if (treeObj == null) continue;

                    // 访问 root 节点
                    var treeObjType = treeObj.GetType();
                    object root = GetFieldOrProp(treeObjType, treeObj, "root")
                                  ?? GetFieldOrProp(treeObjType, treeObj, "mRoot")
                                  ?? GetFieldOrProp(treeObjType, treeObj, "_root");
                    if (root == null) continue;

                    var techs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    DfsCollectTechTypes(root, techType, techs);
                    var treeName = treeEnum.ToString();
                    var fabId = ResolveRepresentativeTechTypeName(treeName, techType) ?? treeName;
                    foreach (var pid in techs)
                    {
                        if (!map.ContainsKey(pid)) map[pid] = fabId;
                    }
                }
            }
            catch { }
            return map;
        }

        // 将 CraftTree.Type 名称映射为用于图标/显示的代表性 TechType 名称
        private static string ResolveRepresentativeTechTypeName(string treeName, Type techType)
        {
            if (string.IsNullOrEmpty(treeName) || techType == null) return null;
            try
            {
                // 1) 尝试同名 TechType（如 Fabricator/CyclopsFabricator/VehicleUpgradeConsole/Constructor 等）
                try { var _ = Enum.Parse(techType, treeName, true); return treeName; } catch { }

                // 2) 常见映射（Subnautica）
                // Workbench -> ModificationStation (游戏内工作台实际名称)
                if (treeName.Equals("Workbench", StringComparison.OrdinalIgnoreCase))
                {
                    try { var _ = Enum.Parse(techType, "ModificationStation", true); return "ModificationStation"; } catch { }
                }
                // SeamothUpgrades/VehicleUpgrades -> VehicleUpgradeConsole
                if (treeName.Equals("SeamothUpgrades", StringComparison.OrdinalIgnoreCase) || treeName.Equals("VehicleUpgrades", StringComparison.OrdinalIgnoreCase))
                {
                    try { var _ = Enum.Parse(techType, "VehicleUpgradeConsole", true); return "VehicleUpgradeConsole"; } catch { }
                }
                // MobileVehicleBay 映射（Constructor）
                if (treeName.Equals("MobileVehicleBay", StringComparison.OrdinalIgnoreCase))
                {
                    try { var _ = Enum.Parse(techType, "Constructor", true); return "Constructor"; } catch { }
                }

                // 3) 通用匹配策略：基于名称归一化进行模糊匹配（兼容模组工作台）
                string Norm(string s)
                {
                    if (string.IsNullOrEmpty(s)) return string.Empty;
                    s = s.Trim();
                    // 去掉常见后缀/前缀
                    var lowers = s.ToLowerInvariant();
                    string[] remove = { "tree", "menu", "crafttree", "craft", "fabrication", "station", "workbench" };
                    foreach (var rm in remove)
                    {
                        if (lowers.EndsWith(rm)) { s = s.Substring(0, s.Length - rm.Length); lowers = s.ToLowerInvariant(); }
                        if (lowers.StartsWith(rm)) { s = s.Substring(rm.Length); lowers = s.ToLowerInvariant(); }
                    }
                    // 去非字母数字
                    var arr = s.Where(char.IsLetterOrDigit).ToArray();
                    return new string(arr).ToLowerInvariant();
                }
                var normTree = Norm(treeName);
                if (string.IsNullOrEmpty(normTree)) normTree = treeName.ToLowerInvariant();

                var names = Enum.GetNames(techType) ?? Array.Empty<string>();
                // 3.1 先尝试完全相等（归一化后）
                foreach (var n in names)
                {
                    if (Norm(n) == normTree) return n;
                }
                // 3.2 尝试包含关系：TechType 包含 Tree，或 Tree 包含 TechType
                foreach (var n in names)
                {
                    var nn = Norm(n);
                    if (nn.Contains(normTree) || normTree.Contains(nn)) return n;
                }
                // 3.3 偏好包含 fabricator/bench/console/constructor 等关键词的 TechType
                string[] pref = { "fabricator", "bench", "console", "constructor", "station" };
                foreach (var n in names)
                {
                    var nn = n.ToLowerInvariant();
                    if (pref.Any(k => nn.Contains(k))) return n;
                }
            }
            catch { }
            return null;
        }

        private static void DfsCollectTechTypes(object node, Type techType, HashSet<string> output)
        {
            if (node == null) return;
            var nt = node.GetType();
            // 读取 craft 节点上的 techType 字段/属性
            var vTech = GetFieldOrProp(nt, node, "techType") ?? GetFieldOrProp(nt, node, "TechType");
            var id = SafeTechTypeToString(vTech);
            if (!string.IsNullOrEmpty(id)) output.Add(id);

            // 遍历子节点集合：尝试常见命名
            var children = GetFieldOrProp(nt, node, "childNodes")
                           ?? GetFieldOrProp(nt, node, "children")
                           ?? GetFieldOrProp(nt, node, "nodes")
                           ?? GetFieldOrProp(nt, node, "_children");
            if (children is System.Collections.IEnumerable en)
            {
                foreach (var ch in en) DfsCollectTechTypes(ch, techType, output);
            }
        }

        private static object GetFieldOrProp(Type t, object o, string name)
        {
            try { var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) return f.GetValue(o); } catch { }
            try { var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (p != null) return p.GetValue(o); } catch { }
            return null;
        }

        private static Type AccessType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName, false); if (t != null) return t; } catch { }
            }
            return null;
        }

        private static int GetIntProp(Type t, object o, string name)
        {
            try { var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance); if (p != null) return Convert.ToInt32(p.GetValue(o)); } catch { }
            return 0;
        }

        private static int GetIntFieldOrProp(Type t, object o, string name)
        {
            try { var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance); if (f != null) return Convert.ToInt32(f.GetValue(o)); } catch { }
            try { var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance); if (p != null) return Convert.ToInt32(p.GetValue(o)); } catch { }
            return 0;
        }

        private static string GetTechTypeName(Type t, object o, Type techType, string member)
        {
            try
            {
                object v = null;
                var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) v = f.GetValue(o);
                else
                {
                    var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null) v = p.GetValue(o);
                }
                return SafeTechTypeToString(v);
            }
            catch { return string.Empty; }
        }

        private static string SafeTechTypeToString(object techTypeValue)
        {
            if (techTypeValue == null) return string.Empty;
            try { return techTypeValue.ToString(); } catch { return string.Empty; }
        }
    }
}
