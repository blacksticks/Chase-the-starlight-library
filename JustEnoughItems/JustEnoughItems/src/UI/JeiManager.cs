using JustEnoughItems.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JustEnoughItems.UI
{
    public class JeiManager : MonoBehaviour
    {
        private JeiUI _ui;
        private AssetBundle _bundle;
        private GameObject _root;
        private GameObject _currentPageInstance;
        private readonly List<GameObject> _hiddenSiblings = new List<GameObject>();
        private static int _lastShowFrame = -1;
        private const string DefaultBundleRelativePath = "JustEnoughItems/AssetBundles/Windows/jeiui";
        private const string DefaultRootPrefab = "JEI_Root";
        private const string DefaultItemCellPrefab = "ItemCell";
        private const string DefaultCategoryGroupPrefab = "CategoryGroup";
        public static JeiManager Instance { get; private set; }
        public static bool Visible => Instance != null && Instance._currentPageInstance != null;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            try { Plugin.Log?.LogInfo("JeiManager Awake: loading AssetBundle UI"); } catch { }

            TryInitUI();
            EnsureGlobalHotkeys();
            try { StartCoroutine(DeferredBuildJeiData()); } catch { }
            try { StartCoroutine(MonitorCraftDataAndAutoRebuild()); } catch { }
        }

        private IEnumerator MonitorCraftDataAndAutoRebuild()
        {
            // 监控 CraftData 的静态配方字典大小，增长时触发一次重建
            int lastCount = -1;
            float elapsed = 0f;
            while (true)
            {
                yield return new WaitForSeconds(1.0f);
                elapsed += 1f;
                try
                {
                    int count = GetCraftDataDictionaryCount();
                    if (count >= 0 && count != lastCount)
                    {
                        lastCount = count;
                        try { Plugin.Log?.LogInfo($"[Just Enough Items] CraftData dict changed/ready: count={count}, elapsed={elapsed:0}s -> rebuilding"); } catch { }
                        try { UnityEngine.Debug.Log($"[Just Enough Items] CraftData dict changed/ready: count={count}, elapsed={elapsed:0}s -> rebuilding"); } catch { }
                        try
                        {
                            JeiDataStore.Invalidate();
                            JeiDataStore.BuildIfNeeded();
                            var snap = JeiDataStore.Snapshot();
                            int items = snap?.Count ?? 0;
                            try { Plugin.Log?.LogInfo($"[Just Enough Items] DataStore items after CraftData change={items}"); } catch { }
                            try { UnityEngine.Debug.Log($"[Just Enough Items] DataStore items after CraftData change={items}"); } catch { }
                        }
                        catch (Exception ex)
                        {
                            try { UnityEngine.Debug.LogWarning($"[Just Enough Items] Auto rebuild on CraftData change failed: {ex.Message}"); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private int GetCraftDataDictionaryCount()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type craftData = null;
                foreach (var asm in assemblies)
                {
                    try { craftData = craftData ?? asm.GetType("CraftData"); } catch { }
                    if (craftData != null) break;
                }
                if (craftData == null) return -1;
                System.Collections.IDictionary dict = null;
                // 字段
                foreach (var f in craftData.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (!typeof(System.Collections.IDictionary).IsAssignableFrom(f.FieldType)) continue;
                        var candidate = f.GetValue(null) as System.Collections.IDictionary;
                        if (candidate != null) { dict = candidate; break; }
                    }
                    catch { }
                }
                // 属性
                if (dict == null)
                {
                    foreach (var p in craftData.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var pt = p.PropertyType;
                            if (!typeof(System.Collections.IDictionary).IsAssignableFrom(pt)) continue;
                            var candidate = p.GetValue(null, null) as System.Collections.IDictionary;
                            if (candidate != null) { dict = candidate; break; }
                        }
                        catch { }
                    }
                }
                if (dict == null) return -1;
                return dict.Count;
            }
            catch { return -1; }
        }


        private static void EnsureInstance()
        {
            if (Instance != null) return;
            Plugin.Log?.LogWarning("JeiManager.EnsureInstance: Instance is null, creating runtime host");
            var host = new GameObject("JEI_Runtime");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<JeiManager>();
            // Awake will run and create _ui
        }

        public static void Toggle()
        {
            EnsureInstance();
            // 新页面方案下的 Toggle：仅用于关闭
            try { Plugin.Log?.LogInfo($"JeiManager.Toggle: visible={Visible}"); } catch { }
            if (Visible)
            {
                Hide();
            }
            else
            {
                // 不再在无 Hover 的情况下打开列表，避免误触
            }
        }

        public static void ShowForItem(string itemId)
        {
            EnsureInstance();
            try
            {
                // 去抖：避免同一帧内被 Tooltip 与 GlobalHotkeys 等重复触发
                try
                {
                    if (Time.frameCount == _lastShowFrame)
                    {
                        Plugin.Log?.LogInfo($"JeiManager.ShowForItem: ignored duplicate in same frame for '{itemId}'");
                        return;
                    }
                    _lastShowFrame = Time.frameCount;
                }
                catch { }

                if (Instance._bundle == null || (Instance._ui == null && Instance._root == null))
                {
                    if (!Instance.TryInitUI())
                    {
                        Plugin.Log?.LogError("JEI initialization failed (AssetBundle missing or invalid). Cannot open page.");
                        return;
                    }
                }
                // 确保找到 PDA Content 作为页面父节点
                TryAttachToPdaScreen();
                // 确保 JEI 数据已构建；避免每次打开页面都全量扫描/读JSON造成卡顿
                try { JeiDataStore.BuildIfNeeded(); } catch { }
                // 确保图标缓存进行一次性初始化（仅成功时置位，不热更新），保证“首次从任意入口打开详情页”能加载图标
                try { JustEnoughItems.IconCache.InitializeOnce(); } catch { }
                // 统一去前缀与空白并大小写不敏感
                string NormalizeId(string s)
                {
                    if (string.IsNullOrEmpty(s)) return string.Empty;
                    var t = s.Trim();
                    if (t.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)) t = t.Substring("TechType.".Length);
                    return t.Trim();
                }
                string idRaw = itemId ?? string.Empty;
                string idNorm = NormalizeId(idRaw);
                // 从数据存储（扫描+JSON）获取 JeiItem
                JeiItem data = null;
                try
                {
                    JeiDataStore.TryGetItem(idNorm, out data);
                    var snap = JeiDataStore.Snapshot();
                    int cnt = snap?.Count ?? 0;
                    var preview = string.Join(", ", snap?.Keys.Take(10) ?? Enumerable.Empty<string>());
                    Plugin.Log?.LogInfo($"JEI Debug: DataStore items count={cnt}, FirstKeys=[{preview}] idRaw='{idRaw}', idNorm='{idNorm}', found={(data != null)}");
                }
                catch { }

                // 准备父节点：uGUI_PDAScreen(Clone)/Content
                var pda = GameObject.Find("uGUI_PDAScreen(Clone)");
                var parent = pda != null ? (pda.transform.Find("Content") as RectTransform) : null;
                if (parent == null)
                {
                    Plugin.Log?.LogError("JEI: PDA Content not found. Cannot attach pages.");
                    return;
                }

                // 清理上一个页面（仅销毁页面本体，不恢复兄弟；兄弟的恢复统一由 Hide() 完成）
                try { if (Instance._currentPageInstance != null) GameObject.Destroy(Instance._currentPageInstance); } catch { }
                Instance._currentPageInstance = null;

                // 选择并实例化页面 Prefab
                string prefabName = data != null ? "DetailPageRoot" : "NotFoundPageRoot";
                var prefab = Instance._bundle != null ? Instance._bundle.LoadAsset<GameObject>(prefabName) : null;
                if (prefab == null)
                {
                    Plugin.Log?.LogError($"JEI: Prefab '{prefabName}' not found in AssetBundle.");
                    return;
                }
                var page = GameObject.Instantiate(prefab);
                page.name = prefab.name + "_Instance";
                page.transform.SetParent(parent, false);
                (page.transform as RectTransform).SetAsLastSibling();
                Instance._currentPageInstance = page;

                // 隐藏兄弟（除当前页面与 JEI_EmbedRoot）：仅在首次显示 JEI 详情页时执行；
                // 若已在本次会话中隐藏过兄弟节点，则复用记录，避免在页面内二次跳转时丢失恢复列表。
                if (Instance._hiddenSiblings.Count == 0)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var ch = parent.GetChild(i).gameObject;
                        if (ch == page) continue;
                        // 保留概览页根 JEI_EmbedRoot 常驻，不进行隐藏
                        if (string.Equals(ch.name, "JEI_EmbedRoot", StringComparison.Ordinal)) continue;
                        if (ch.activeSelf)
                        {
                            ch.SetActive(false);
                            Instance._hiddenSiblings.Add(ch);
                        }
                    }
                }
                else
                {
                    // 已隐藏过兄弟，确保新页面可见并位于层级末尾
                    try { page.SetActive(true); } catch { }
                }

                // 运行时自动挂载并初始化
                try
                {
                    if (data != null)
                    {
                        var comp = page.GetComponent<JeiDetailPage>();
                        if (comp == null) comp = page.AddComponent<JeiDetailPage>();
                        comp.Init(data);
                    }
                    else
                    {
                        var comp = page.GetComponent<JeiNotFoundPage>();
                        if (comp == null) comp = page.AddComponent<JeiNotFoundPage>();
                        comp.Init(itemId);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"JEI: Page Init failed: {ex.Message}");
                }

                // 鼠标可见（以便交互）
                try { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; } catch { }
                try { Plugin.Log?.LogInfo($"JEI: Page shown -> prefab='{prefabName}', siblingsHidden={Instance._hiddenSiblings.Count}"); } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"ShowForItem exception: {ex}");
            }
        }

        public static void Hide()
        {
            if (Instance == null) return;
            try
            {
                // 防止同帧被其他输入路径再次打开
                try { _lastShowFrame = Time.frameCount; } catch { }
                Plugin.Log?.LogInfo("JEI: Hide called");
                // 立即隐藏并销毁当前页面
                if (Instance._currentPageInstance != null)
                {
                    try { Plugin.Log?.LogInfo($"JEI: Destroy current page '{Instance._currentPageInstance.name}'"); } catch { }
                    try { Instance._currentPageInstance.SetActive(false); } catch { }
                    GameObject.Destroy(Instance._currentPageInstance);
                    Instance._currentPageInstance = null;
                }

                // 恢复 PDA Content 兄弟（只恢复我们当次隐藏的项）
                try
                {
                    var pda = GameObject.Find("uGUI_PDAScreen(Clone)");
                    var parent = pda != null ? (pda.transform.Find("Content") as RectTransform) : null;
                    if (parent != null)
                    {
                        int restored = 0;
                        foreach (var go in Instance._hiddenSiblings)
                        {
                            if (go != null && !go.activeSelf) { go.SetActive(true); restored++; }
                        }
                        try { Plugin.Log?.LogInfo($"JEI: Restored siblings count={restored}"); } catch { }

                        // 兜底清理：如果页面残留（意外未销毁），强制查找并隐藏+销毁
                        for (int i = parent.childCount - 1; i >= 0; i--)
                        {
                            var ch = parent.GetChild(i).gameObject;
                            if (ch == null) continue;
                            var nm = ch.name ?? string.Empty;
                            if (nm.Equals("DetailPageRoot_Instance", StringComparison.OrdinalIgnoreCase) || nm.Equals("NotFoundPageRoot_Instance", StringComparison.OrdinalIgnoreCase))
                            {
                                try { Plugin.Log?.LogInfo($"JEI: Force destroy leftover page '{nm}'"); } catch { }
                                try { ch.SetActive(false); } catch { }
                                GameObject.Destroy(ch);
                            }
                        }
                    }
                }
                catch { }
                Instance._hiddenSiblings.Clear();
                try { Plugin.Log?.LogInfo("JEI: Hide finished"); } catch { }
            }
            catch { }
        }

        private bool TryInitUI()
        {
            try
            {
                // Try reuse already loaded bundle (by name)
                foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (string.Equals(b.name, "jeiui", StringComparison.OrdinalIgnoreCase))
                    {
                        _bundle = b;
                        break;
                    }
                }

                // If not found, load from file
                if (_bundle == null)
                {
                    // 1) 优先从 DLL 所在目录（QMods/JustEnoughItems）相对路径查找
                    try
                    {
                        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        if (!string.IsNullOrEmpty(asmDir))
                        {
                            var path1 = Path.Combine(asmDir, "AssetBundles", "Windows", "jeiui");
                            if (File.Exists(path1))
                            {
                                _bundle = AssetBundle.LoadFromFile(path1);
                                if (_bundle == null) Plugin.Log?.LogWarning($"Failed to load AssetBundle from: {path1}");
                            }
                        }
                    }
                    catch { }

                    // 2) 再尝试原 BepInEx 插件路径下的约定位置（兼容 BepInEx/plugins 部署）
                    if (_bundle == null)
                    {
                        var path2 = Path.Combine(BepInEx.Paths.PluginPath, DefaultBundleRelativePath);
                        if (File.Exists(path2))
                        {
                            _bundle = AssetBundle.LoadFromFile(path2);
                            if (_bundle == null) Plugin.Log?.LogWarning($"Failed to load AssetBundle from: {path2}");
                        }
                    }

                    // 3) 均未加载到则回退到运行时 UI
                    if (_bundle == null)
                    {
                        Plugin.Log?.LogWarning("JEI AssetBundle not found in both DLL-relative and BepInEx paths. Falling back to runtime UI.");
                        return InitRuntimeFallbackUI();
                    }
                }

                // 页面模式：仅需要 AssetBundle 以便加载页面 Prefab，不再实例化旧的根 UI
                Plugin.Log?.LogInfo("JeiManager: AssetBundle loaded (page mode), skipping legacy root UI initialization");
                EnsureGlobalHotkeys();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"JeiManager TryInitUI exception: {ex}");
                // Attempt fallback to runtime UI even on exception
                try
                {
                    if (InitRuntimeFallbackUI()) return true;
                }
                catch { }
                return false;
            }
        }

        private bool InitRuntimeFallbackUI()
        {
            try
            {
                _root = new GameObject("JEI_Root_Fallback");
                DontDestroyOnLoad(_root);
                var canvas = _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30000;
                var gr = _root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var cg = _root.AddComponent<UnityEngine.CanvasGroup>();
                cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true;

                // Content
                var contentGo = new GameObject("Content");
                contentGo.transform.SetParent(_root.transform, false);
                var content = contentGo.AddComponent<RectTransform>();
                content.anchorMin = new Vector2(0.1f, 0.1f);
                content.anchorMax = new Vector2(0.9f, 0.9f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                // ensure EventSystem exists
                if (EventSystem.current == null)
                {
                    var esGo = new GameObject("EventSystem");
                    esGo.AddComponent<EventSystem>();
                    esGo.AddComponent<StandaloneInputModule>();
                    GameObject.DontDestroyOnLoad(esGo);
                }

                _ui = _root.AddComponent<JeiUI>();
                _ui.Init(_root, content, null, null);
                _ui.Hide();
                Plugin.Log?.LogWarning("JeiManager: Initialized runtime fallback UI (no AssetBundle)");
                TryAttachToPdaScreen();
                EnsureGlobalHotkeys();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"InitRuntimeFallbackUI error: {ex}");
                return false;
            }
        }

        private static void EnsureGlobalHotkeys()
        {
            try
            {
                var host = GameObject.Find("JEI_GlobalHotkeys");
                if (host == null)
                {
                    host = new GameObject("JEI_GlobalHotkeys");
                    GameObject.DontDestroyOnLoad(host);
                }
                if (host.GetComponent<GlobalHotkeys>() == null)
                {
                    host.AddComponent<GlobalHotkeys>();
                    Plugin.Log?.LogInfo("JeiManager: GlobalHotkeys attached");
                }
            }
            catch { }
        }

        private static void TryAttachToPdaScreen()
        {
            try
            {
                if (Instance == null || Instance._root == null) return;
                var go = GameObject.Find("uGUI_PDAScreen(Clone)");
                if (go == null) return;
                // 挂到 uGUI_PDAScreen(Clone)/Content 下方（Content 内层级最高）
                var target = go.transform.Find("Content") as RectTransform;
                if (target == null) return;

                // 已经是子节点则跳过
                if (Instance._root.transform.parent == target) return;

                // 设为子节点并全拉伸
                Instance._root.transform.SetParent(target, false);
                var rt = Instance._root.GetComponent<RectTransform>() ?? Instance._root.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                // 放到层级最后，确保在 Content 内显示在最上层
                Instance._root.transform.SetAsLastSibling();
                // 不再强制修改 Canvas 排序与 Layer，避免覆盖预制体显示；仅保持兄弟顺序置底（置末）与全拉伸
                Plugin.Log?.LogInfo("JeiManager: Attached JEI UI under uGUI_PDAScreen(Clone)");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"TryAttachToPdaScreen failed: {ex.Message}");
            }
        }

        private IEnumerator DeferredBuildJeiData()
        {
            try { Plugin.Log?.LogInfo("[Just Enough Items] Deferred build (JeiManager): coroutine started"); } catch { }
            try { UnityEngine.Debug.Log("[Just Enough Items] Deferred build (JeiManager): coroutine started"); } catch { }
            yield return new WaitForSeconds(3f);
            int attempts = 0;
            while (attempts < 3)
            {
                attempts++;
                try { UnityEngine.Debug.Log($"[Just Enough Items] Deferred build (JeiManager): attempt heartbeat #{attempts}"); } catch { }
                try
                {
                    JeiDataStore.Invalidate();
                    JeiDataStore.BuildIfNeeded();
                    var snapshot = JeiDataStore.Snapshot();
                    int cnt = snapshot?.Count ?? 0;
                    try { Plugin.Log?.LogInfo($"[Just Enough Items] Deferred build (JeiManager) attempt #{attempts}: DataStore items={cnt}"); } catch { }
                    try { UnityEngine.Debug.Log($"[Just Enough Items] Deferred build (JeiManager) attempt #{attempts}: DataStore items={cnt}"); } catch { }
                    if (cnt > 0) yield break;
                }
                catch (Exception ex)
                {
                    try { UnityEngine.Debug.LogWarning($"[Just Enough Items] Deferred build (JeiManager) error on attempt #{attempts}: {ex.Message}"); } catch { }
                }
                yield return new WaitForSeconds(2f);
            }
        }
    }
}
