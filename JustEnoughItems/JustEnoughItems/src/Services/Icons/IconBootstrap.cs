using System;

namespace JustEnoughItems
{
    internal static class IconBootstrap
    {
        private static bool _iconsPrimed;

        public static void EnsureIconsPrimedOnce()
        {
            if (_iconsPrimed && IconCache.IsReady()) return;
            try
            {
                IconCache.InitializeOnce();
                if (IconCache.IsReady())
                {
                    _iconsPrimed = true;
                }
            }
            catch { }
        }
    }
}
