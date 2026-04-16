using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class Log
    {
        public static void Debug(string msg)
        {
            if (CannotCatchFishAlive.Main.Log != null) CannotCatchFishAlive.Main.Log.LogDebug(msg);
            else UnityEngine.Debug.Log("[CCFA][DEBUG] " + msg);
        }
        public static void Info(string msg)
        {
            if (CannotCatchFishAlive.Main.Log != null) CannotCatchFishAlive.Main.Log.LogInfo(msg);
            else UnityEngine.Debug.Log("[CCFA][INFO] " + msg);
        }
        public static void Warn(string msg)
        {
            if (CannotCatchFishAlive.Main.Log != null) CannotCatchFishAlive.Main.Log.LogWarning(msg);
            else UnityEngine.Debug.LogWarning("[CCFA][WARN] " + msg);
        }
        public static void Error(string msg)
        {
            if (CannotCatchFishAlive.Main.Log != null) CannotCatchFishAlive.Main.Log.LogError(msg);
            else UnityEngine.Debug.LogError("[CCFA][ERROR] " + msg);
        }
    }
}
