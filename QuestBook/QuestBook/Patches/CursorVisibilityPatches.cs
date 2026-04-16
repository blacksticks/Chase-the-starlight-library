using HarmonyLib;
using UnityEngine;

namespace QuestBook
{
    [HarmonyPatch]
    internal static class CursorVisibilityPatches
    {
        // 在 FPSInputModule.OnUpdate 之后强制保持光标可见与解锁，
        // 并禁止 alwaysLockCursor（仅当我们的 UI 打开时）。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FPSInputModule), nameof(FPSInputModule.OnUpdate))]
        private static void FPSInputModule_OnUpdate_Postfix()
        {
            if (!UIManager.IsOpen)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
