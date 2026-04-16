using BepInEx;
using System;
using System.IO;
using Newtonsoft.Json;


namespace JustEnoughItems.Config
{
    public static class ConfigService
    {
        public static JeiConfig Current { get; private set; } = new JeiConfig();
        private static readonly string ConfigDir = Path.Combine(Paths.ConfigPath, "JustEnoughItems"); // legacy (unused)
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "jei.json");             // legacy (unused)

        // Layout under <GameRoot>/BepInEx/plugins/JustEnoughItems/AssetBundles (BepInEx + Nautilus)
        private static readonly string GameRoot = (AppDomain.CurrentDomain.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "");
        private static readonly string PluginsBaseDir = Path.Combine(Paths.PluginPath, "JustEnoughItems");
        private static readonly string AssetBundlesDir = Path.Combine(PluginsBaseDir, "AssetBundles");
        private static readonly string NewJsonDir = Path.Combine(AssetBundlesDir, "json");
        private static readonly string NewIconsDir = Path.Combine(AssetBundlesDir, "icons");

        // Effective paths used at runtime
        private static string _effectiveConfigPath;
        private const string ItemsFileName = "jei-supplement.json";
        private const string NamesFileName = "jei-names.json"; // 新增：独立中文名称映射文件

        // 物品中文名映射（ItemId -> ChineseName），仅从独立 JSON 加载
        public static readonly System.Collections.Generic.Dictionary<string, string> ChineseNames = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        public static string ConfigDirectory => NewJsonDir; // for reference only
        public static string ConfigFilePath => _effectiveConfigPath ?? Path.Combine(NewJsonDir, ItemsFileName);
        public static string NewConfigJsonDirectory => NewJsonDir;
        public static string NewIconsDirectory => NewIconsDir;
        public static string PluginsAssetBundlesDirectory => AssetBundlesDir;

        public static void Initialize()
        {
            Directory.CreateDirectory(NewJsonDir);
            try { UnityEngine.Debug.Log($"[JEI][Boot] GameRoot='{GameRoot}' pluginsBase='{PluginsBaseDir}' jsonDir='{NewJsonDir}'"); } catch { }

            // Strict: only items.json is allowed; no legacy fallback
            var itemsPath = Path.Combine(NewJsonDir, ItemsFileName);
            _effectiveConfigPath = itemsPath;
            try { BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Config: expect='{_effectiveConfigPath}', exists={File.Exists(_effectiveConfigPath)}"); } catch { }
            if (!File.Exists(_effectiveConfigPath))
            {
                BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogError($"Missing required config file: {_effectiveConfigPath}. Mod will not load.");
                throw new FileNotFoundException($"JEI config not found: {_effectiveConfigPath}");
            }
            try
            {
                var json = ReadAllTextSmart(ConfigFilePath);
                Current = JsonConvert.DeserializeObject<JeiConfig>(json) ?? new JeiConfig();
                try
                {
                    int itemCount = Current?.Items?.Count ?? 0;
                    int usageTotal = 0; if (itemCount > 0) foreach (var it in Current.Items) usageTotal += (it?.Usage?.Count ?? 0);
                    BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Config: items={itemCount}, usageTabs={usageTotal}");
                }
                catch { }
            }
            catch (Exception)
            {
                Current = new JeiConfig();
            }

            // categories.json 已由其他模组管理，本模组不再读取

            // 加载独立中文名称映射（可选文件）
            try
            {
                var namesPath = System.IO.Path.Combine(NewJsonDir, NamesFileName);
                ChineseNames.Clear();
                if (System.IO.File.Exists(namesPath))
                {
                    var json2 = ReadAllTextSmart(namesPath);
                    var cfg = JsonConvert.DeserializeObject<NamesConfig>(json2) ?? new NamesConfig();
                    int add = 0;
                    foreach (var e in (cfg.Names ?? new System.Collections.Generic.List<NameEntry>()))
                    {
                        if (e == null) continue;
                        var id = (e.ItemId ?? string.Empty).Trim();
                        var name = (e.ChineseName ?? string.Empty).Trim();
                        if (id.Length == 0 || name.Length == 0) continue;
                        ChineseNames[id] = name; add++;
                    }
                    try { BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Names: loaded {add} entries"); } catch { }
                }
            }
            catch { }
        }

        public static void Reload()
        {
            try
            {
                // Strict: only items.json; if missing, throw
                var itemsPath = Path.Combine(NewJsonDir, ItemsFileName);
                _effectiveConfigPath = itemsPath;
                try { BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Config Reload: expect='{_effectiveConfigPath}', exists={File.Exists(_effectiveConfigPath)}"); } catch { }
                if (!File.Exists(_effectiveConfigPath))
                {
                    BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogError($"Missing required config file: {_effectiveConfigPath}. Mod will not load.");
                    throw new FileNotFoundException($"JEI config not found: {_effectiveConfigPath}");
                }
                var json = ReadAllTextSmart(ConfigFilePath);
                Current = JsonConvert.DeserializeObject<JeiConfig>(json) ?? new JeiConfig();
                try
                {
                    int itemCount = Current?.Items?.Count ?? 0;
                    int usageTotal = 0; if (itemCount > 0) foreach (var it in Current.Items) usageTotal += (it?.Usage?.Count ?? 0);
                    BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Config Reload: items={itemCount}, usageTabs={usageTotal}");
                }
                catch { }
            }
            catch (Exception)
            {
                Current = new JeiConfig();
            }
            // categories.json 已由其他模组管理，本模组不再读取

            // 重新加载独立中文名称映射
            try
            {
                var namesPath = System.IO.Path.Combine(NewJsonDir, NamesFileName);
                ChineseNames.Clear();
                if (System.IO.File.Exists(namesPath))
                {
                    var json2 = ReadAllTextSmart(namesPath);
                    var cfg = JsonConvert.DeserializeObject<NamesConfig>(json2) ?? new NamesConfig();
                    int add = 0;
                    foreach (var e in (cfg.Names ?? new System.Collections.Generic.List<NameEntry>()))
                    {
                        if (e == null) continue;
                        var id = (e.ItemId ?? string.Empty).Trim();
                        var name = (e.ChineseName ?? string.Empty).Trim();
                        if (id.Length == 0 || name.Length == 0) continue;
                        ChineseNames[id] = name; add++;
                    }
                    try { BepInEx.Logging.Logger.CreateLogSource("JustEnoughItems").LogInfo($"JEI Names Reload: loaded {add} entries"); } catch { }
                }
            }
            catch { }
        }

        // 运行期兜底：若中文名映射尚未就绪，则从 <jsonDir>/jei-names.json 载入一次
        public static void EnsureNamesLoaded()
        {
            try
            {
                if (ChineseNames != null && ChineseNames.Count > 0) return;
                var namesPath = System.IO.Path.Combine(NewJsonDir, NamesFileName);
                ChineseNames.Clear();
                if (System.IO.File.Exists(namesPath))
                {
                    var json2 = ReadAllTextSmart(namesPath);
                    var cfg = JsonConvert.DeserializeObject<NamesConfig>(json2) ?? new NamesConfig();
                    foreach (var e in (cfg.Names ?? new System.Collections.Generic.List<NameEntry>()))
                    {
                        if (e == null) continue;
                        var id = (e.ItemId ?? string.Empty).Trim();
                        var name = (e.ChineseName ?? string.Empty).Trim();
                        if (id.Length == 0 || name.Length == 0) continue;
                        ChineseNames[id] = name;
                    }
                }
            }
            catch { }
        }

        // No legacy resolution anymore

        

        private static class Templates
        {
            public static string DefaultJson =
                "{\n" +
                "  \"Items\": [\n" +
                "    {\n" +
                "      \"ItemId\": \"TiIngot\",\n" +
                "      \"DisplayName\": \"钛锭\",\n" +
                "      \"Description\": \"示例：钛锭的来源与用途\",\n" +
                "      \"Source\": [\n" +
                "        { \"IfFabricator\": true,  \"Fabricator\": \"Fabricator1\", \"Ingredient\": [\"diamond\", \"diamond\", \"Titanium\", \"Silver\"], \"Text\": \"下方显示自定义的文本\" } ,\n" +
                "        { \"IfFabricator\": false, \"Image\": \"image1.png\", \"Text\": \"下方显示自定义的文本\" }\n" +
                "      ],\n" +
                "      \"Usage\": [\n" +
                "        { \"IfFabricator\": true,  \"Fabricator\": \"Fabricator2\", \"Ingredient\": [\"Silver\", \"Silver\", \"Gear\"], \"Target\": \"EXTiIngot\",  \"Text\": \"下方显示自定义的文本\" },\n" +
                "        { \"IfFabricator\": false, \"Image\": \"image1.png\", \"Target\": \"EXTiIngot1\", \"Text\": \"下方显示自定义的文本\" }\n" +
                "      ]\n" +
                "    }\n" +
                "  ]\n" +
                "}";
        }

        private static string ReadAllTextSmart(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // UTF-8 (with/without BOM)
                string utf8 = new System.Text.UTF8Encoding(false, false).GetString(bytes);
                if (LooksMojibake(utf8))
                {
                    // fallback to system default (e.g., GBK/936)
                    try { return System.Text.Encoding.Default.GetString(bytes); } catch { }
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

    // 独立中文名称配置模型（从 jei-names.json 读取）
    public class NamesConfig
    {
        [JsonProperty("Names")] public System.Collections.Generic.List<NameEntry> Names { get; set; } = new System.Collections.Generic.List<NameEntry>();
    }
    public class NameEntry
    {
        public string ItemId { get; set; } = string.Empty;
        [JsonProperty("ChineseName")] public string ChineseName { get; set; } = string.Empty;
    }
}
