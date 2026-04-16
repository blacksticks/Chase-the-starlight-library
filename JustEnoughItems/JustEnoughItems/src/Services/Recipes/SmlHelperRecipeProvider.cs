using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JustEnoughItems
{
    // Legacy 68598 + SMLHelper 2.15 兼容的配方提供者（完全基于反射，避免编译期绑定）
    public class SmlHelperRecipeProvider : IRecipeProvider
    {
        public string Name => "SMLHelper/Legacy Reflection Scanner";


        private static int GetEnumerableCount(object dict)
        {
            try
            {
                var pi = dict.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (pi != null)
                {
                    var v = pi.GetValue(dict);
                    if (v is int i) return i;
                }
            }
            catch { }
            int c = 0;
            try
            {
                foreach (var _ in (dict as System.Collections.IEnumerable)) { c++; if (c > 0) break; }
            }
            catch { }
            return c;
        }

        public ScanResult Scan()
        {
            var result = new ScanResult();
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var asmCSharp = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                // 容错：若未命中，也允许为空（后续类型解析将遍历所有程序集）

                // 兼容旧版：在所有已加载程序集中解析类型，避免强依赖单一程序集
                Type techType = null, craftData = null, techDataType = null;
                foreach (var asm in assemblies)
                {
                    try
                    {
                        techType = techType ?? asm.GetType("TechType");
                        craftData = craftData ?? asm.GetType("CraftData");
                        techDataType = techDataType ?? asm.GetType("TechData");
                    }
                    catch { }
                }
                // 兜底：按 Name 匹配（忽略命名空间），遍历所有类型
                if (techType == null)
                {
                    try
                    {
                        foreach (var asm in assemblies)
                        {
                            try
                            {
                                var types = asm.GetTypes();
                                foreach (var t in types)
                                {
                                    if (t != null && t.IsEnum && string.Equals(t.Name, "TechType", StringComparison.Ordinal)) { techType = t; break; }
                                }
                                if (techType != null) break;
                            }
                            catch { }
                        }
                        
                    }
                    catch { }
                }
                if (craftData == null)
                {
                    try
                    {
                        foreach (var asm in assemblies)
                        {
                            try
                            {
                                var types = asm.GetTypes();
                                foreach (var t in types)
                                {
                                    if (t != null && string.Equals(t.Name, "CraftData", StringComparison.Ordinal)) { craftData = t; break; }
                                }
                                if (craftData != null) break;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                if (techDataType == null)
                {
                    try
                    {
                        foreach (var asm in assemblies)
                        {
                            try
                            {
                                var types = asm.GetTypes();
                                foreach (var t in types)
                                {
                                    if (t != null && string.Equals(t.Name, "TechData", StringComparison.Ordinal)) { techDataType = t; break; }
                                }
                                if (techDataType != null) break;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                if (techType == null)
                {
                    try { Plugin.Log?.LogWarning("[Just Enough Items] Scan aborted: type 'TechType' not found in loaded assemblies."); } catch { }
                    try { UnityEngine.Debug.LogWarning("[Just Enough Items] Scan aborted: type 'TechType' not found in loaded assemblies."); } catch { }
                    return result;
                }
                if (craftData == null)
                {
                    try { Plugin.Log?.LogWarning("[Just Enough Items] Scan: type 'CraftData' not found. Skipping CraftData channels."); } catch { }
                    try { UnityEngine.Debug.LogWarning("[Just Enough Items] Scan: type 'CraftData' not found. Skipping CraftData channels."); } catch { }
                }
                // TechData 全局类型缺失并不致命，我们会在运行时用 td.GetType() 解析成员
                if (techDataType == null)
                {
                    try { Plugin.Log?.LogWarning("[Just Enough Items] Scan: type 'TechData' not found globally. Will infer from returned objects."); } catch { }
                    try { UnityEngine.Debug.LogWarning("[Just Enough Items] Scan: type 'TechData' not found globally. Will infer from returned objects."); } catch { }
                }


                // CraftData.Get(TechType) -> TechData（允许缺失；仅跳过该通道），动态搜索任意符合签名的方法
                MethodInfo miGet = null;
                if (craftData != null)
                {
                    try
                    {
                        // 先尝试常见名称
                        miGet = craftData.GetMethod("Get", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { techType }, null)
                               ?? craftData.GetMethod("GetTechData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { techType }, null);
                        // 若未命中，遍历全部静态方法，找形参唯一且为 TechType，返回类型名包含 "TechData" 或 "RecipeData"
                        if (miGet == null)
                        {
                            foreach (var m in craftData.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                            {
                                var ps = m.GetParameters();
                                if (ps.Length == 1 && ps[0].ParameterType == techType)
                                {
                                    var rt = m.ReturnType;
                                    var rtName = rt != null ? rt.FullName ?? rt.Name : string.Empty;
                                    if (!string.IsNullOrEmpty(rtName) &&
                                        (rtName.IndexOf("TechData", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         rtName.IndexOf("RecipeData", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        miGet = m; break;
                                    }
                                }
                            }
                        }
                        
                    }
                    catch { }
                    
                }

                int addedGet = 0, addedSml = 0, addedDict = 0;
                if (miGet != null)
                {
                    int baseCount0 = result.Recipes.Count;
                    Array values = Enum.GetValues(techType);
                    foreach (var tt in values)
                    {
                        object td = null;
                        try { td = miGet.Invoke(null, new object[] { tt }); } catch { td = null; }
                        if (td == null) continue;

                        var ingredients = ReadIngredientsFromTechData(td);
                        if (ingredients.Count == 0) continue;

                        // Products：当前 TechType 自身
                        string productId = tt.ToString();
                        var entry = new RecipeEntry
                        {
                            FabricatorId = ResolveFabricatorIdFor(tt) ?? string.Empty,
                            FabricatorDisplayName = null,
                            Ingredients = ingredients,
                            Products = new List<string> { productId }
                        };
                        result.Recipes.Add(entry);
                    }
                    addedGet = result.Recipes.Count - baseCount0;
                }

                // 追加通道：使用 SMLHelper 的 CraftDataHandler.GetTechData(TechType)
                int baseCount1 = result.Recipes.Count;
                try
                {
                    AppendFromSmlHelper(asmCSharp, techType, result);
                }
                catch { }
                addedSml = result.Recipes.Count - baseCount1;

                // 追加通道：直接读取 SMLHelper CraftDataHandler 的静态字典（有些版本仅维护内部表）
                int baseCountSmlDict = result.Recipes.Count;
                try
                {
                    AppendFromSmlHelperDictionaries(techType, result);
                }
                catch { }
                var addedSmlDict = result.Recipes.Count - baseCountSmlDict;

                // 追加通道：直接读取 CraftData 的静态字典（避免 Get 在时机上为 null）
                if (craftData != null)
                {
                    int baseCount2 = result.Recipes.Count;
                    try
                    {
                        AppendFromCraftDataDictionary(craftData, techType, result);
                    }
                    catch { }
                    addedDict = result.Recipes.Count - baseCount2;
                    try { Plugin.Log?.LogInfo($"[Just Enough Items] Scan CraftData static dictionary added={addedDict}"); } catch { }
                    try { UnityEngine.Debug.Log($"[Just Enough Items] Scan CraftData static dictionary added={addedDict}"); } catch { }
                }
                // 无需方法级兜底与全局任意字典兜底
            }
            catch { }
            return result;
        }

        
        private static List<RecipeIngredient> ReadIngredientsFromTechData(object td)
        {
            var list = new List<RecipeIngredient>();
            if (td == null) return list;
            try
            {
                var tTd = td.GetType();
                // 尝试多种字段/属性名
                var piIngredients = tTd.GetProperty("Ingredients", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetProperty("ingredients", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetProperty("ingredientList", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetProperty("_ingredients", BindingFlags.NonPublic | BindingFlags.Instance);
                var fiIngredients = tTd.GetField("Ingredients", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetField("ingredients", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetField("ingredientList", BindingFlags.Public | BindingFlags.Instance)
                                   ?? tTd.GetField("_ingredients", BindingFlags.NonPublic | BindingFlags.Instance);
                object listObj = piIngredients != null ? piIngredients.GetValue(td) : (fiIngredients != null ? fiIngredients.GetValue(td) : null);
                if (listObj is System.Collections.IEnumerable en)
                {
                    foreach (var ing in en)
                    {
                        if (ing == null) continue;
                        var tIng = ing.GetType();
                        // Ingredient 的标识与数量字段：尽可能多兼容
                        var fiId = tIng.GetField("ingredient", BindingFlags.Public | BindingFlags.Instance)
                                    ?? tIng.GetField("techType", BindingFlags.Public | BindingFlags.Instance)
                                    ?? tIng.GetField("_techType", BindingFlags.NonPublic | BindingFlags.Instance)
                                    ?? tIng.GetField("techTypeID", BindingFlags.Public | BindingFlags.Instance);
                        var fiAmt = tIng.GetField("amount", BindingFlags.Public | BindingFlags.Instance)
                                     ?? tIng.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? tIng.GetField("count", BindingFlags.Public | BindingFlags.Instance);
                        var piId = tIng.GetProperty("ingredient", BindingFlags.Public | BindingFlags.Instance)
                                    ?? tIng.GetProperty("techType", BindingFlags.Public | BindingFlags.Instance)
                                    ?? tIng.GetProperty("TechType", BindingFlags.Public | BindingFlags.Instance);
                        var piAmt = tIng.GetProperty("amount", BindingFlags.Public | BindingFlags.Instance)
                                     ?? tIng.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)
                                     ?? tIng.GetProperty("count", BindingFlags.Public | BindingFlags.Instance);
                        string id = null; int amt = 1;
                        try { var val = (fiId != null ? fiId.GetValue(ing) : (piId != null ? piId.GetValue(ing) : null)); id = val?.ToString(); } catch { }
                        try { var val = (fiAmt != null ? fiAmt.GetValue(ing) : (piAmt != null ? piAmt.GetValue(ing) : null)); if (val is int i) amt = Math.Max(1, i); } catch { }
                        if (!string.IsNullOrEmpty(id)) list.Add(new RecipeIngredient { TechId = id, Amount = amt });
                    }
                    if (list.Count > 0) return list;
                }
                // 回退：按索引访问（存在 IngredientCount/ingredientCount，并提供 GetIngredient(int) 方法的版本）
                try
                {
                    var piCnt = tTd.GetProperty("IngredientCount", BindingFlags.Public | BindingFlags.Instance)
                               ?? tTd.GetProperty("ingredientCount", BindingFlags.Public | BindingFlags.Instance)
                               ?? tTd.GetProperty("_ingredientCount", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fiCnt = tTd.GetField("IngredientCount", BindingFlags.Public | BindingFlags.Instance)
                               ?? tTd.GetField("ingredientCount", BindingFlags.Public | BindingFlags.Instance)
                               ?? tTd.GetField("_ingredientCount", BindingFlags.NonPublic | BindingFlags.Instance);
                    int cnt = 0;
                    try { var v = piCnt != null ? piCnt.GetValue(td) : (fiCnt != null ? fiCnt.GetValue(td) : null); if (v is int c) cnt = c; } catch { }
                    if (cnt > 0)
                    {
                        var miGetIng = tTd.GetMethod("GetIngredient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                                      ?? tTd.GetMethod("get_Ingredient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (miGetIng != null)
                        {
                            for (int i = 0; i < cnt; i++)
                            {
                                object ing = null;
                                try { ing = miGetIng.Invoke(td, new object[] { i }); } catch { ing = null; }
                                if (ing == null) continue;
                                var tIng = ing.GetType();
                                var fiId2 = tIng.GetField("ingredient", BindingFlags.Public | BindingFlags.Instance)
                                            ?? tIng.GetField("techType", BindingFlags.Public | BindingFlags.Instance);
                                var piId2 = tIng.GetProperty("ingredient", BindingFlags.Public | BindingFlags.Instance)
                                            ?? tIng.GetProperty("techType", BindingFlags.Public | BindingFlags.Instance)
                                            ?? tIng.GetProperty("TechType", BindingFlags.Public | BindingFlags.Instance);
                                var fiAmt2 = tIng.GetField("amount", BindingFlags.Public | BindingFlags.Instance)
                                             ?? tIng.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)
                                             ?? tIng.GetField("count", BindingFlags.Public | BindingFlags.Instance);
                                var piAmt2 = tIng.GetProperty("amount", BindingFlags.Public | BindingFlags.Instance)
                                             ?? tIng.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)
                                             ?? tIng.GetProperty("count", BindingFlags.Public | BindingFlags.Instance);
                                string id2 = null; int amt2 = 1;
                                try { var val = (fiId2 != null ? fiId2.GetValue(ing) : (piId2 != null ? piId2.GetValue(ing) : null)); id2 = val?.ToString(); } catch { }
                                try { var val = (fiAmt2 != null ? fiAmt2.GetValue(ing) : (piAmt2 != null ? piAmt2.GetValue(ing) : null)); if (val is int iv) amt2 = Math.Max(1, iv); } catch { }
                                if (!string.IsNullOrEmpty(id2)) list.Add(new RecipeIngredient { TechId = id2, Amount = amt2 });
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
            return list;
        }

        private void AppendFromSmlHelper(System.Reflection.Assembly asmCSharp, Type techType, ScanResult result)
        {
            try
            {
                Type tHandler = null;
                // 优先直接通过全名解析
                try { tHandler = Type.GetType("SMLHelper.V2.Handlers.CraftDataHandler, SMLHelper", throwOnError: false); } catch { }
                if (tHandler == null)
                {
                    // 在所有已加载程序集中查找该类型，避免程序集名称差异
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { tHandler = asm.GetType("SMLHelper.V2.Handlers.CraftDataHandler"); } catch { tHandler = null; }
                        if (tHandler != null) break;
                    }
                }
                if (tHandler == null) { try { Plugin.Log?.LogInfo("[Just Enough Items] AppendFromSmlHelper: CraftDataHandler not found"); } catch { } return; }
                // static SMLHelper.V2.Crafting.TechData GetTechData(TechType)
                var miGetTD = tHandler.GetMethod("GetTechData", BindingFlags.Public | BindingFlags.Static, null, new Type[] { techType }, null);
                if (miGetTD == null) { try { Plugin.Log?.LogInfo("[Just Enough Items] AppendFromSmlHelper: GetTechData method not found"); } catch { } return; }

                Array values = Enum.GetValues(techType);
                foreach (var tt in values)
                {
                    object td = null;
                    try { td = miGetTD.Invoke(null, new object[] { tt }); } catch { td = null; }
                    if (td == null) continue;

                    var ingredients = ReadIngredientsFromTechData(td);
                    if (ingredients.Count == 0) continue;

                    // 产物为当前 TechType
                    string productId = tt.ToString();
                    var entry = new RecipeEntry
                    {
                        FabricatorId = ResolveFabricatorIdFor(tt) ?? string.Empty,
                        FabricatorDisplayName = null,
                        Ingredients = ingredients,
                        Products = new List<string> { productId }
                    };
                    result.Recipes.Add(entry);
                }
            }
            catch { }
        }

        // 直接读取 SMLHelper CraftDataHandler 的所有静态字典（字段），兼容泛型 Dictionary<TKey, TValue>
        private void AppendFromSmlHelperDictionaries(Type techType, ScanResult result)
        {
            try
            {
                Type tHandler = null;
                try { tHandler = Type.GetType("SMLHelper.V2.Handlers.CraftDataHandler, SMLHelper", throwOnError: false); } catch { }
                if (tHandler == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { tHandler = asm.GetType("SMLHelper.V2.Handlers.CraftDataHandler"); } catch { tHandler = null; }
                        if (tHandler != null) break;
                    }
                }
                if (tHandler == null) return;

                var dictObjs = new List<object>();
                foreach (var f in tHandler.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        var candidate = f.GetValue(null);
                        if (candidate == null) continue;
                        var ien = candidate as System.Collections.IEnumerable;
                        if (ien == null) continue;
                        object any = null; foreach (var kv in ien) { any = kv; break; }
                        if (any == null) continue;
                        var tKV = any.GetType();
                        var piV = tKV.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                        if (piV == null) continue;
                        var vObj = piV.GetValue(any);
                        var valTypeName = vObj?.GetType()?.FullName ?? vObj?.GetType()?.Name ?? string.Empty;
                        if (!string.IsNullOrEmpty(valTypeName) &&
                            (valTypeName.IndexOf("TechData", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             valTypeName.IndexOf("RecipeData", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            dictObjs.Add(candidate);
                            try { Plugin.Log?.LogInfo($"[Just Enough Items] Scan: SMLHelper dictionary resolved -> {f.Name}"); } catch { }
                            try { UnityEngine.Debug.Log($"[Just Enough Items] Scan: SMLHelper dictionary resolved -> {f.Name}"); } catch { }
                        }
                    }
                    catch { }
                }
                foreach (var dict in dictObjs)
                {
                    var ien = dict as System.Collections.IEnumerable;
                    if (ien == null) continue;
                    foreach (var kv in ien)
                    {
                        var tKV = kv?.GetType();
                        if (tKV == null) continue;
                        var piK = tKV.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                        var piV = tKV.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                        var key = piK?.GetValue(kv); var td = piV?.GetValue(kv);
                        if (key == null || td == null) continue;

                        var ingredients = ReadIngredientsFromTechData(td);
                        if (ingredients.Count == 0) continue;

                        string productId = key.ToString();
                        var entry = new RecipeEntry
                        {
                            FabricatorId = ResolveFabricatorIdFor(key) ?? string.Empty,
                            FabricatorDisplayName = null,
                            Ingredients = ingredients,
                            Products = new List<string> { productId }
                        };
                        result.Recipes.Add(entry);
                    }
                }
            }
            catch { }
        }

        // 直接读取 CraftData 的静态字典（避免 Get 在初始化时机为 null），兼容泛型 Dictionary<TKey, TValue>
        private void AppendFromCraftDataDictionary(Type craftData, Type techType, ScanResult result)
        {
            try
            {
                // 优先：若传入的 craftData 不包含目标字典，尝试在所有程序集中重新解析“真正包含 techData 字段”的 CraftData 类型
                if (craftData == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type cand = null;
                        try { cand = asm.GetType("CraftData"); } catch { cand = null; }
                        if (cand == null)
                        {
                            try
                            {
                                foreach (var t in asm.GetTypes()) { if (t != null && t.Name == "CraftData") { cand = t; break; } }
                            }
                            catch { }
                        }
                        if (cand == null) continue;
                        try
                        {
                            var f0 = cand.GetField("techData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            if (f0 != null) { craftData = cand; break; }
                        }
                        catch { }
                    }
                    try { UnityEngine.Debug.Log($"[JEI][Scan] CraftData fallback pick -> {(craftData != null ? craftData.FullName : "<null>")}"); } catch { }
                }

                // 首先尝试常见字段名（含 SMLHelper 注入路径：CraftData.techData）
                var fi = craftData?.GetField("techData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? craftData?.GetField("s_techData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? craftData?.GetField("techDataByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? craftData?.GetField("techDataByTechType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? craftData?.GetField("recipeData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object dict = null;
                if (fi != null)
                {
                    try { dict = fi.GetValue(null); } catch { dict = null; }
                }
                // 若未命中已知字段，则枚举所有静态 IDictionary 字段作为候选
                if (dict == null)
                {
                    foreach (var f in craftData.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var candidate = f.GetValue(null);
                            if (candidate == null) continue;
                            var ien = candidate as System.Collections.IEnumerable;
                            if (ien == null) continue;
                            object any = null; foreach (var kv in ien) { any = kv; break; }
                            if (any == null) continue;
                            var tKV = any.GetType();
                            var piV = tKV.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                            if (piV == null) continue;
                            var vObj = piV.GetValue(any);
                            var valTypeName = vObj?.GetType()?.FullName ?? vObj?.GetType()?.Name ?? string.Empty;
                            if (!string.IsNullOrEmpty(valTypeName) &&
                                (valTypeName.IndexOf("TechData", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 valTypeName.IndexOf("RecipeData", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                dict = candidate;
                                try { Plugin.Log?.LogInfo($"[Just Enough Items] Scan: CraftData dictionary resolved -> {f.Name}"); } catch { }
                                try { UnityEngine.Debug.Log($"[Just Enough Items] Scan: CraftData dictionary resolved -> {f.Name}"); } catch { }
                                break;
                            }
                        }
                        catch { }
                    }
                }
                // 再尝试扫描所有静态属性，返回 IDictionary/IDictionary<,>
                if (dict == null)
                {
                    foreach (var p in craftData.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var candidate = p.GetValue(null, null);
                            if (candidate == null) continue;
                            var ien = candidate as System.Collections.IEnumerable;
                            if (ien == null) continue;
                            object any = null; foreach (var kv in ien) { any = kv; break; }
                            if (any == null) continue;
                            var tKV = any.GetType();
                            var piV = tKV.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                            if (piV == null) continue;
                            var vObj = piV.GetValue(any);
                            var valTypeName = vObj?.GetType()?.FullName ?? vObj?.GetType()?.Name ?? string.Empty;
                            if (!string.IsNullOrEmpty(valTypeName) &&
                                (valTypeName.IndexOf("TechData", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 valTypeName.IndexOf("RecipeData", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                dict = candidate;
                                try { Plugin.Log?.LogInfo($"[Just Enough Items] Scan: CraftData dictionary resolved (property) -> {p.Name}"); } catch { }
                                try { UnityEngine.Debug.Log($"[Just Enough Items] Scan: CraftData dictionary resolved (property) -> {p.Name}"); } catch { }
                                break;
                            }
                        }
                        catch { }
                    }
                }
                // 诊断：报告选用的 craftData 与字段/属性命中情况
                try
                {
                    var asmName = craftData?.Assembly?.GetName()?.Name ?? "<null-asm>";
                    UnityEngine.Debug.Log($"[JEI][Scan] CraftData type used: {(craftData != null ? craftData.FullName : "<null>")} from {asmName}; DictFound={(dict!=null)}");
                }
                catch { }

                if (dict == null || GetEnumerableCount(dict) == 0)
                {
                    // 诊断：打印 CraftData 静态成员清单，帮助后续适配
                    try
                    {
                        var names = new System.Text.StringBuilder();
                        foreach (var m in craftData.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            try { names.Append(m.MemberType).Append(':').Append(m.Name).Append(';'); } catch { }
                        }
                        Plugin.Log?.LogWarning($"[Just Enough Items] Scan: no CraftData dictionary found. Members => {names.ToString()}");
                    }
                    catch { }
                    // 若找不到字典则跳过
                    try { Plugin.Log?.LogInfo("[Just Enough Items] AppendFromCraftDataDictionary: no valid dictionary found"); } catch { }
                    return;
                }

                var ienDict = dict as System.Collections.IEnumerable;
                if (ienDict == null) { try { Plugin.Log?.LogInfo("[Just Enough Items] AppendFromCraftDataDictionary: dictionary is not enumerable"); } catch { } return; }
                foreach (var kv in ienDict)
                {
                    var tKV = kv?.GetType();
                    if (tKV == null) continue;
                    var piK = tKV.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                    var piV = tKV.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                    var key = piK?.GetValue(kv); var td = piV?.GetValue(kv);
                    if (key == null || td == null) continue;

                    var ingredients = new List<RecipeIngredient>();
                    try
                    {
                        var tTd = td.GetType();
                        var piIngredients = tTd.GetProperty("Ingredients", BindingFlags.Public | BindingFlags.Instance)
                                           ?? tTd.GetProperty("ingredientList", BindingFlags.Public | BindingFlags.Instance)
                                           ?? tTd.GetProperty("_ingredients", BindingFlags.NonPublic | BindingFlags.Instance);
                        var fiIngredients = tTd.GetField("Ingredients", BindingFlags.Public | BindingFlags.Instance)
                                           ?? tTd.GetField("ingredientList", BindingFlags.Public | BindingFlags.Instance)
                                           ?? tTd.GetField("_ingredients", BindingFlags.NonPublic | BindingFlags.Instance);
                        object listObj = piIngredients != null ? piIngredients.GetValue(td) : (fiIngredients != null ? fiIngredients.GetValue(td) : null);
                        if (listObj is System.Collections.IEnumerable en)
                        {
                            foreach (var ing in en)
                            {
                                if (ing == null) continue;
                                var tIng = ing.GetType();
                                var fiId = tIng.GetField("ingredient", BindingFlags.Public | BindingFlags.Instance)
                                            ?? tIng.GetField("techType", BindingFlags.Public | BindingFlags.Instance);
                                var fiAmt = tIng.GetField("amount", BindingFlags.Public | BindingFlags.Instance)
                                             ?? tIng.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance);
                                string id = null; int amt = 1;
                                try { var val = fiId?.GetValue(ing); id = val?.ToString(); } catch { }
                                try { var val = fiAmt?.GetValue(ing); if (val is int i) amt = Math.Max(1, i); } catch { }
                                if (!string.IsNullOrEmpty(id)) ingredients.Add(new RecipeIngredient { TechId = id, Amount = amt });
                            }
                        }
                    }
                    catch { }
                    if (ingredients.Count == 0) continue;

                    string productId = key.ToString();
                    var entry = new RecipeEntry
                    {
                        FabricatorId = ResolveFabricatorIdFor(key) ?? string.Empty,
                        FabricatorDisplayName = null,
                        Ingredients = ingredients,
                        Products = new List<string> { productId }
                    };
                    result.Recipes.Add(entry);
                }
            }
            catch { }
        }

        // 粗略解析所在工作台：尝试通过 CraftTree 反查（若失败则返回 null）
        private string ResolveFabricatorIdFor(object techTypeValue)
        {
            try
            {
                var asmCSharp = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (asmCSharp == null) return null;
                var craftTree = asmCSharp.GetType("CraftTree");
                if (craftTree == null) return null;
                // Legacy 没有统一查询 API，尝试已知枚举 CraftTree.Type 并匹配路径
                var typeEnum = craftTree.GetNestedType("Type", BindingFlags.Public | BindingFlags.NonPublic);
                if (typeEnum == null) return null;

                var getNode = craftTree.GetMethod("GetNodeForTechType", BindingFlags.Public | BindingFlags.Static);
                if (getNode == null) return null;
                foreach (var t in Enum.GetValues(typeEnum))
                {
                    try
                    {
                        var node = getNode.Invoke(null, new object[] { t, techTypeValue });
                        if (node != null)
                        {
                            // 命中即可用该 CraftTree.Type 作为工作台 Id
                            return t.ToString();
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }
    }
}
