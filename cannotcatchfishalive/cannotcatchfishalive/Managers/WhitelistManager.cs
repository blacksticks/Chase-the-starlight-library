using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Newtonsoft.Json;
using UnityEngine;

namespace CannotCatchFishAlive.Managers
{
    internal static class WhitelistManager
    {
        private static readonly object _lock = new object();
        private static HashSet<string> _items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static FileSystemWatcher _watcher;
        private static string _filePath;

        public static void Initialize(string baseDir)
        {
            _filePath = Path.Combine(baseDir ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "whitelist.json");
            EnsureFile();
            Load();
            SetupWatcher();
        }

        public static void Dispose()
        {
            try { if (_watcher != null) { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); _watcher = null; } } catch { }
        }

        public static bool IsWhitelisted(GameObject go)
        {
            if (go == null) return false;
            string tech = Utils.DetectionUtil.GetTechTypeName(go);
            if (!string.IsNullOrEmpty(tech) && Contains(tech)) return true;
            string classId = Utils.DetectionUtil.GetClassId(go);
            if (!string.IsNullOrEmpty(classId) && Contains(classId)) return true;
            return false;
        }

        public static void ForceReload()
        {
            Load();
        }

        private static bool Contains(string key)
        {
            lock (_lock) { return _items.Contains(key); }
        }

        private static void EnsureFile()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    var defaultList = new List<string> { "Floater" };
                    var json = JsonConvert.SerializeObject(defaultList, Formatting.Indented);
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
                    _items = new HashSet<string>(list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                }
                CannotCatchFishAlive.Main.Log?.LogInfo($"Whitelist loaded: {_items.Count} items");
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
                ErrorMessage.AddMessage("whitelist.json 已重新载入");
            }
            catch { }
        }
    }
}
