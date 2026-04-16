using HarmonyLib;

namespace QuestBook
{
    [HarmonyPatch]
    internal static class CameraPatches
    {
        // 禁用摄像机在 UI 打开时的 Update，以阻止鼠标驱动视角
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MainCameraControl), "Update")]
        private static bool MainCameraControl_Update_Prefix()
        {
            // 当 UI 打开时，跳过原方法
            return !UIManager.IsOpen;
        }
    }
}
