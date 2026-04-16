using HarmonyLib;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JustEnoughItems.Patches
{
    // 完全复刻示例的思路：在 TooltipFactory.ItemCommons 的 Postfix 中检测按键并打开 JEI
    [HarmonyPatch(typeof(TooltipFactory), "ItemCommons")]
    internal static class TooltipItemCommonsHotkey
    {
        private static MethodInfo _miGetButtonDown;

        [HarmonyPostfix]
        private static void Postfix(StringBuilder sb, TechType techType, GameObject obj)
        {
            try
            {
                // 仅当存在有效物品时处理；无悬浮物品时按键无效
                if (techType == TechType.None)
                    return;

                // 若 JEI 已打开，则按键应当关闭 JEI，而不是再次触发打开
                bool jeiVisible = false;
                try { jeiVisible = JustEnoughItems.UI.JeiManager.Visible; } catch { }

                // 仅在系统菜单打开时跳过；PDA 内允许使用热键
                bool inMenu = false;
                try { inMenu = IngameMenu.main != null && IngameMenu.main.selected; } catch { }
                if (inMenu)
                    return;

                // 1) 移除 Nautilus(Mod Input) 按键读取，仅保留配置键与 KeyCode.J
                bool pressedModInput = false;

                // 2) 回退：配置键 或 直接 J
                bool pressedConfig = false;
                try { if (JustEnoughItems.Plugin.OpenKey != null) pressedConfig = Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value); } catch { }
                bool pressedRawJ = Input.GetKeyDown(KeyCode.J);

                if (!(pressedConfig || pressedRawJ))
                    return;

                // 移除调试日志与游戏内弹窗

                if (jeiVisible)
                {
                    try { JustEnoughItems.UI.JeiManager.Hide(); } catch { }
                    return;
                }

                // 打开悬浮物品详情（调试：输出悬浮ID与传递ID）；传入裸 ID
                string id = techType.ToString();
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Debug: Hover TechType={techType}, PassingId={id}"); } catch { }
                try
                {
                    JustEnoughItems.UI.JeiManager.ShowForItem(id);
                }
                catch (Exception ex)
                {
                    JustEnoughItems.Plugin.Log?.LogError($"TooltipItemCommonsHotkey ShowForItem exception: {ex}");
                }
            }
            catch (Exception ex)
            {
                // 仅保留必要错误日志
            }
        }
    }
}
