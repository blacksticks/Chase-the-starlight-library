using HarmonyLib;
using System;
using UnityEngine;
using BELogger = BepInEx.Logging.Logger;
using System.IO;

namespace JustEnoughItems.QMod
{
    // QModManager v4: 通过 mod.json 的 EntryMethod 调用此方法，无需引用 QMM API
    public static class QModEntry
    {
        public static void Patch()
        {
            try
            {
                Debug.Log("[JEI] QModEntry.Patch() starting");
                // 确保 Plugin.Log 可用（在仅 QMod 入口时，BepInEx 插件未实例化会导致 Plugin.Log 为空）
                try { if (JustEnoughItems.Plugin.Log == null) JustEnoughItems.Plugin.Log = BELogger.CreateLogSource("JustEnoughItems.QMod"); } catch { }
                // Harmony 打补丁
                var harmony = new Harmony("com.yourname.subnautica.jei");
                try
                {
                    harmony.PatchAll(typeof(JustEnoughItems.Patches.BlueprintsEmbed));
                    harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemActionsAppend));
                    harmony.PatchAll(typeof(JustEnoughItems.Patches.TooltipItemCommonsHotkey));
                    harmony.PatchAll(typeof(JustEnoughItems.Patches.SmlHelperSetTechDataHook));
                }
                catch (Exception ex)
                {
                    Debug.Log("[JEI] Harmony patch error: " + ex);
                }

                // JEI 管理器
                if (GameObject.Find("JEI_Bootstrap") == null)
                {
                    var go = new GameObject("JEI_Bootstrap");
                    go.AddComponent<JustEnoughItems.UI.JeiManager>();
                    go.AddComponent<JustEnoughItems.UI.GlobalHotkeys>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
                Debug.Log("[JEI] QModEntry.Patch() finished");
            }
            catch (Exception ex)
            {
                Debug.Log("[JEI] QModEntry.Patch() fatal: " + ex);
            }
        }

        private static void SetupNewtonsoftResolver()
        {
            // disabled
        }

        private static System.Reflection.Assembly OnAssemblyResolveNewtonsoft(object sender, ResolveEventArgs args)
        {
            return null; // disabled
        }

        private static void TryPreloadNewtonsoft()
        {
            // disabled
        }

        private static string[] BuildNewtonsoftCandidatePaths()
        {
            return Array.Empty<string>();
        }
    }
}
