using HarmonyLib;
using UnityEngine;

namespace QuestBook.Patches
{
    [HarmonyPatch]
    internal static class AvatarInputHandlerPatches2
    {
        // 跳过在启用/更新时把系统光标再次锁定
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AvatarInputHandler), "OnEnable")]
        private static bool AvatarInputHandler_OnEnable_Prefix()
        {
            if (UIManager.IsOpen)
            {
                return false; // 跳过原逻辑（避免 Utils.lockCursor = true）
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AvatarInputHandler), "Update")]
        private static bool AvatarInputHandler_Update_Prefix()
        {
            if (UIManager.IsOpen)
            {
                return false; // 跳过原逻辑（避免在点击等情况下重新锁定光标）
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class FPSInputModulePatches2
    {
        // UI 打开时，彻底跳过 FPSInputModule 的 OnUpdate，避免其修改 alwaysLockCursor 或接管输入
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FPSInputModule), nameof(FPSInputModule.OnUpdate))]
        private static bool FPSInputModule_OnUpdate_Prefix()
        {
            if (UIManager.IsOpen)
            {
                // 保险：即便存在其它实例也确保不要强制锁光标
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class GameInputPatches2
    {
        // UI 打开时，主输入设备一律视为键鼠，避免其它系统基于手柄判断去锁定光标
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.GetPrimaryDevice))]
        private static bool GameInput_GetPrimaryDevice_Prefix(ref GameInput.Device __result)
        {
            if (UIManager.IsOpen)
            {
                __result = GameInput.Device.Keyboard;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.IsPrimaryDeviceGamepad))]
        private static bool GameInput_IsPrimaryDeviceGamepad_Prefix(ref bool __result)
        {
            if (UIManager.IsOpen)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
