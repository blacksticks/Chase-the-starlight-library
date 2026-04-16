using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughItems.Patches
{
    [HarmonyPatch]
    internal static class BlueprintsEmbed
    {
        private static UnityEngine.AssetBundle _overviewBundle;
        private static GameObject _overviewInstance;
        private const string OverviewBundleFile = "jeiui";      // 请在 Unity 打包为此文件名
        private const string OverviewPrefabName = "JEI_Overview"; // 预制体名称
        private static RectTransform GetFieldRectTransform(object obj, string fieldName)
        {
            var t = obj.GetType();
            var fi = AccessTools.Field(t, fieldName);
            return fi?.GetValue(obj) as RectTransform;
        }



        // 延迟一次调用 Build 的执行器，避免首帧布局/引用未就绪
        private class JeiBuildDelayedInvoker : MonoBehaviour
        {
            public Component Builder;
            public System.Reflection.MethodInfo BuildMethod;

            private void Start()
            {
                try { StartCoroutine(InvokeNextFrame()); } catch { }
            }

            private System.Collections.IEnumerator InvokeNextFrame()
            {
                yield return null; // 下一帧
                try
                {
                    if (Builder != null && BuildMethod != null)
                    {
                        BuildMethod.Invoke(Builder, null);
                        try { Plugin.Log?.LogInfo("[JEI Embed] Overview builder executed (delayed)"); } catch { }
                    }
                }
                catch (System.Exception ex)
                {
                    try { Plugin.Log?.LogError($"[JEI Embed] Delayed Build() failed: {ex}"); } catch { }
                }
                // 执行一次后即可移除
                try { Destroy(this); } catch { }
            }
        }

        private static void TryInvokeOverviewBuilder(GameObject root)
        {
            try
            {
                // 使用反射，避免编译期依赖（若类型未编译进同一程序集仍可运行）
                var builderType = AccessTools.TypeByName("JustEnoughItems.UI.JeiOverviewBuilder");
                if (builderType == null)
                {
                    try { Plugin.Log?.LogError("[JEI Embed] Overview builder type not found: JustEnoughItems.UI.JeiOverviewBuilder"); } catch { }
                    return;
                }
                var buildMi = builderType.GetMethod("Build");
                if (buildMi == null)
                {
                    try { Plugin.Log?.LogError("[JEI Embed] Overview builder method Build() not found"); } catch { }
                    return;
                }
                var builder = root.GetComponent(builderType) ?? root.AddComponent(builderType);
                // 仅当未构建过时才执行 Build，避免图标与内容闪动
                bool alreadyBuilt = false;
                try
                {
                    var fiBuilt = builderType.GetField("_builtOnce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (fiBuilt != null)
                    {
                        var v = fiBuilt.GetValue(builder);
                        if (v is bool b) alreadyBuilt = b;
                    }
                }
                catch { }

                if (!alreadyBuilt)
                {
                    try
                    {
                        buildMi.Invoke(builder, null);
                        try { Plugin.Log?.LogInfo("[JEI Embed] Overview builder executed"); } catch { }
                    }
                    catch (System.Exception ex)
                    {
                        try { Plugin.Log?.LogError($"[JEI Embed] Build() threw: {ex}"); } catch { }
                    }
                    // 安排一次延迟调用，防止首帧布局/引用未准备好
                    try
                    {
                        var inv = root.AddComponent<JeiBuildDelayedInvoker>();
                        inv.Builder = builder as Component;
                        inv.BuildMethod = buildMi;
                    }
                    catch { }
                }
                else
                {
                    try { Plugin.Log?.LogInfo("[JEI Embed] Overview already built, skip re-build to prevent icon flicker"); } catch { }
                }
            }
            catch (System.Exception ex)
            {
                try { Plugin.Log?.LogError($"[JEI Embed] TryInvokeOverviewBuilder error: {ex}"); } catch { }
            }
        }

        private static CanvasGroup GetFieldCanvasGroup(object obj, string fieldName)
        {
            var t = obj.GetType();
            var fi = AccessTools.Field(t, fieldName);
            return fi?.GetValue(obj) as CanvasGroup;
        }

        private static RectTransform EnsureEmbedRoot(RectTransform parent)
        {
            if (parent == null) return null;
            var holder = parent.Find("JEI_EmbedRoot") as RectTransform;
            if (holder == null)
            {
                var go = new GameObject("JEI_EmbedRoot", typeof(RectTransform), typeof(CanvasGroup));
                holder = go.GetComponent<RectTransform>();
                holder.SetParent(parent, false);
                holder.anchorMin = Vector2.zero;
                holder.anchorMax = Vector2.one;
                holder.offsetMin = Vector2.zero;
                holder.offsetMax = Vector2.zero;
                var cg = go.GetComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;       // 允许 JEI 内部 ScrollRect 捕获输入
                cg.blocksRaycasts = true;     // 拦截射线，避免事件落到外层原版 ScrollRect
            }
            return holder;
        }

        private static RectTransform FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                if (ch != null && ch.name == name) return ch as RectTransform;
            }
            return null;
        }

        private static RectTransform FindChildByNameDeep(Transform root, string name)
        {
            if (root == null) return null;
            var stack = new System.Collections.Generic.Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur != null && cur.name == name)
                    return cur as RectTransform;
                for (int i = 0; i < cur.childCount; i++)
                {
                    stack.Push(cur.GetChild(i));
                }
            }
            return null;
        }

        // 回退：尝试通过 ScrollRect 结构定位蓝图内容容器（通常为 Scroll View/Viewport/Content）
        private static RectTransform TryFindScrollContent(Transform root)
        {
            if (root == null) return null;
            try
            {
                // 优先查找命名约定：Scroll View/Viewport/Content
                var scrollView = FindChildByNameDeep(root, "Scroll View");
                if (scrollView != null)
                {
                    var viewport = FindChildByName(scrollView, "Viewport") ?? FindChildByNameDeep(scrollView, "Viewport");
                    if (viewport != null)
                    {
                        var content = FindChildByName(viewport, "Content") ?? FindChildByNameDeep(viewport, "Content");
                        if (content != null) return content;
                    }
                }

                // 次优：任意 ScrollRect 的 content（若存在）
                var scrollRects = root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true);
                if (scrollRects != null && scrollRects.Length > 0)
                {
                    foreach (var sr in scrollRects)
                    {
                        if (sr != null && sr.content != null)
                            return sr.content;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "<null>";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            var cur = t;
            while (cur != null)
            {
                if (sb.Length == 0) sb.Insert(0, cur.name); else sb.Insert(0, cur.name + "/");
                cur = cur.parent;
            }
            return sb.ToString();
        }

        private static void LogHierarchySnapshot(Transform root)
        {
            try
            {
                if (root == null) { Plugin.Log?.LogInfo("[JEI Embed] Hierarchy snapshot: <root null>"); return; }
                Plugin.Log?.LogInfo($"[JEI Embed] Hierarchy snapshot at {GetTransformPath(root)}:");
                int childCount = root.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var ch = root.GetChild(i);
                    Plugin.Log?.LogInfo($"  - {i}: {ch.name} (active={ch.gameObject.activeSelf})");
                    int sub = Math.Min(6, ch.childCount);
                    for (int j = 0; j < sub; j++)
                    {
                        var ch2 = ch.GetChild(j);
                        Plugin.Log?.LogInfo($"      - {i}.{j}: {ch2.name} (active={ch2.gameObject.activeSelf})");
                    }
                }
            }
            catch { }
        }

        private static void HideSiblingsExcept(RectTransform parent, Transform except)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var ch = parent.GetChild(i);
                if (ch == null || ch == except) continue;
                try
                {
                    ch.gameObject.SetActive(false);
                    Plugin.Log?.LogInfo($"[JEI Embed] Fallback hide child: {GetTransformPath(ch)}");
                }
                catch { }
            }
        }

        private static GameObject TryFindOverviewPrefab(AssetBundle bundle, bool logPreview)
        {
            if (bundle == null) return null;
            // 1) 直接按名
            try
            {
                var byName = bundle.LoadAsset<GameObject>(OverviewPrefabName);
                if (byName != null)
                {
                    try { Plugin.Log?.LogInfo("[JEI Embed] Overview prefab resolved by name"); } catch { }
                    return byName;
                }
            }
            catch { }

            // 2) 扫描资产路径（AssetBundle 内通常存储小写路径）
            try
            {
                var names = bundle.GetAllAssetNames();
                if (names != null && names.Length > 0)
                {
                    if (logPreview)
                    {
                        // 诊断：最多打印前 10 个资产名
                        try
                        {
                            int cap = Math.Min(10, names.Length);
                            string preview = string.Join(", ", System.Linq.Enumerable.Take(names, cap));
                            Plugin.Log?.LogInfo($"[JEI Embed] Bundle assets preview: {preview}...");
                        }
                        catch { }
                    }

                    foreach (var n in names)
                    {
                        var ln = n.ToLowerInvariant();
                        if (ln.EndsWith("/jei_overview.prefab") || ln.EndsWith("/JEI_Overview.prefab".ToLowerInvariant()))
                        {
                            try
                            {
                                var byPath = bundle.LoadAsset<GameObject>(n);
                                if (byPath != null)
                                {
                                    try { Plugin.Log?.LogInfo($"[JEI Embed] Overview prefab resolved by path: {n}"); } catch { }
                                    return byPath;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 3) 退化：加载所有 GameObject，通过对象名匹配
            try
            {
                var all = bundle.LoadAllAssets<GameObject>();
                if (all != null)
                {
                    foreach (var go in all)
                    {
                        if (go != null && string.Equals(go.name, OverviewPrefabName, StringComparison.OrdinalIgnoreCase))
                        {
                            try { Plugin.Log?.LogInfo("[JEI Embed] Overview prefab resolved by scanning GameObjects"); } catch { }
                            return go;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static string ResolveBundlePath()
        {
            try
            {
                var dir = JustEnoughItems.Config.ConfigService.PluginsAssetBundlesDirectory;
                if (string.IsNullOrEmpty(dir)) return null;
                var win = System.IO.Path.Combine(dir, "Windows");
                var expectedPath = System.IO.Path.Combine(win, OverviewBundleFile);
                if (System.IO.File.Exists(expectedPath)) return expectedPath;
                try { Plugin.Log?.LogError($"[JEI Embed] Overview bundle not found. Expected: {expectedPath}"); } catch { }
            }
            catch { }
            return null;
        }

        [HarmonyPatch(typeof(uGUI_BlueprintsTab), nameof(uGUI_BlueprintsTab.Open))]
        [HarmonyPostfix]
        private static void Postfix_Open(uGUI_BlueprintsTab __instance)
        {
            try
            {
                // 优先使用 __instance.transform 作为父节点，避免私有字段差异
                var bpRoot = __instance != null ? (__instance.transform as RectTransform) : null;
                // 若存在私有字段 canvas，则优先用其作为父级
                var canvas = GetFieldRectTransform(__instance, "canvas") ?? bpRoot;
                // 优先把 JEI_EmbedRoot 放到 BlueprintsTab/Content 下
                RectTransform contentParentForEmbed = null;
                // 原版内容节点：优先通过字段 content（CanvasGroup 或 RectTransform），其次在层级中深度查找
                CanvasGroup content = GetFieldCanvasGroup(__instance, "content");
                if (content == null)
                {
                    var contentRt = GetFieldRectTransform(__instance, "content");
                    if (contentRt != null)
                    {
                        content = contentRt.GetComponent<CanvasGroup>();
                        if (content == null) content = contentRt.gameObject.AddComponent<CanvasGroup>();
                        contentParentForEmbed = contentRt;
                    }
                }
                if (content == null)
                {
                    RectTransform contentTf = null;
                    contentTf = FindChildByName(canvas, "Content") ?? FindChildByNameDeep(canvas, "Content");
                    if (contentTf == null) contentTf = FindChildByNameDeep(canvas, "BlueprintsContent");
                    if (contentTf == null) contentTf = FindChildByNameDeep(canvas, "content");
                    // 回退：通过 ScrollRect/Viewport/Content 定位
                    if (contentTf == null) contentTf = TryFindScrollContent(canvas);
                    if (contentTf != null)
                    {
                        content = contentTf.GetComponent<CanvasGroup>();
                        if (content == null) content = contentTf.gameObject.AddComponent<CanvasGroup>();
                        contentParentForEmbed = contentTf;
                    }
                }
                // 优先将 JEI_EmbedRoot 放在 ScrollCanvas 下，以便只控制该层的显示/隐藏
                RectTransform targetParent = contentParentForEmbed ?? canvas;
                var scrollCanvas = FindChildByNameDeep(targetParent, "ScrollCanvas") ?? FindChildByNameDeep(canvas, "ScrollCanvas");
                if (scrollCanvas != null) targetParent = scrollCanvas;
                var holder = EnsureEmbedRoot(targetParent);
                try { Plugin.Log?.LogInfo($"[JEI Embed] Holder parent at: {GetTransformPath(targetParent?.transform)}"); } catch { }
                if (holder != null)
                {
                    // 永久隐藏原版蓝图页：隐藏 ScrollCanvas 下除 JEI_EmbedRoot 外的所有子节点
                    try { HideSiblingsExcept(targetParent, holder); } catch { }
                    try { holder.SetAsLastSibling(); } catch { }

                    // 若已有实例（跨多次打开），确保重新挂载到当前 holder 并激活
                    if (_overviewInstance != null)
                    {
                        try
                        {
                            var rt = _overviewInstance.GetComponent<RectTransform>() ?? _overviewInstance.AddComponent<RectTransform>();
                            _overviewInstance.transform.SetParent(holder, false);
                            _overviewInstance.SetActive(true);
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                            holder.SetAsLastSibling();
                            Plugin.Log?.LogInfo("[JEI Embed] Reattached existing overview instance to holder and activated");
                        }
                        catch { }
                    }

                    // 从 AssetBundle 实例化 JEI_Overview 预制体
                    if (_overviewInstance == null)
                    {
                        try
                        {
                            if (_overviewBundle == null)
                            {
                                // 先尝试复用已加载的 Bundle，避免相同文件提示
                                try
                                {
                                    foreach (var b in UnityEngine.AssetBundle.GetAllLoadedAssetBundles())
                                    {
                                        var test = TryFindOverviewPrefab(b, logPreview: false);
                                        if (test != null)
                                        {
                                            _overviewBundle = b;
                                            try { Plugin.Log?.LogInfo("[JEI Embed] Found overview prefab in an already-loaded bundle (pre-scan)"); } catch { }
                                            break;
                                        }
                                    }
                                }
                                catch { }

                                if (_overviewBundle == null)
                                {
                                    var path = ResolveBundlePath();
                                    if (string.IsNullOrEmpty(path))
                                    {
                                        try { Plugin.Log?.LogError($"[JEI Embed] Overview bundle not found under: {JustEnoughItems.Config.ConfigService.PluginsAssetBundlesDirectory}"); } catch { }
                                    }
                                    else
                                    {
                                        _overviewBundle = UnityEngine.AssetBundle.LoadFromFile(path);
                                        try { Plugin.Log?.LogInfo($"[JEI Embed] Loaded bundle: {path} -> {(_overviewBundle!=null)}"); } catch { }
                                    }
                                }
                            }
                            GameObject prefab = TryFindOverviewPrefab(_overviewBundle, logPreview: true);
                            // 若直接加载失败（例如同一资源包已被其他地方加载导致本次返回 null），尝试复用已加载的 AssetBundle，并使用更稳健的查找
                            if (prefab == null)
                            {
                                try
                                {
                                    foreach (var b in UnityEngine.AssetBundle.GetAllLoadedAssetBundles())
                                    {
                                        GameObject test = TryFindOverviewPrefab(b, logPreview: false);
                                        if (test != null)
                                        {
                                            _overviewBundle = b;
                                            prefab = test;
                                            try { Plugin.Log?.LogInfo("[JEI Embed] Reused already-loaded bundle containing prefab"); } catch { }
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                            if (prefab != null)
                            {
                                _overviewInstance = UnityEngine.GameObject.Instantiate(prefab, holder);
                                try { _overviewInstance.SetActive(true); } catch { }
                                var rt = _overviewInstance.GetComponent<RectTransform>() ?? _overviewInstance.AddComponent<RectTransform>();
                                rt.anchorMin = Vector2.zero;
                                rt.anchorMax = Vector2.one;
                                rt.offsetMin = Vector2.zero;
                                rt.offsetMax = Vector2.zero;
                                // 关键：保证实例不随 PDA 关闭而被销毁（跨次复用，避免重复构建与卡顿）
                                try { UnityEngine.Object.DontDestroyOnLoad(_overviewInstance); } catch { }
                                try { Plugin.Log?.LogInfo("[JEI Embed] Overview prefab instantiated"); } catch { }
                                // 显式调用 Build，并安排一次延迟调用（不做任何与原版滚动条接线相关的处理）
                                TryInvokeOverviewBuilder(_overviewInstance);
                            }
                            else
                            {
                                try { Plugin.Log?.LogError($"[JEI Embed] Prefab '{OverviewPrefabName}' not found in bundle"); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogError($"[JEI Embed] Load overview prefab failed: {ex}");
                        }
                    }

                    if (holder != null)
                    {
                        holder.gameObject.SetActive(true);
                        holder.SetAsLastSibling();
                        try { Plugin.Log?.LogInfo("[JEI Embed] JEI_EmbedRoot active & on top under ScrollCanvas; vanilla hidden"); } catch { }
                    }
                }
                else
                {
                    try { Plugin.Log?.LogError("[JEI Embed] Holder not created (parent null)"); } catch { }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[JEI Embed] Open postfix error: {ex}");
            }
        }

        [HarmonyPatch(typeof(uGUI_BlueprintsTab), nameof(uGUI_BlueprintsTab.Close))]
        [HarmonyPostfix]
        private static void Postfix_Close(uGUI_BlueprintsTab __instance)
        {
            // 空操作：不再隐藏 JEI_EmbedRoot、不再恢复原版蓝图内容。
            // 目的：总览页常驻，原版蓝图页永久关闭，防止多次切换/关闭后 JEI 消失。
            try { Plugin.Log?.LogInfo("[JEI Embed] Close postfix: no-op (JEI stays, vanilla remains hidden)"); } catch { }
        }
    }
}
