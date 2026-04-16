using JustEnoughItems.Config;
using System;

namespace JustEnoughItems.Integration
{
    internal static class EnumRegistry
    {
        private static bool _registered;

        public static void RegisterFromConfig()
        {
            if (_registered) return;
            try
            {
                // JEI 不再在本模组内创建/绑定 TechGroup 与 TechCategory。
                // 分类与绑定由其他模组负责；此处仅确保配置已加载，供 UI 消费。
                if (ConfigService.Current == null)
                {
                    Plugin.Log?.LogWarning("[EnumRegistry] Config not initialized; skipping.");
                }
                else
                {
                    Plugin.Log?.LogInfo("[EnumRegistry] Skipped enum registration; categories/groups handled by external mod.");
                }

                _registered = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[EnumRegistry] RegisterFromConfig error: {ex}");
            }
        }

        private static TechType ResolveTechType(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return TechType.None;
            try
            {
                // 支持 "TechType.X" 或 "X"
                var token = itemId.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)
                    ? itemId.Substring("TechType.".Length)
                    : itemId;
                TechType tt;
                if (Enum.TryParse<TechType>(token, true, out tt))
                    return tt;
            }
            catch { }
            return TechType.None;
        }
    }
}
