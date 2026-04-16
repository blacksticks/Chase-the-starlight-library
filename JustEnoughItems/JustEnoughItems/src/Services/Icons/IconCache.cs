using JustEnoughItems.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JustEnoughItems
{
    internal static class IconCache
    {
        private static readonly Dictionary<string, Sprite> _byRelPath = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> _byId = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static void InitializeOnce()
        {
            if (_initialized) return;
            bool anyLoaded = false;
            try
            {
                // 1) 外部目录：<GameRoot>/QMods/JustEnoughItems/AssetBundles/icons
                var iconsRoot = ConfigService.NewIconsDirectory;
                if (Directory.Exists(iconsRoot))
                {
                    var files = Directory.GetFiles(iconsRoot, "*.png", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        try
                        {
                            var rel = NormalizeToIconsRelative(f);
                            if (string.IsNullOrEmpty(rel)) continue;
                            var spr = LoadSpriteFromFile(f);
                            if (spr == null) continue;
                            if (!_byRelPath.ContainsKey(rel)) _byRelPath[rel] = spr;
                            // 对于 ingredients/<id>.png，建立 id 映射
                            var parts = rel.Replace("\\", "/");
                            if (parts.StartsWith("icons/ingredients/", StringComparison.OrdinalIgnoreCase))
                            {
                                var name = Path.GetFileNameWithoutExtension(parts);
                                if (!_byId.ContainsKey(name)) _byId[name] = spr;
                            }
                            anyLoaded = true;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 2) 内置 AssetBundle：AssetBundles/Windows/jei-icons（可选），将其中的 Sprite 作为回退填充
            try
            {
                // 候选路径1：以程序集目录为基准
                var asmDir = Path.GetDirectoryName(typeof(IconCache).Assembly.Location);
                var candidate1 = Path.Combine(asmDir ?? string.Empty, "AssetBundles", "Windows", "jei-icons");

                // 候选路径2：以 QMods 路径为基准（通过外部 icons 根推导）
                // ConfigService.NewIconsDirectory = <GameRoot>/QMods/JustEnoughItems/AssetBundles/icons
                // 目标 = <GameRoot>/QMods/JustEnoughItems/AssetBundles/Windows/jei-icons
                string candidate2 = null;
                try
                {
                    var iconsRoot = ConfigService.NewIconsDirectory;
                    var iconsDir = Path.GetDirectoryName(iconsRoot);              // .../AssetBundles/icons
                    var assetBundlesDir = string.IsNullOrEmpty(iconsDir) ? null : Path.GetDirectoryName(iconsDir); // .../AssetBundles
                    if (!string.IsNullOrEmpty(assetBundlesDir))
                        candidate2 = Path.Combine(assetBundlesDir, "Windows", "jei-icons");
                }
                catch { }

                foreach (var bundlePath in new[] { candidate1, candidate2 })
                {
                    try
                    {
                        if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath)) continue;
                        var bundle = AssetBundle.LoadFromFile(bundlePath);
                        if (bundle == null) continue;
                        var sprites = bundle.LoadAllAssets<Sprite>() ?? Array.Empty<Sprite>();
                        foreach (var spr in sprites)
                        {
                            try
                            {
                                if (spr == null) continue;
                                var id = spr.name;
                                if (!_byId.ContainsKey(id)) _byId[id] = spr;
                                var rel = $"icons/ingredients/{id}.png";
                                if (!_byRelPath.ContainsKey(rel)) _byRelPath[rel] = spr;
                                anyLoaded = true;
                            }
                            catch { }
                        }
                        try { bundle.Unload(false); } catch { }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                // 3) 内置 Resources 回退（需要将资源放在 Resources/JEI/ingredients 下，命名为 <id>.png）
                // 外部优先，只有在外部不存在对应键时才填充
                var embedded = Resources.LoadAll<Sprite>("JEI/ingredients");
                foreach (var spr in embedded)
                {
                    try
                    {
                        if (spr == null) continue;
                        var id = spr.name;
                        if (!_byId.ContainsKey(id)) _byId[id] = spr;
                        var rel = $"icons/ingredients/{id}.png";
                        if (!_byRelPath.ContainsKey(rel)) _byRelPath[rel] = spr;
                        anyLoaded = true;
                    }
                    catch { }
                }
            }
            catch { }

            // 仅当至少载入到一个资源时，才标记初始化完成；否则允许稍后重试（依然保持“成功一次后不再热更新”）
            if (anyLoaded)
            {
                _initialized = true;
            }
        }

        public static Sprite GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            InitializeOnce();
            _byId.TryGetValue(id, out var s);
            return s;
        }

        public static Sprite GetByIconsRelative(string rel)
        {
            if (string.IsNullOrEmpty(rel)) return null;
            InitializeOnce();
            rel = rel.Replace("\\", "/");
            if (!rel.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)) return null;
            _byRelPath.TryGetValue(rel, out var s);
            return s;
        }

        public static bool IsReady()
        {
            try { return _byId.Count > 0 || _byRelPath.Count > 0; } catch { return false; }
        }

        private static string NormalizeToIconsRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            try
            {
                var root = ConfigService.NewIconsDirectory;
                var full = Path.GetFullPath(absolutePath);
                var rootFull = Path.GetFullPath(root);
                if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return null;
                var rel = full.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                rel = rel.Replace("\\", "/");
                return $"icons/{rel}";
            }
            catch { return null; }
        }

        private static Sprite LoadSpriteFromFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0) return null;
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(bytes, markNonReadable: false)) { UnityEngine.Object.Destroy(tex); return null; }
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                spr.hideFlags = HideFlags.DontUnloadUnusedAsset;
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return spr;
            }
            catch { return null; }
        }
    }
}
