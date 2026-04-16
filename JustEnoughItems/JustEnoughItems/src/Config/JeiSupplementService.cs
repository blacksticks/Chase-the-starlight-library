using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace JustEnoughItems.Config
{
    // 读取补充 JSON：jei-supplement.json
    public static class JeiSupplementService
    {
        private static JeiConfig _cache;
        private static string _path;

        public static JeiConfig Current
        {
            get
            {
                if (_cache == null) Reload();
                return _cache ?? new JeiConfig();
            }
        }

        public static string ConfigPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_path)) return _path;
                try
                {
                    // 以 DLL 所在目录为基准：QMods/JustEnoughItems/AssetBundles/json/jei-supplement.json
                    var asmDir = Path.GetDirectoryName(typeof(JeiSupplementService).Assembly.Location);
                    var dir = Path.Combine(asmDir ?? string.Empty, "AssetBundles", "json");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "jei-supplement.json");
                }
                catch
                {
                    _path = "jei-supplement.json";
                }
                return _path;
            }
        }

        public static void Reload()
        {
            try
            {
                var path = ConfigPath;
                bool exists = File.Exists(path);
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Supplement: load path='{path}', exists={exists}"); } catch { }
                try { UnityEngine.Debug.Log($"[Just Enough Items] Supplement: asmDir='{Path.GetDirectoryName(typeof(JeiSupplementService).Assembly.Location)}', path='{path}', exists={exists}"); } catch { }
                if (!exists) { _cache = new JeiConfig(); return; }

                var json = ReadAllTextSmart(path);
                var cfg = JsonConvert.DeserializeObject<JeiConfig>(json) ?? new JeiConfig();
                _cache = cfg;
                int count = _cache?.Items?.Count ?? 0;
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Supplement: items count={count}"); } catch { }
                try { UnityEngine.Debug.Log($"[Just Enough Items] Supplement: items count={count}"); } catch { }
            }
            catch
            {
                _cache = new JeiConfig();
            }
        }

        private static string ReadAllTextSmart(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                string utf8 = new UTF8Encoding(false, false).GetString(bytes);
                if (LooksMojibake(utf8))
                {
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
