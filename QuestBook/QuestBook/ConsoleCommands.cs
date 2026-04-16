using System;
using SMLHelper.V2.Handlers;

namespace QuestBook
{
    internal static class ConsoleCommands
    {
        private const string Command = "QuestBook";

        internal static void Register()
        {
            try
            {
                ConsoleCommandsHandler.Main.RegisterConsoleCommand(Command, typeof(ConsoleCommands), nameof(HandleCommand), new Type[] { typeof(string) });
                Mod.Log?.LogInfo($"Registered console command: {Command}");
            }
            catch (Exception e)
            {
                Mod.Log?.LogError($"Failed to register console command {Command}: {e}");
            }
        }

        public static string HandleCommand(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return Usage();
            }

            var token = action.Trim();
            switch (token.ToLowerInvariant())
            {
                case "edit":
                case "toggle":
                    DeveloperModeManager.Toggle();
                    return $"任务书开发者模式 = {(DeveloperModeManager.IsDeveloperMode ? "开启" : "关闭")}";

                case "on":
                case "enable":
                    DeveloperModeManager.Set(true);
                    return "任务书开发者模式 = 开启";

                case "off":
                case "disable":
                    DeveloperModeManager.Set(false);
                    return "任务书开发者模式 = OFF";

                case "status":
                    return $"任务书开发者模式 = {(DeveloperModeManager.IsDeveloperMode ? "ON" : "OFF")}, UI = {(UIManager.IsOpen ? "OPEN" : "CLOSED")}";

                case "ui":
                case "open":
                    UIManager.Open();
                    return "任务书UI = 已开启";

                case "close":
                    UIManager.Close();
                    return "任务书UI = 已关闭";

                case "uitoggle":
                    UIManager.Toggle();
                    return $"任务书UI = {(UIManager.IsOpen ? "开启" : "关闭")}";

                default:
                    return Usage();
            }
        }

        private static string Usage()
        {
            return "用法: 任务书 <edit|on|off|status|UI|close|uitoggle>";
        }
    }
}
