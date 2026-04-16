using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace JustEnoughItems.Patches
{
    [HarmonyPatch]
    internal static class TooltipAppend
    {
        // 动态定位 uGUI_Tooltip.Show 的所有重载
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var tt = AccessTools.TypeByName("uGUI_Tooltip");
            if (tt == null) yield break;
            foreach (var mi in tt.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mi.Name == "Show")
                    yield return mi;
            }
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                // 仅在我们识别到 Hover TechType 时追加
                var hovered = UI.HoverContext.GetHoveredTechType();
                if (string.IsNullOrEmpty(hovered)) return;

                // 取按键显示：优先 GameInput.FormatButton(JeiButtonEnum,false)，失败则回退到配置键/J
                string keyText = null;
                try
                {
                    if (Plugin.JeiButtonEnum != null && Plugin.JeiButtonEnumType != null)
                    {
                        var giType = AccessTools.TypeByName("GameInput");
                        var miFormat = giType?.GetMethod("FormatButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { Plugin.JeiButtonEnumType, typeof(bool) }, null);
                        if (miFormat != null)
                        {
                            keyText = (string)miFormat.Invoke(null, new object[] { Plugin.JeiButtonEnum, false });
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(keyText))
                {
                    try { keyText = Plugin.OpenKey.Value.ToString(); } catch { keyText = "J"; }
                }
                var appendLine = $"打开百科 - [{keyText}]";

                // 查找文本组件字段并追加
                var inst = __instance as Component;
                if (inst == null) return;
                var t = inst.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // 常见字段名尝试
                var fieldNames = new[] { "text", "label", "labelText" };
                foreach (var fn in fieldNames)
                {
                    var fi = t.GetField(fn, flags);
                    if (fi == null) continue;
                    var val = fi.GetValue(inst);
                    if (val == null) continue;

                    // UnityEngine.UI.Text
                    var textType = AccessTools.TypeByName("UnityEngine.UI.Text");
                    if (textType != null && textType.IsInstanceOfType(val))
                    {
                        var prop = textType.GetProperty("text");
                        var old = prop?.GetValue(val) as string ?? string.Empty;
                        if (!old.Contains(appendLine))
                            prop?.SetValue(val, string.IsNullOrEmpty(old) ? appendLine : (old + "\n" + appendLine));
                        return;
                    }

                    // TMPro.TextMeshProUGUI
                    var tmpType = AccessTools.TypeByName("TMPro.TextMeshProUGUI");
                    if (tmpType != null && tmpType.IsInstanceOfType(val))
                    {
                        var prop = tmpType.GetProperty("text");
                        var old = prop?.GetValue(val) as string ?? string.Empty;
                        if (!old.Contains(appendLine))
                            prop?.SetValue(val, string.IsNullOrEmpty(old) ? appendLine : (old + "\n" + appendLine));
                        return;
                    }
                }

                // 回退：向下查找子节点上的文本组件
                try
                {
                    var go = inst.gameObject;
                    if (go != null)
                    {
                        var textType = AccessTools.TypeByName("UnityEngine.UI.Text");
                        var tmpType = AccessTools.TypeByName("TMPro.TextMeshProUGUI");
                        if (textType != null)
                        {
                            var getCompInChildren = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .FirstOrDefault(m => m.Name == "GetComponentInChildren" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
                            if (getCompInChildren != null)
                            {
                                var gm = getCompInChildren.MakeGenericMethod(textType);
                                var comp = gm.Invoke(go, new object[] { true, false });
                                if (comp != null)
                                {
                                    var prop = textType.GetProperty("text");
                                    var old = prop?.GetValue(comp) as string ?? string.Empty;
                                    if (!old.Contains(appendLine))
                                        prop?.SetValue(comp, string.IsNullOrEmpty(old) ? appendLine : (old + "\n" + appendLine));
                                    return;
                                }
                            }
                        }
                        if (tmpType != null)
                        {
                            var getCompInChildren = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .FirstOrDefault(m => m.Name == "GetComponentInChildren" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
                            if (getCompInChildren != null)
                            {
                                var gm = getCompInChildren.MakeGenericMethod(tmpType);
                                var comp = gm.Invoke(go, new object[] { true, false });
                                if (comp != null)
                                {
                                    var prop = tmpType.GetProperty("text");
                                    var old = prop?.GetValue(comp) as string ?? string.Empty;
                                    if (!old.Contains(appendLine))
                                        prop?.SetValue(comp, string.IsNullOrEmpty(old) ? appendLine : (old + "\n" + appendLine));
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Plugin.Log?.LogDebug($"[TooltipAppend] fallback search failed: {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[TooltipAppend] Postfix error: {ex.Message}");
            }
        }
    }
}
