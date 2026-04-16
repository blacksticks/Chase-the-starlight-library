using System;
using BepInEx.Logging;
using HarmonyLib;
using QuestBook.Config;
using QuestBook.Models;

namespace QuestBook
{
    internal static class Mod
    {
        private static bool _initialized;
        private static Harmony _harmony;
        internal static ManualLogSource Log;

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Log = Logger.CreateLogSource(PluginInfo.Guid);
            _harmony = new Harmony(PluginInfo.Guid);
            _harmony.PatchAll();
            ConsoleCommands.Register();
            QuestRepository.Load();
            if (QuestRepository.Current != null)
            {
                UIManager.SetData(QuestRepository.Current);
            }
            // 载入用户数据（持久化）：章节自定义图标/背景、创建的章节元信息、任务完成等
            var userData = UserDataRepository.Load();
            UIManager.SetUserData(userData);
        }
    }
}
