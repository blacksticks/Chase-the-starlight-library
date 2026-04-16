using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace JustEnoughItems.Config
{
    public class FabricatorOverride
    {
        public string Id { get; set; } = string.Empty;               // 工作台ID或任意标识
        public string DisplayName { get; set; } = string.Empty;       // 工作台显示名
        public string Icon { get; set; } = string.Empty;              // 图标：可为 icons/ 相对路径或 TechType 名称
        public List<string> IncludeItems { get; set; } = new List<string>(); // 归属该工作台的产物/物品ID（可带 TechType. 前缀）
    }

    public static class FabricatorOverridesService
    {
        private static List<FabricatorOverride> _cache;
        private static string _path;

        public static List<FabricatorOverride> Current
        {
            get
            {
                if (_cache == null) Reload();
                return _cache ?? new List<FabricatorOverride>();
            }
        }

        public static string ConfigPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_path)) return _path;
                try
                {
                    // 固定：QMods/JustEnoughItems/AssetBundles/json/jei-fabricators.json（以程序集所在目录为基准）
                    var asmDir = Path.GetDirectoryName(typeof(FabricatorOverridesService).Assembly.Location);
                    var dir = Path.Combine(asmDir ?? string.Empty, "AssetBundles", "json");
                    var primary = Path.Combine(dir, "jei-fabricators.json");
                    try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
                    _path = primary;
                }
                catch
                {
                    _path = "jei-fabricators.json";
                }
                return _path;
            }
        }

        public static void Reload()
        {
            try
            {
                var path = ConfigPath;
                if (!File.Exists(path)) { _cache = new List<FabricatorOverride>(); try { JustEnoughItems.Plugin.Log?.LogError($"JEI FabricatorOverrides: config not found at {path}"); } catch { } return; }
                var json = File.ReadAllText(path);
                var list = JsonConvert.DeserializeObject<List<FabricatorOverride>>(json) ?? new List<FabricatorOverride>();
                _cache = list;
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI FabricatorOverrides: loaded {(_cache?.Count ?? 0)} from {path}"); } catch { }
            }
            catch
            {
                _cache = new List<FabricatorOverride>();
            }
        }
    }
}
