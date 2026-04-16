using System.IO;
using System.Reflection;
using HarmonyLib;

namespace CannotCatchFishAlive
{
    internal static class Bootstrap
    {
        private static bool _inited;
        internal static Harmony HarmonyInstance;

        public static void Init(string baseDir = null)
        {
            if (_inited) return;
            _inited = true;
            Utils.Log.Info("[CCFA] Bootstrap.Init starting...");
            HarmonyInstance = new Harmony("com.tichunyuanyan.cannotcatchfishalive");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            var dir = baseDir ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Managers.WhitelistManager.Initialize(dir);
            Managers.DeadPickupRegistry.Initialize(dir);
            Utils.Log.Info("[CCFA] Bootstrap.Init done. Patches applied and managers initialized.");
        }

        public static void Shutdown()
        {
            Utils.Log.Info("[CCFA] Bootstrap.Shutdown...");
            try { Managers.WhitelistManager.Dispose(); } catch { }
            try { Managers.DeadPickupRegistry.Dispose(); } catch { }
            try { HarmonyInstance?.UnpatchSelf(); } catch { }
            _inited = false;
        }
    }
}
