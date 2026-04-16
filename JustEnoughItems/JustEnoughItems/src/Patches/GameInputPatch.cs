using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace JustEnoughItems.Patches
{
    // 动态挂在稳定存在的 Update 循环上（Player.Update / uGUI_PDA.Update / IngameMenu.Update），保证每帧都能检测到按键
    [HarmonyPatch]
    internal static class GameInputPatch
    {
        [HarmonyTargetMethods]
        static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            var candidates = new[] { "Player", "uGUI_PDA", "IngameMenu" };
            foreach (var typeName in candidates)
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) continue;
                var m = AccessTools.Method(t, "Update", new Type[0]);
                if (m != null)
                    yield return m;
            }
        }
        private static bool _inPostfix;

        [HarmonyPostfix]
        static void Postfix()
        {
            if (_inPostfix) return; // 防重入保护
            _inPostfix = true;
            try
            {
                bool pressedConfig = false;
                try { pressedConfig = Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value); } catch { }
                bool pressedRawJ = Input.GetKeyDown(KeyCode.J);

                if (pressedConfig || pressedRawJ)
                {
                    try
                    {
                        if (pressedConfig) JustEnoughItems.Plugin.Log?.LogInfo("GameInputPatch: Open key via Config Key");
                        else if (pressedRawJ) JustEnoughItems.Plugin.Log?.LogInfo("GameInputPatch: Open key via KeyCode.J");
                    }
                    catch { }



                    // 优先 HoverContext
                    var hovered = UI.HoverContext.GetHoveredTechType();
                    if (!string.IsNullOrEmpty(hovered))
                    {
                        UI.JeiManager.ShowForItem(hovered);
                        return;
                    }

                    // 兜底：切换 JEI 面板
                    try { UI.JeiManager.Toggle(); } catch { }
                }
            }
            catch (Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogWarning($"GameInputPatch Postfix error: {ex}");
            }
            finally
            {
                _inPostfix = false;
            }
        }
    }
}
