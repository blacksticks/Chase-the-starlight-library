using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace JustEnoughItems.Patches
{
    // 动态 Hook：SMLHelper.V2.Handlers.CraftDataHandler.SetTechData(TechType, TechData)
    [HarmonyPatch]
    internal static class SmlHelperSetTechDataHook
    {
        private static DateTime _lastBuildAtUtc = DateTime.MinValue;
        private const int MinIntervalMs = 1000; // 节流：至少 1 秒触发一次

        // 针对所有重载的 SetTechData 进行 Hook（避免在 try/catch 中使用 yield）
        static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            try
            {
                var tHandler = Type.GetType("SMLHelper.V2.Handlers.CraftDataHandler, SMLHelper", throwOnError: false);
                if (tHandler == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { tHandler = asm.GetType("SMLHelper.V2.Handlers.CraftDataHandler"); } catch { tHandler = null; }
                        if (tHandler != null) break;
                    }
                }
                if (tHandler == null) return methods;
                foreach (var m in tHandler.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (!string.Equals(m.Name, "SetTechData", StringComparison.Ordinal)) continue;
                    // 放宽：只要是静态 SetTechData 都挂上（不同版本/重载参数个数不同）
                    methods.Add(m);
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

                try { JustEnoughItems.Plugin.Log?.LogInfo("[Just Enough Items] Rebuild triggered by SMLHelper.SetTechData (throttled)"); } catch { }
                try { UnityEngine.Debug.Log("[Just Enough Items] Rebuild triggered by SMLHelper.SetTechData (throttled)"); } catch { }

                try
                {
                    JustEnoughItems.JeiDataStore.Invalidate();
                    JustEnoughItems.JeiDataStore.BuildIfNeeded();
                    var snap = JustEnoughItems.JeiDataStore.Snapshot();
                    int cnt = snap?.Count ?? 0;
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"[Just Enough Items] DataStore items after trigger={cnt}"); } catch { }
                    try { UnityEngine.Debug.Log($"[Just Enough Items] DataStore items after trigger={cnt}"); } catch { }
                }
                catch (Exception ex)
                {
                    try { UnityEngine.Debug.LogWarning($"[Just Enough Items] Rebuild on SMLHelper.SetTechData failed: {ex.Message}"); } catch { }
                }
            }
            catch { }
        }
    }
}
