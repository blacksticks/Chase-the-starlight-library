using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CannotCatchFishAlive
{
    [BepInPlugin("com.tichunyuanyan.cannotcatchfishalive", "can not catch fish alive", "1.0.0")]    
    public class Main : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony HarmonyInstance;

        private void Awake()
        {
            Log = Logger;
            Bootstrap.Init(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        private void OnDestroy()
        {
            try { Bootstrap.Shutdown(); } catch { }
        }
    }
}
