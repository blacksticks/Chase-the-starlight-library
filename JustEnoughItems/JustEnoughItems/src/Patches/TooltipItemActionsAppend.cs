using HarmonyLib;
using System;
using System.Reflection;
using System.Text;

namespace JustEnoughItems.Patches
{
    [HarmonyPatch(typeof(TooltipFactory), "ItemActions")]
    internal static class TooltipItemActionsAppend
    {
        [HarmonyPostfix]
        private static void Postfix(StringBuilder sb, InventoryItem item)
        {
            try
            {
                if (sb == null) return;
                // 仅当存在物品时追加
                if (item == null) return;

                // 防重复：若已存在文本“转到JEI”则不再追加
                var snapshot = sb.ToString();
                if (!string.IsNullOrEmpty(snapshot) && snapshot.IndexOf("转到JEI界面", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                // 生成按键显示文本：优先使用 Mod Input GetBinding，然后 FormatButton，最后回退配置键
                string keyText = null;
                try
                {
                    if (JustEnoughItems.Plugin.JeiButtonEnum != null)
                    {
                        var giType = AccessTools.TypeByName("GameInput");
                        // 尝试 GetBinding(PrimaryDevice, Button, BindingSet.Primary)
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
                        // 回退：FormatButton(Button, false)
                        if (string.IsNullOrEmpty(keyText))
                        {
                            var miFormat = giType?.GetMethod("FormatButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { JustEnoughItems.Plugin.JeiButtonEnumType, typeof(bool) }, null);
                            if (miFormat != null)
                            {
                                keyText = (string)miFormat.Invoke(null, new object[] { JustEnoughItems.Plugin.JeiButtonEnum, false });
                            }
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(keyText))
                {
                    try { keyText = Plugin.OpenKey.Value.ToString(); } catch { keyText = "J"; }
                }

                // 规范化按键显示：
                // - 将 "<Keyboard>/j" -> "J"
                // - 将 "KeyboardJ" -> "J"
                // - 单字符转为大写
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

                // 固定动作文本（不做国际化）
                string actionText = "转到JEI界面";

                // 仅当存在 TooltipFactory.WriteAction 时，在系统动作区域追加
                var tfType = typeof(TooltipFactory);
                var miWriteAction = tfType.GetMethod("WriteAction", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(StringBuilder), typeof(string), typeof(string) }, null);
                if (miWriteAction == null) return;
                miWriteAction.Invoke(null, new object[] { sb, keyText, actionText });
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[TooltipItemActionsAppend] Postfix error: {ex.Message}");
            }
        }
    }
}
