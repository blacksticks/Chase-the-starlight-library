using HarmonyLib;
using QModManager.API.ModLoading;
using System;
using System.Collections;
using UnityEngine;

namespace JustEnoughItems
{
    [QModCore]
    public static class QModBootstrap
    {
        [QModPatch]
        public static void Patch()
        {
            try
            {
                // 精简：不再安装解析器或预加载，保持最小化入口

                if (GameObject.Find("JEI_Bootstrap") != null)
                {
                    Debug.Log("JEI(QMod): detected existing JEI_Bootstrap; skip QModBootstrap");
                    return;
                }
                var host = new GameObject("JEI_QModEntrypoint");
                host.AddComponent<QModBootstrapBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(host);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JEI QModBootstrap Patch failed: {ex}");
            }
        }

        private static void SetupNewtonsoftResolver() { /* disabled */ }

        private static System.Reflection.Assembly OnAssemblyResolveNewtonsoft(object sender, ResolveEventArgs args) { return null; /* disabled */ }

        public static string SafeLocation(System.Reflection.Assembly a)
        {
            try { return a.Location; } catch { return "<no-location>"; }
        }

        private static void TryPreloadNewtonsoft() { /* disabled */ }

        private static string[] BuildNewtonsoftCandidatePaths() { return System.Array.Empty<string>(); }
    }

    internal class QModBootstrapBehaviour : MonoBehaviour
    {
        private Harmony _harmony;

        private void Start()
        {
            StartCoroutine(DeferredInit());
        }

        private IEnumerator DeferredInit()
        {
            yield return null;
            yield return null;

            float waited = 0f;
            while (AccessTools.TypeByName("uGUI_PDA") == null && AccessTools.TypeByName("uGUI_BlueprintsTab") == null && waited < 10f)
            {
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }
            Debug.Log($"JEI(QMod): deferred wait={waited:0.0}s; uGUI_PDA={(AccessTools.TypeByName("uGUI_PDA") != null)} uGUI_BlueprintsTab={(AccessTools.TypeByName("uGUI_BlueprintsTab") != null)}");

            if (GameObject.Find("JEI_Bootstrap") != null)
            {
                Debug.Log("JEI(QMod): found JEI_Bootstrap after wait; skip init");
                yield break;
            }

            try
            {
                _harmony = new Harmony("com.yourname.subnautica.jei.qmod");
                _harmony.PatchAll(typeof(JustEnoughItems.Patches.BlueprintsEmbed));
                _harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemActionsAppend));
                _harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemCommonsHotkey));
                _harmony.PatchAll(typeof(JustEnoughItems.Patches.SmlHelperSetTechDataHook));
                Debug.Log("JEI(QMod): Harmony patches applied");

                // 诊断：枚举当前已加载的 Newtonsoft.Json
                try
                {
                    var loaded = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var a in loaded)
                    {
                        try
                        {
                            var n = a.GetName();
                            if (n != null && string.Equals(n.Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.Log($"JEI(QMod): Loaded Newtonsoft.Json -> {a.FullName}; Location='{QModBootstrap.SafeLocation(a)}'");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"JEI(QMod): Harmony patch failed: {ex}");
            }

            try
            {
                var go = new GameObject("JEI_Bootstrap");
                go.AddComponent<UI.JeiManager>();
                go.AddComponent<UI.GlobalHotkeys>();
                DontDestroyOnLoad(go);
                Debug.Log("JEI(QMod): bootstrap objects created");
                // 启动延迟与重试构建 JEI 数据
                StartCoroutine(DeferredBuildJeiData());
            }
            catch (Exception ex)
            {
                Debug.LogError($"JEI(QMod): create bootstrap failed: {ex}");
            }
        }

        private IEnumerator DeferredBuildJeiData()
        {
            // 初始等待，让 SMLHelper/CraftData 完成 techData 填充
            try { JustEnoughItems.Plugin.Log?.LogInfo("[Just Enough Items] Deferred build: coroutine started"); } catch { }
            try { UnityEngine.Debug.Log("[Just Enough Items] Deferred build: coroutine started"); } catch { }
            // 给 SMLHelper / CraftData 更多初始化时间
            yield return new WaitForSeconds(5f);
            int attempts = 0;
            while (attempts < 10)
            {
                attempts++;
                try { UnityEngine.Debug.Log($"[Just Enough Items] Deferred build: attempt heartbeat #{attempts}"); } catch { }
                try
                {
                    // 探针：如果 CraftData.techData 仍为空，跳过本次构建
                    int techDataCount = TryGetCraftDataDictCount();
                    try { UnityEngine.Debug.Log($"[Just Enough Items] Deferred build probe: CraftData.techData count={techDataCount}"); } catch { }
                    if (techDataCount <= 0)
                    {
                        try { UnityEngine.Debug.Log("[Just Enough Items] Deferred build skipped: CraftData.techData is empty yet"); } catch { }
                    }
                    else
                    {
                        JustEnoughItems.JeiDataStore.Invalidate();
                        JustEnoughItems.JeiDataStore.BuildIfNeeded();
                        var snapshot = JustEnoughItems.JeiDataStore.Snapshot();
                        int cnt = snapshot?.Count ?? 0;
                        try { JustEnoughItems.Plugin.Log?.LogInfo($"[Just Enough Items] Deferred build attempt #{attempts}: DataStore items={cnt}"); } catch { }
                        try { UnityEngine.Debug.Log($"[Just Enough Items] Deferred build attempt #{attempts}: DataStore items={cnt}"); } catch { }
                        if (cnt > 0) yield break;
                    }
                }
                catch (Exception ex)
                {
                    try { UnityEngine.Debug.LogWarning($"[Just Enough Items] Deferred build error on attempt #{attempts}: {ex.Message}"); } catch { }
                }
                yield return new WaitForSeconds(2f);
            }
        }

        private static int TryGetCraftDataDictCount()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type craftData = null;
                foreach (var asm in assemblies)
                {
                    try { craftData = craftData ?? asm.GetType("CraftData"); } catch { }
                }
                if (craftData == null)
                {
                    foreach (var asm in assemblies)
                    {
                        try
                        {
                            foreach (var t in asm.GetTypes())
                            {
                                if (t != null && string.Equals(t.Name, "CraftData", StringComparison.Ordinal)) { craftData = t; break; }
                            }
                            if (craftData != null) break;
                        }
                        catch { }
                    }
                }
                if (craftData == null) return -1;
                var fi = craftData.GetField("techData", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                object dict = null;
                if (fi != null) { try { dict = fi.GetValue(null); } catch { } }
                if (dict == null) return 0;
                try
                {
                    var piCount = dict.GetType().GetProperty("Count", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (piCount != null)
                    {
                        var v = piCount.GetValue(dict);
                        if (v is int i) return i;
                    }
                }
                catch { }
                // 退化：尝试枚举几个元素估计是否非空
                try
                {
                    var ien = dict as System.Collections.IEnumerable;
                    if (ien != null)
                    {
                        int c = 0; foreach (var _ in ien) { c++; if (c > 0) break; }
                        return c;
                    }
                }
                catch { }
                return 0;
            }
            catch { }
            return -1;
        }
    }
}
