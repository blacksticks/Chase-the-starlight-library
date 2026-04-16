using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace JustEnoughItems.Patches
{
    // 针对蓝图相关的 Tooltip 生成：匹配 TooltipFactory 内名称包含 "Blueprint" 或 "Recipe" 且首参为 StringBuilder 的方法
    [HarmonyPatch]
    internal static class TooltipBlueprintActionsAppend
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var tf = AccessTools.TypeByName("TooltipFactory");
            if (tf == null) yield break;
            foreach (var m in tf.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                // 只拦截以 StringBuilder 为首参的静态构建函数，排除我们已单独处理的 ItemActions/ItemCommons
                var ps = m.GetParameters();
                if (ps.Length == 0) continue;
                if (ps[0].ParameterType != typeof(StringBuilder)) continue;
                if (m.Name == "ItemActions" || m.Name == "ItemCommons") continue;
                yield return m;
            }
        }

        [HarmonyPostfix]
        private static void Postfix([HarmonyArgument(0)] StringBuilder sb)
        {
            try
            {
                if (sb == null) return;
                // 避免重复追加（若其他补丁或本补丁已写入）
                var snapshot = sb.ToString();
                if (!string.IsNullOrEmpty(snapshot) && snapshot.IndexOf("转到JEI", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                // 按键文本：优先使用 GetBinding(PrimaryDevice, Button, Primary) 再回退 FormatButton，最后配置键
                string keyText = null;
                try
                {
                    if (JustEnoughItems.Plugin.JeiButtonEnum != null)
                    {
                        var giType = AccessTools.TypeByName("GameInput");
                        var deviceType = giType?.GetNestedType("Device", BindingFlags.Public | BindingFlags.NonPublic);
                        var bindingSetType = giType?.GetNestedType("BindingSet", BindingFlags.Public | BindingFlags.NonPublic);
                        var fiPrimaryDevice = giType?.GetField("PrimaryDevice", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        var primaryDevice = fiPrimaryDevice?.GetValue(null);
                        var miGetBinding = giType?.GetMethod("GetBinding", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                            new Type[] { deviceType, JustEnoughItems.Plugin.JeiButtonEnumType, bindingSetType }, null);
                        if (miGetBinding != null && primaryDevice != null && bindingSetType != null)
                        {
                            var bsPrimary = Enum.Parse(bindingSetType, "Primary");
                            keyText = miGetBinding.Invoke(null, new object[] { primaryDevice, JustEnoughItems.Plugin.JeiButtonEnum, bsPrimary }) as string;
                        }
                        if (string.IsNullOrEmpty(keyText))
                        {
                            var miFormat = giType?.GetMethod("FormatButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { JustEnoughItems.Plugin.JeiButtonEnumType, typeof(bool) }, null);
                            if (miFormat != null)
                                keyText = (string)miFormat.Invoke(null, new object[] { JustEnoughItems.Plugin.JeiButtonEnum, false });
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(keyText))
                {
                    try { keyText = JustEnoughItems.Plugin.OpenKey.Value.ToString(); } catch { keyText = "J"; }
                }

                // 规范化按键显示
                if (!string.IsNullOrEmpty(keyText))
                {
                    try
                    {
                        if (keyText.StartsWith("<") && keyText.Contains(">/"))
                        {
                            var idx = keyText.LastIndexOf('/') + 1;
                            if (idx > 0 && idx < keyText.Length)
                                keyText = keyText.Substring(idx);
                        }
                        if (keyText.StartsWith("Keyboard", StringComparison.OrdinalIgnoreCase))
                        {
                            keyText = keyText.Substring("Keyboard".Length);
                        }
                        if (keyText.Length == 1)
                        {
                            keyText = keyText.ToUpperInvariant();
                        }
                    }
                    catch { }
                }

                // 仅当存在 TooltipFactory.WriteAction 时，在动作区域追加
                var tfType2 = AccessTools.TypeByName("TooltipFactory");
                var miWriteAction2 = tfType2?.GetMethod("WriteAction", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(StringBuilder), typeof(string), typeof(string) }, null);
                if (miWriteAction2 == null) return;
                miWriteAction2.Invoke(null, new object[] { sb, keyText, "转到JEI" });
            }
            catch (Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogWarning($"[TooltipBlueprintActionsAppend] Postfix error: {ex.Message}");
            }
        }
    }
}
