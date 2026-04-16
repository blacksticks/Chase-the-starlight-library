using System;

namespace QuestBook
{
    internal static class DeveloperModeManager
    {
        internal static bool IsDeveloperMode { get; private set; }
        internal static event Action<bool> DeveloperModeChanged;

        internal static void Set(bool enable)
        {
            if (IsDeveloperMode == enable) return;
            IsDeveloperMode = enable;
            Mod.Log?.LogInfo($"开发者模式: {(IsDeveloperMode ? "开启" : "关闭")}");
            DeveloperModeChanged?.Invoke(IsDeveloperMode);
        }

        internal static void Toggle()
        {
            Set(!IsDeveloperMode);
        }
    }
}
