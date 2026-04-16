using HarmonyLib;
using UnityEngine;

namespace QuestBook
{
    [HarmonyPatch]
    internal static class FPSInputModulePatches
    {
        // 当 UI 打开时，强制使用真实鼠标位置，而不是凝视/屏幕中心，确保 UI 可点且能触发右键
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FPSInputModule), "GetCursorScreenPosition")]
        private static bool FPSInputModule_GetCursorScreenPosition_Prefix(ref Vector2 __result)
        {
            if (!UIManager.IsOpen)
                return true; // 走原逻辑

            __result = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            return false; // 跳过原逻辑
        }
    }
}
