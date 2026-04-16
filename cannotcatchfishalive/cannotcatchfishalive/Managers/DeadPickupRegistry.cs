using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Newtonsoft.Json;
using UnityEngine;

namespace CannotCatchFishAlive.Managers
{
    internal static class DeadPickupRegistry
    {
        private static readonly object _lock = new object();
        private static HashSet<string> _allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _filePath;
        private static FileSystemWatcher _watcher;

        public static void Initialize(string baseDir)
        {
            _filePath = Path.Combine(baseDir ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "deadpickups.json");
            Load();
            SetupWatcher();
        }

        public static void Dispose()
        {
            try { if (_watcher != null) { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); _watcher = null; } } catch { }
        }

        public static bool TryResolveCorpse(GameObject go, out TechType corpse)
        {
            corpse = TechType.None;
            if (go == null) return false;
            var keys = EnumerateKeys(go).Where(s => !string.IsNullOrEmpty(s));
            bool allowed = false;
            lock (_lock)
            {
                foreach (var k in keys) { if (_allow.Contains(k)) { allowed = true; break; } }
            }
            if (!allowed) return false;

            // 自动推导要给予的 TechType：
            // 1) CraftData.GetTechType(go)
            try { corpse = CraftData.GetTechType(go); } catch { corpse = TechType.None; }
            if (corpse != TechType.None) return true;

            // 2) 从对象或父级的 TechTag
            try
            {
                var tag = go.GetComponentInParent<TechTag>();
                if (tag != null && tag.type != TechType.None) { corpse = tag.type; return true; }
            }
            catch { }

            // 3) 若命中的 key 本身是 TechType 名称则解析
            foreach (var k in keys)
            {
                try { if (Enum.TryParse<TechType>(k, true, out corpse) && corpse != TechType.None) return true; }
                catch { }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateKeys(GameObject go)
        {
            TechType tt = CraftData.GetTechType(go);
            if (tt != TechType.None) yield return tt.ToString();
            string classId = CannotCatchFishAlive.Utils.PrefabUtil.GetPrefabIdentifierClassId(go);
            if (!string.IsNullOrEmpty(classId)) yield return classId;
            string name = go.name;
            if (!string.IsNullOrEmpty(name))
            {
                if (name.EndsWith("(Clone)")) name = name.Substring(0, name.Length - 7).Trim();
                yield return name;
            }
        }

        private static void EnsureFile()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    var json = JsonConvert.SerializeObject(new List<string>(), Formatting.Indented);
                    File.WriteAllText(_filePath, json);
                }
            }
            catch { }
        }

        private static void Load()
        {
            try
            {
                var json = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "[]";
                var list = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                lock (_lock)
                {
                    _allow = new HashSet<string>(list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                }
                CannotCatchFishAlive.Main.Log?.LogInfo($"DeadPickupRegistry loaded: {_allow.Count} entries");
            }
            catch (Exception e)
            {
                CannotCatchFishAlive.Main.Log?.LogError(e);
            }
        }

        private static void SetupWatcher()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                var name = Path.GetFileName(_filePath);
                _watcher = new FileSystemWatcher(dir, name);
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                _watcher.Changed += (_, __) => OnChanged();
                _watcher.Created += (_, __) => OnChanged();
                _watcher.Renamed += (_, __) => OnChanged();
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                CannotCatchFishAlive.Main.Log?.LogError(e);
            }
        }

        private static void OnChanged()
        {
            try
            {
                System.Threading.Thread.Sleep(50);
                Load();
                ErrorMessage.AddMessage("deadpickups.json 已重新载入");
            }
            catch { }
        }
    }
}
