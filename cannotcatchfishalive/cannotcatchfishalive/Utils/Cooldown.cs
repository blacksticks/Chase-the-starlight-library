using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class Cooldown
    {
        private static float last;
        private const float interval = 300f;

        public static bool InCooldown()
        {
            if (last <= 0f) return false;
            return (Time.realtimeSinceStartup - last) < interval;
        }

        public static void Mark()
        {
            last = Time.realtimeSinceStartup;
        }
    }
}
