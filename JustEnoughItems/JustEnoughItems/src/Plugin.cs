using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JustEnoughItems;


namespace JustEnoughItems
{
    [BepInPlugin("com.tichunyuanyan.subnautica.jei", "JustEnoughItems", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        // 旧版：不使用 Nautilus 选项与 Mod Input
        internal static object JeiButtonEnum; // 保留占位，始终为 null
        internal static System.Type JeiButtonEnumType; // 保留占位，始终为 null
        internal static ConfigEntry<KeyCode> OpenKey;
        internal static ConfigEntry<bool> DebugAutoOpen;
        internal static readonly Dictionary<string, string> NameCache = new Dictionary<string, string>(StringComparer.Ordinal);

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony("com.yourname.subnautica.jei");

            // 启用 JSON 配置加载（仅通过 JSON 提供显示名与数据）
            JustEnoughItems.Config.ConfigService.Initialize();

            // Fallback hotkey via BepInEx config
            OpenKey = Config.Bind("Hotkeys", "OpenJEI", KeyCode.J, "Key to open the JEI window (fallback if Nautilus options are unavailable)");
            DebugAutoOpen = Config.Bind("Debug", "AutoOpenOnce", false, "If true, JEI will auto-open once 3 seconds after load to verify UI rendering");

            StartCoroutine(DeferredInit());

            // PatchAll moved to DeferredInit after game assemblies are ready

            var go = new GameObject("JEI_Bootstrap");
            go.AddComponent<UI.JeiManager>();
            // 移除旧的蓝图拦截与自定义 PDA 页签逻辑
            DontDestroyOnLoad(go);

            // 预加载图标缓存（外部 icons 优先，内置 Resources 作为回退）
            try { IconCache.InitializeOnce(); Log.LogInfo("JEI IconCache initialized"); } catch { }

            Log.LogInfo("JustEnoughItems initialized");
            // ensure config file is written to disk immediately
            try { Config.Save(); } catch { }
        }

        private IEnumerator RebuildLater(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds <= 0f ? 2f : delaySeconds);
            try
            {
                JustEnoughItems.JeiDataStore.Invalidate();
                JustEnoughItems.JeiDataStore.BuildIfNeeded();
                var snap = JustEnoughItems.JeiDataStore.Snapshot();
                int cnt = snap?.Count ?? 0;
                Logger.LogInfo($"[JEI][Boot] RebuildLater after {delaySeconds:0.0}s -> items={cnt}");
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"[JEI][Boot] RebuildLater failed: {ex.Message}");
            }
        }

        private IEnumerator DeferredInit()
        {
            // 等待一会儿，确保游戏 UI 类型加载完成
            yield return null;
            yield return null;
            // 旧版：不注册 Nautilus 选项或 Mod Input

            // 基于 categories.json / items.json 扩展枚举并注册物品到组/分类
            // 停用基于 items.json 的枚举扩展与注册
            // try
            // {
            //     JustEnoughItems.Integration.EnumRegistry.RegisterFromConfig();
            // }
            // catch (Exception ex)
            // {
            //     Log.LogWarning($"EnumRegistry.RegisterFromConfig failed: {ex.Message}");
            // }

            // 不再注册 JEI 自定义 PDA 页签

            // wait for game UI types to be loaded (no try/catch around yields)
            float waited = 0f;
            while (AccessTools.TypeByName("uGUI_PDA") == null && AccessTools.TypeByName("uGUI_BlueprintsTab") == null && waited < 10f)
            {
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }
            Log.LogInfo($"Deferred patching after wait={waited:0.0}s; uGUI_PDA={(AccessTools.TypeByName("uGUI_PDA") != null)} uGUI_BlueprintsTab={(AccessTools.TypeByName("uGUI_BlueprintsTab") != null)}");

            // 注册蓝图页内嵌 JEI 的最小补丁（Open/Close 生命周期）与 TooltipFactory.ItemActions 追加提示
            try
            {
                Harmony.PatchAll(typeof(JustEnoughItems.Patches.BlueprintsEmbed));
                Log.LogInfo("Harmony patched BlueprintsEmbed");
                Harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemActionsAppend));
                Harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemCommonsHotkey));
                Log.LogInfo("Harmony patched TooltipItemActionsAppend");
                // 追加：Nautilus 配方注册钩子（替代旧版 SMLHelper 触发点）
                Harmony.PatchAll(typeof(JustEnoughItems.Patches.NautilusRecipeHooks));
                Log.LogInfo("Harmony patched NautilusRecipeHooks");
                // 延迟一次数据重建，避免在 Nautilus 注册完成前扫描为空
                try { StartCoroutine(RebuildLater(2f)); } catch { }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Deferred Harmony patch failed: {ex}");
            }

            // 一次性等待语言系统就绪，避免在 Language.main 为空时进行名称解析
            {
                float waitedLang = 0f;
                System.Type tLanguage = null;
                System.Reflection.PropertyInfo pMain = null;
                object lang = null;
                try { tLanguage = AccessTools.TypeByName("Language"); } catch { }
                try { pMain = tLanguage?.GetProperty("main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); } catch { }
                try { lang = pMain?.GetValue(null, null); } catch { }
                while (lang == null && waitedLang < 10f)
                {
                    yield return new WaitForSeconds(0.2f);
                    waitedLang += 0.2f;
                    try { tLanguage = AccessTools.TypeByName("Language"); } catch { }
                    try { pMain = tLanguage?.GetProperty("main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); } catch { }
                    try { lang = pMain?.GetValue(null, null); } catch { }
                }
                try { Log.LogInfo($"Language readiness wait={waitedLang:0.0}s; ready={(lang!=null)}"); } catch { }
            }

            // 不使用国际化语言设置
            if (DebugAutoOpen != null && DebugAutoOpen.Value && Debug.isDebugBuild)
            {
                Logger.LogInfo("DebugAutoOpen: will auto-open JEI in 3 seconds");
                yield return new WaitForSeconds(3f);
                try
                {
                    UI.JeiManager.Toggle();
                    Logger.LogInfo("DebugAutoOpen: toggled JEI once");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"DebugAutoOpen failed: {ex.Message}");
                }
            }
        }

        // 不提供 Nautilus Mod Input 注册（旧版不需要）
    }
}
