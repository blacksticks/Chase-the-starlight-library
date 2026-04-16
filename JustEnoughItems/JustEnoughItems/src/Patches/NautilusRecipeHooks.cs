using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace JustEnoughItems.Patches
{
    // 动态 Hook：Nautilus 在注册/设置配方时触发 JEI 数据重建
    [HarmonyPatch]
    internal static class NautilusRecipeHooks
    {
        private static DateTime _lastBuildAtUtc = DateTime.MinValue;
        private const int MinIntervalMs = 1000; // 节流：至少 1 秒一次

        // 通过反射定位 Nautilus 的扩展方法与构造函数：
        // - Nautilus.Assets.Gadgets.GadgetExtensions.SetRecipe(ICustomPrefab, RecipeData)
        // - Nautilus.Assets.Gadgets.GadgetExtensions.SetRecipeFromJson(ICustomPrefab, string)
        // - Nautilus.Assets.Gadgets.CraftingGadget..ctor(ICustomPrefab, RecipeData)
        static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type tExtensions = null;
                Type tCraftingGadget = null;
                foreach (var asm in assemblies)
                {
                    try { tExtensions = tExtensions ?? asm.GetType("Nautilus.Assets.Gadgets.GadgetExtensions"); } catch { }
                    try { tCraftingGadget = tCraftingGadget ?? asm.GetType("Nautilus.Assets.Gadgets.CraftingGadget"); } catch { }
                }

                if (tExtensions != null)
                {
                    foreach (var m in tExtensions.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var name = m.Name;
                        if (string.Equals(name, "SetRecipe", StringComparison.Ordinal) ||
                            string.Equals(name, "SetRecipeFromJson", StringComparison.Ordinal))
                        {
                            methods.Add(m);
                        }
                    }
                }

                if (tCraftingGadget != null)
                {
                    foreach (var ctor in tCraftingGadget.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        // 构造函数也可能在某些路径上被调用以注入配方
                        methods.Add(ctor);
                    }
                }
            }
            catch { }
            return methods;
        }

        static void Postfix()
        {
            try
            {
                var now = DateTime.UtcNow;
                if ((now - _lastBuildAtUtc).TotalMilliseconds < MinIntervalMs)
                    return;
                _lastBuildAtUtc = now;

                try { JustEnoughItems.Plugin.Log?.LogInfo("[Just Enough Items] Rebuild triggered by Nautilus recipe registration (throttled)"); } catch { }
                try { UnityEngine.Debug.Log("[Just Enough Items] Rebuild triggered by Nautilus recipe registration (throttled)"); } catch { }

                try
                {
                    JustEnoughItems.JeiDataStore.Invalidate();
                    JustEnoughItems.JeiDataStore.BuildIfNeeded();
                    var snap = JustEnoughItems.JeiDataStore.Snapshot();
                    int cnt = snap?.Count ?? 0;
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"[Just Enough Items] DataStore items after Nautilus trigger={cnt}"); } catch { }
                    try { UnityEngine.Debug.Log($"[Just Enough Items] DataStore items after Nautilus trigger={cnt}"); } catch { }
                }
                catch (Exception ex)
                {
                    try { UnityEngine.Debug.LogWarning($"[Just Enough Items] Rebuild on Nautilus hook failed: {ex.Message}"); } catch { }
                }
            }
            catch { }
        }
    }
}
