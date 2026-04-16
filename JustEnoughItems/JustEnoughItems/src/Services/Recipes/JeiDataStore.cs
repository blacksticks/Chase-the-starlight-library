using JustEnoughItems.Config;
using System;
using System.Collections.Generic;

namespace JustEnoughItems
{
    public static class JeiDataStore
    {
        private static Dictionary<string, JeiItem> _cache;
        private static bool _built;

        public static void BuildIfNeeded()
        {
            if (_built && _cache != null) return;
            try
            {
                try { JustEnoughItems.Plugin.Log?.LogInfo("[Just Enough Items] DataStore.BuildIfNeeded: start"); } catch { }
                try { UnityEngine.Debug.Log("[Just Enough Items] DataStore.BuildIfNeeded: start"); } catch { }
                // 1) 扫描（失败时降级为空扫描，但不中断 JSON 构建）
                ScanResult scan = null;
                try
                {
                    var scanner = new RecipeScanner();
                    scan = scanner.ScanAll();
                }
                catch (Exception ex)
                {
                    try { JustEnoughItems.Plugin.Log?.LogWarning($"[Just Enough Items] Recipe scan failed, fallback to JSON only: {ex.Message}"); } catch { }
                    scan = new ScanResult { Recipes = new List<RecipeEntry>() };
                }

                // 2) 刷新并读取 JSON（唯一文件名：jei-supplement.json）
                try { JustEnoughItems.Config.JeiSupplementService.Reload(); } catch { }
                var merger = new RecipeMerger();
                var merged = merger.Merge(scan, JustEnoughItems.Config.JeiSupplementService.Current);
                var builder = new JeiModelBuilder();
                _cache = builder.Build(merged);
                // 诊断：缓存统计与首批键
                int cacheCount = _cache?.Count ?? 0;
                var keys = new System.Text.StringBuilder();
                try
                {
                    int i = 0;
                    System.Collections.Generic.IEnumerable<string> keyEnum = (_cache != null)
                        ? (System.Collections.Generic.IEnumerable<string>)_cache.Keys
                        : (System.Collections.Generic.IEnumerable<string>)new System.Collections.Generic.List<string>();
                    foreach (var k in keyEnum) { keys.Append(k).Append(','); if (++i >= 5) break; }
                }
                catch { }
                try { JustEnoughItems.Plugin.Log?.LogInfo($"[JEI][Store] DataStore items count={cacheCount}, FirstKeys=[{keys.ToString().TrimEnd(',')}] (json.items={merged?.JsonConfig?.Items?.Count ?? 0}, scanned={scan?.Recipes?.Count ?? 0})"); } catch { }
                try { UnityEngine.Debug.Log($"[JEI][Store] DataStore items count={cacheCount}, FirstKeys=[{keys.ToString().TrimEnd(',')}] (json.items={merged?.JsonConfig?.Items?.Count ?? 0}, scanned={scan?.Recipes?.Count ?? 0})"); } catch { }
                // 探针：常见ID命中情况（用于定位“扫描成功但未入库”的问题）
                try
                {
                    string[] probes = new[] { "Flashlight", "Titanium", "ScrapMetal" };
                    foreach (var pid in probes)
                    {
                        var norm = pid?.Trim();
                        bool ok = _cache != null && _cache.ContainsKey(norm);
                        UnityEngine.Debug.Log($"[JEI][Store] Probe idRaw='{pid}', idNorm='{norm}', found={ok}");
                    }
                }
                catch { }
                _built = true;
            }
            catch (Exception ex)
            {
                _cache = new Dictionary<string, JeiItem>(StringComparer.OrdinalIgnoreCase);
                _built = true;
                try { JustEnoughItems.Plugin.Log?.LogWarning($"[Just Enough Items] DataStore.BuildIfNeeded failed: {ex.Message}"); } catch { }
                try { UnityEngine.Debug.LogWarning($"[Just Enough Items] DataStore.BuildIfNeeded failed: {ex.Message}"); } catch { }
            }
        }

        public static void Invalidate()
        {
            _built = false;
            _cache = null;
        }

        public static bool TryGetItem(string id, out JeiItem item)
        {
            item = null;
            if (!_built) BuildIfNeeded();
            if (_cache == null) return false;
            return _cache.TryGetValue(id, out item);
        }

        public static IReadOnlyDictionary<string, JeiItem> Snapshot()
        {
            if (!_built) BuildIfNeeded();
            return _cache;
        }
    }
}
