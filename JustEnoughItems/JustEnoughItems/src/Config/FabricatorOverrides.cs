using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oculus.Newtonsoft.Json;

namespace JustEnoughItems.Config
{
    public class FabricatorOverride
    {
        public string Id { get; set; } = string.Empty;                 // 工作台ID（用于 TopFabricator 的图标与显示名解析）
        public string DisplayName { get; set; } = string.Empty;         // 可选，留空则使用游戏内本地化
        public string Icon { get; set; } = string.Empty;                // 可选：TechType 名或 PNG 相对路径
        public List<string> IncludeItems { get; set; } = new List<string>(); // 属于该工作台的产物ID（TechType 名）
    }

    public static class FabricatorOverridesService
    {
        private static List<FabricatorOverride> _cache;
        private static string _path;

        public static IReadOnlyList<FabricatorOverride> Current
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
                    // 默认放置：BepInEx/plugins/JustEnoughItems/AssetBundles/json/jei-fabricators.json
                    var dir = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "JustEnoughItems", "AssetBundles", "json");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _path = System.IO.Path.Combine(dir, "jei-fabricators.json");
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
                if (!File.Exists(path))
                {
                    _cache = new List<FabricatorOverride>();
                    return;
                }
                var json = ReadAllTextSmart(path);
                var list = JsonConvert.DeserializeObject<List<FabricatorOverride>>(json) ?? new List<FabricatorOverride>();
                _cache = list;
            }
            catch
            {
                _cache = new List<FabricatorOverride>();
            }
        }

        private static string ReadAllTextSmart(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // 优先按 UTF-8 解码（含/不含 BOM）
                string utf8 = new UTF8Encoding(false, false).GetString(bytes);
                if (LooksMojibake(utf8))
                {
                    // 回退到系统默认编码（如 GBK/936），以处理中文
                    try { return Encoding.Default.GetString(bytes); } catch { }
                }
                return utf8;
            }
            catch
            {
                return File.ReadAllText(path);
            }
        }

        private static bool LooksMojibake(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int total = Math.Min(2000, s.Length);
            int bad = 0;
            for (int i = 0; i < total; i++) if (s[i] == '\uFFFD') bad++;
            return total > 0 && bad * 10 > total;
        }
    }
}
