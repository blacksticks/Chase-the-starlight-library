using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using QuestBook.Models;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using QuestBook.UI;
using QuestBook.Config;

namespace QuestBook
{
    internal static class UIManager
    {
        private static bool _loaded;
        private static AssetBundle _bundle;
        private static GameObject _root;
        private static QuestBookData _data;
        private static Transform _chaptersContent;
        private static Transform _graphPanel;

        private static GameObject _pfChapterItem;
        private static GameObject _pfChapterPage;
        private static GameObject _pfNodeItem;
        private static GameObject _pfEdgeLine;
        private static GameObject _pfContextMenu;
        private static GameObject _pfMenuButtonItem;
        private static GameObject _pfCreateChapterDialog;
        private static GameObject _pfCreateTaskDialog;
        private static GameObject _pfIconPickerDialog;
        private static GameObject _pfIconGridItem;
        private static GameObject _pfConfirmExitDialog;

        private static GameObject _currentChapterPage;
        private static int _currentChapterIndex = -1;
        private static bool _prevCursorVisible;
        private static CursorLockMode _prevLockMode;
        private static StandaloneInputModule _sim;
        private static FPSInputModule _fpsim;
        private static bool _fpsimPrevEnabled;
        private static int _prevDragThreshold = -1;
        private static GameObject _iconPickerInstance;
        private static Dictionary<string, Sprite> _iconSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static GameObject _confirmExitInstance;
        private static UserData _userData;
        private static Vector2? _pendingCreateNodeScreenPos;
        private static Vector2? _pendingCreateNodeLocalPos;

        internal static bool IsOpen => _root != null && _root.activeSelf;

        internal static Transform GetCurrentChapterPageTransform() => _currentChapterPage != null ? _currentChapterPage.transform : null;

        private class OneShotAction : MonoBehaviour
        {
            internal Action Action;
            private System.Collections.IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                try { Action?.Invoke(); }
                catch (Exception e) { Mod.Log?.LogWarning($"OneShotAction error: {e.Message}"); }
                Destroy(this);
            }
        }

        internal static void SaveCreatedChapterMeta(string id, string name, int order, bool visible, string description)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_userData == null) _userData = new UserData();
            var meta = _userData.CreatedChapters.Find(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (meta == null)
            {
                meta = new UserCreatedChapter { Id = id };
                _userData.CreatedChapters.Add(meta);
            }
            meta.Name = name;
            meta.Order = order;
            meta.VisibleByDefault = visible;
            meta.Description = description;
            try { UserDataRepository.Save(_userData); } catch { }
        }

        private static void MergeUserCreatedChapters()
        {
            if (_data == null || _userData == null) return;
            foreach (var meta in _userData.CreatedChapters)
            {
                if (meta == null || string.IsNullOrEmpty(meta.Id)) continue;
                var exists = _data.Chapters.Exists(c => string.Equals(c.Id, meta.Id, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    var ch = new Chapter
                    {
                        Id = meta.Id,
                        Name = string.IsNullOrEmpty(meta.Name) ? meta.Id : meta.Name,
                        Order = meta.Order,
                        VisibleByDefault = meta.VisibleByDefault
                    };
                    _data.Chapters.Add(ch);
                }
            }
            _data.Chapters.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private static void MergeUserCreatedTasks()
        {
            if (_data == null || _userData == null) return;
            foreach (var t in _userData.CreatedTasks)
            {
                if (t == null || string.IsNullOrEmpty(t.ChapterId) || string.IsNullOrEmpty(t.Id)) continue;
                var ch = _data.Chapters.Find(c => string.Equals(c.Id, t.ChapterId, StringComparison.OrdinalIgnoreCase));
                if (ch == null) continue; // 若章节不存在则跳过（章节合并已在前面完成）
                var exists = ch.Nodes.Exists(n => string.Equals(n.Id, t.Id, StringComparison.OrdinalIgnoreCase));
                if (exists) continue;

                var node = new Node
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Icon = t.IconPath,
                    PosX = t.PosX,
                    PosY = t.PosY,
                    Resettable = true,
                    VisibleRule = t.VisibleBeforeUnlock ? "always" : "after_unlock",
                    UnlockRule = t.DirectUnlock ? "direct" : (t.UnlockConditionIndex == 0 ? "prereq_any" : "prereq_all"),
                };
                if (t.RewardModeIndex == 0 && t.RewardItems != null)
                {
                    foreach (var it in t.RewardItems)
                    {
                        if (it == null || string.IsNullOrWhiteSpace(it.ItemId)) continue;
                        var rew = new Reward { Type = "item" };
                        rew.Params["id"] = it.ItemId;
                        rew.Params["count"] = it.Count.ToString();
                        node.Rewards.Add(rew);
                    }
                }
                else if (t.RewardModeIndex == 1)
                {
                    var rew = new Reward { Type = "pool" };
                    rew.Params["index"] = t.RewardPoolIndex.ToString();
                    node.Rewards.Add(rew);
                }
                ch.Nodes.Add(node);

                // 合并前置边
                if (t.PrereqNodeIds != null)
                {
                    foreach (var pid in t.PrereqNodeIds)
                    {
                        if (string.IsNullOrEmpty(pid)) continue;
                        if (!ch.Nodes.Exists(n => string.Equals(n.Id, pid, StringComparison.OrdinalIgnoreCase))) continue;
                        if (!ch.Edges.Exists(e => string.Equals(e.From, pid, StringComparison.OrdinalIgnoreCase) && string.Equals(e.To, node.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            ch.Edges.Add(new Edge { From = pid, To = node.Id, RequireComplete = (t.UnlockConditionIndex == 1), HideUntilComplete = false });
                        }
                    }
                }
            }
        }

        internal static string GetBackgroundsDirectory()
        {
            try
            {
                var uiDir = GetBundleBasePath();
                if (string.IsNullOrEmpty(uiDir)) return null;
                var assetsDir = Path.GetDirectoryName(uiDir); // ...\Assets
                if (string.IsNullOrEmpty(assetsDir)) return null;
                var bgDir = Path.Combine(assetsDir, "Background");
                return bgDir;
            }
            catch { return null; }
        }

        private static List<string> ListBackgroundFiles()
        {
            var list = new List<string>();
            try
            {
                var dir = GetBackgroundsDirectory();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
                var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
                foreach (var p in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(p);
                    if (exts.Contains(ext)) list.Add(p);
                }
                list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(a), Path.GetFileName(b)));
            }
            catch { }
            return list;
        }


        private static Sprite LoadSpriteCached(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            if (_iconSpriteCache.TryGetValue(filePath, out var sp) && sp != null)
                return sp;
            sp = LoadSpriteFromFile(filePath);
            if (sp != null) _iconSpriteCache[filePath] = sp;
            return sp;
        }

        private static List<string> ListIconFiles()
        {
            var list = new List<string>();
            try
            {
                var dir = GetIconsDirectory();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
                var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
                foreach (var p in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(p);
                    if (exts.Contains(ext)) list.Add(p);
                }
                list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(a), Path.GetFileName(b)));
            }
            catch { }
            return list;
        }

        internal static bool OpenIconPicker(Action<string, Sprite> onPicked)
        {
            EnsureLoaded();
            // 若缺少预制体，返回 false 由上层决定是否兜底
            if (_pfIconPickerDialog == null || _pfIconGridItem == null)
                return false;

            if (_iconPickerInstance == null)
            {
                _iconPickerInstance = UnityEngine.Object.Instantiate(_pfIconPickerDialog, _root != null ? _root.transform : null);
                _iconPickerInstance.name = "IconPickerDialog(Runtime)";
                _iconPickerInstance.transform.SetAsLastSibling();
                var cv = _iconPickerInstance.GetComponent<Canvas>();
                if (cv == null) cv = _iconPickerInstance.AddComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.overrideSorting = true;
                if (cv.sortingOrder < 30060) cv.sortingOrder = 30060; // 高于 CreateChapterDialog
                if (_iconPickerInstance.GetComponent<GraphicRaycaster>() == null)
                    _iconPickerInstance.AddComponent<GraphicRaycaster>();
                var cg = _iconPickerInstance.GetComponent<CanvasGroup>();
                if (cg == null) cg = _iconPickerInstance.AddComponent<CanvasGroup>();
                cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
                if (_iconPickerInstance.GetComponent<QuestBook.UI.IconPickerDialogController>() == null)
                {
                    _iconPickerInstance.AddComponent<QuestBook.UI.IconPickerDialogController>();
                }
            }
            var ctrl = _iconPickerInstance.GetComponent<QuestBook.UI.IconPickerDialogController>();
            if (ctrl != null)
            {
                var files = ListIconFiles();
                ctrl.Initialize(files, LoadSpriteCached, (path, sp) =>
                {
                    try { onPicked?.Invoke(path, sp); }
                    catch (Exception e) { Mod.Log?.LogWarning($"Icon picked cb error: {e.Message}"); }
                    CloseIconPicker();
                }, _pfIconGridItem);
            }
            _iconPickerInstance.SetActive(true);
            return true;
        }

        // 章节下所有节点（Id, Title）清单，供前置多选使用
        internal static List<KeyValuePair<string, string>> GetChapterNodeList(string chapterId)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (_data == null || string.IsNullOrEmpty(chapterId)) return list;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (ch == null || ch.Nodes == null) return list;
            for (int i = 0; i < ch.Nodes.Count; i++)
            {
                var n = ch.Nodes[i];
                if (n == null) continue;
                var title = string.IsNullOrEmpty(n.Title) ? n.Id : n.Title;
                list.Add(new KeyValuePair<string, string>(n.Id, title));
            }
            return list;
        }

        internal static void SetPendingCreateNodeScreenPos(Vector2 screenPos)
        {
            _pendingCreateNodeScreenPos = screenPos;
            _pendingCreateNodeLocalPos = null;
            try { EnsurePendingLocalFromScreen(); } catch { }
        }

        private static void EnsurePendingLocalFromScreen()
        {
            if (!_pendingCreateNodeScreenPos.HasValue || _pendingCreateNodeLocalPos.HasValue) return;
            var nodes = GetNodesContainerTransform() as RectTransform;
            if (nodes == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(nodes, _pendingCreateNodeScreenPos.Value, null, out local))
            {
                _pendingCreateNodeLocalPos = local;
            }
        }

        internal static void OnNodeDragged(string chapterId, string nodeId, Vector2 localAnchoredPos)
        {
            if (_data == null || string.IsNullOrEmpty(chapterId) || string.IsNullOrEmpty(nodeId)) return;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (ch == null) return;
            var node = ch.Nodes.Find(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null) return;
            node.PosX = localAnchoredPos.x;
            node.PosY = -localAnchoredPos.y;

            // 若为用户创建任务，同步持久化位置
            if (_userData != null)
            {
                var ut = _userData.CreatedTasks.Find(t => string.Equals(t.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                if (ut != null)
                {
                    ut.PosX = node.PosX;
                    ut.PosY = node.PosY;
                    try { UserDataRepository.Save(_userData); } catch { }
                }
            }
            RefreshEdges();
        }

        internal static void RefreshEdges()
        {
            if (_currentChapterIndex < 0 || _data == null) return;
            RectTransform nodesContainer = null, edgesContainer = null;
            if (_currentChapterPage != null)
            {
                nodesContainer = _currentChapterPage.transform.Find("ContentRoot/NodesContainer") as RectTransform;
                edgesContainer = _currentChapterPage.transform.Find("ContentRoot/EdgesContainer") as RectTransform;
            }
            // 回退到全局容器
            if (nodesContainer == null || edgesContainer == null)
            {
                if (_root != null)
                {
                    nodesContainer = _root.transform.Find("GraphPanel/Viewport/ContentRoot/NodesContainer") as RectTransform;
                    edgesContainer = _root.transform.Find("GraphPanel/Viewport/ContentRoot/EdgesContainer") as RectTransform;
                }
            }
            if (nodesContainer == null || edgesContainer == null) return;
            RenderEdges(edgesContainer, nodesContainer);
        }

        internal static bool OpenBackgroundPicker(Action<string, Sprite> onPicked)
        {
            EnsureLoaded();
            if (_pfIconPickerDialog == null || _pfIconGridItem == null)
                return false;

            if (_iconPickerInstance == null)
            {
                _iconPickerInstance = UnityEngine.Object.Instantiate(_pfIconPickerDialog, _root != null ? _root.transform : null);
                _iconPickerInstance.name = "BackgroundPickerDialog(Runtime)";
                _iconPickerInstance.transform.SetAsLastSibling();
                var cv = _iconPickerInstance.GetComponent<Canvas>();
                if (cv == null) cv = _iconPickerInstance.AddComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.overrideSorting = true;
                if (cv.sortingOrder < 30060) cv.sortingOrder = 30060;
                if (_iconPickerInstance.GetComponent<GraphicRaycaster>() == null)
                    _iconPickerInstance.AddComponent<GraphicRaycaster>();
                var cg = _iconPickerInstance.GetComponent<CanvasGroup>();
                if (cg == null) cg = _iconPickerInstance.AddComponent<CanvasGroup>();
                cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
                if (_iconPickerInstance.GetComponent<QuestBook.UI.IconPickerDialogController>() == null)
                {
                    _iconPickerInstance.AddComponent<QuestBook.UI.IconPickerDialogController>();
                }
            }
            // 设置标题为“选择背景”
            try
            {
                var titleTr = _iconPickerInstance.transform.Find("Panel/Header/Title");
                var txt = titleTr != null ? titleTr.GetComponent<Text>() : null;
                if (txt != null) txt.text = "选择背景";
            }
            catch { }

            var ctrl = _iconPickerInstance.GetComponent<QuestBook.UI.IconPickerDialogController>();
            if (ctrl != null)
            {
                var files = ListBackgroundFiles();
                ctrl.Initialize(files, LoadSpriteCached, (path, sp) =>
                {
                    try { onPicked?.Invoke(path, sp); }
                    catch (Exception e) { Mod.Log?.LogWarning($"Background picked cb error: {e.Message}"); }
                    CloseIconPicker();
                }, _pfIconGridItem);
            }
            _iconPickerInstance.SetActive(true);
            return true;
        }

        internal static void CloseIconPicker()
        {
            if (_iconPickerInstance != null)
            {
                UnityEngine.Object.Destroy(_iconPickerInstance);
                _iconPickerInstance = null;
            }
        }
        
        internal static bool OpenConfirmExitDialog(string message, Action onYes, Action onNo)
        {
            EnsureLoaded();
            if (_pfConfirmExitDialog == null)
                return false;
            if (_confirmExitInstance == null)
            {
                _confirmExitInstance = UnityEngine.Object.Instantiate(_pfConfirmExitDialog, _root != null ? _root.transform : null);
                _confirmExitInstance.name = "ConfirmExitDialog(Runtime)";
                var cv = _confirmExitInstance.GetComponent<Canvas>();
                if (cv == null) cv = _confirmExitInstance.AddComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.overrideSorting = true;
                if (cv.sortingOrder < 30070) cv.sortingOrder = 30070; // 高于 CreateChapterDialog(30050)
                if (_confirmExitInstance.GetComponent<GraphicRaycaster>() == null)
                    _confirmExitInstance.AddComponent<GraphicRaycaster>();
                var cg = _confirmExitInstance.GetComponent<CanvasGroup>();
                if (cg == null) cg = _confirmExitInstance.AddComponent<CanvasGroup>();
                cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
                if (_confirmExitInstance.GetComponent<QuestBook.UI.ConfirmExitDialogController>() == null)
                {
                    _confirmExitInstance.AddComponent<QuestBook.UI.ConfirmExitDialogController>();
                }
            }
            var ctrl = _confirmExitInstance.GetComponent<QuestBook.UI.ConfirmExitDialogController>();
            if (ctrl != null)
            {
                ctrl.Initialize(message,
                    () => { try { onYes?.Invoke(); } catch (Exception e) { Mod.Log?.LogWarning($"Confirm yes cb error: {e.Message}"); } CloseConfirmExitDialog(); },
                    () => { try { onNo?.Invoke(); } catch (Exception e) { Mod.Log?.LogWarning($"Confirm no cb error: {e.Message}"); } CloseConfirmExitDialog(); }
                );
            }
            _confirmExitInstance.SetActive(true);
            return true;
        }

        internal static void CloseConfirmExitDialog()
        {
            if (_confirmExitInstance != null)
            {
                UnityEngine.Object.Destroy(_confirmExitInstance);
                _confirmExitInstance = null;
            }
        }
        

        internal static bool TryPickFileWithDialog(string initialDir, string filter, string title, out string filePath)
        {
            filePath = null;
            try
            {
                var ofdType = Type.GetType("System.Windows.Forms.OpenFileDialog, System.Windows.Forms");
                if (ofdType == null)
                {
                    return false;
                }
                var ofd = Activator.CreateInstance(ofdType);
                // set basic properties via reflection to avoid compile-time reference
                var piInit = ofdType.GetProperty("InitialDirectory");
                var piFilter = ofdType.GetProperty("Filter");
                var piTitle = ofdType.GetProperty("Title");
                var piMulti = ofdType.GetProperty("Multiselect");
                if (piInit != null && !string.IsNullOrEmpty(initialDir)) piInit.SetValue(ofd, initialDir, null);
                if (piFilter != null && !string.IsNullOrEmpty(filter)) piFilter.SetValue(ofd, filter, null);
                if (piTitle != null && !string.IsNullOrEmpty(title)) piTitle.SetValue(ofd, title, null);
                if (piMulti != null) piMulti.SetValue(ofd, false, null);
                var miShow = ofdType.GetMethod("ShowDialog", Type.EmptyTypes);
                if (miShow == null) return false;
                var result = miShow.Invoke(ofd, null);
                // try get FileName regardless of dialog result; empty means cancel
                var piFile = ofdType.GetProperty("FileName");
                if (piFile != null)
                {
                    var val = piFile.GetValue(ofd, null) as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        filePath = val;
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                filePath = null;
                return false;
            }
        }

        internal static void SetData(QuestBookData data)
        {
            _data = data;
            if (_data != null)
            {
                Mod.Log?.LogInfo($"QuestBook loaded: chapters={_data.Chapters.Count}");
            }
            // 若用户数据已存在，则把用户创建章节与任务合并进来
            try { MergeUserCreatedChapters(); } catch { }
            try { MergeUserCreatedTasks(); } catch { }
        }

        internal static void SetUserData(UserData data)
        {
            _userData = data ?? new UserData();
            // 若已加载配置数据，则将用户创建章节合并
            try { MergeUserCreatedChapters(); } catch { }
            try { MergeUserCreatedTasks(); } catch { }
        }

        internal static string GetCurrentChapterId()
        {
            if (_data == null) return null;
            if (_currentChapterIndex < 0 || _currentChapterIndex >= _data.Chapters.Count) return null;
            return _data.Chapters[_currentChapterIndex].Id;
        }

        internal static Transform GetChaptersPanelTransform() => _root != null ? _root.transform.Find("ChaptersPanel") : null;
        internal static Transform GetGraphPanelTransform() => _graphPanel;
        internal static Transform GetNodesContainerTransform()
        {
            // 优先返回当前章节页内的容器，保障不同章节的节点相互隔离
            if (_currentChapterPage != null)
            {
                var local = _currentChapterPage.transform.Find("ContentRoot/NodesContainer");
                if (local != null) return local;
            }
            if (_root == null) return null;
            // 回退：使用全局容器（兼容旧结构或缺省时）
            return _root.transform.Find("GraphPanel/Viewport/ContentRoot/NodesContainer");
        }
        internal static Transform GetGraphViewportTransform()
        {
            if (_root == null) return null;
            return _root.transform.Find("GraphPanel/Viewport");
        }
        

        internal static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            var baseDir = GetBundleBasePath();
            string[] searchDirs = new[]
            {
                baseDir,
                Path.GetFullPath(Path.Combine(baseDir, "..")),
                Path.GetFullPath(Path.Combine(baseDir, "../..")),
            };
            string bundlePath = null;
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var candidate = Path.Combine(dir, "QuestBookUI");
                if (File.Exists(candidate)) { bundlePath = candidate; break; }
            }
            if (bundlePath != null)
            {
                _bundle = AssetBundle.LoadFromFile(bundlePath);
                if (_bundle != null)
                {
                    try
                    {
                        var names = _bundle.GetAllAssetNames();
                        if (names != null && names.Length > 0)
                        {
                            Mod.Log?.LogInfo($"AB assets: {names.Length}");
                            int max = Mathf.Min(12, names.Length);
                            for (int i = 0; i < max; i++) Mod.Log?.LogInfo($" - {names[i]}");
                        }
                        // 容错：若 IconPickerDialog 或 IconGridItem 未直接命中，尝试遍历名称
                        if ((_pfIconPickerDialog == null || _pfIconGridItem == null))
                        {
                            try
                            {
                                var all = _bundle.GetAllAssetNames();
                                if (all != null)
                                {
                                    foreach (var a in all)
                                    {
                                        var lower = a.ToLowerInvariant();
                                        if (_pfIconPickerDialog == null && (lower.EndsWith("/iconpickerdialog.prefab") || lower.Contains("/iconpickerdialog")))
                                        {
                                            _pfIconPickerDialog = _bundle.LoadAsset<GameObject>(a);
                                        }
                                        if (_pfIconGridItem == null && (lower.EndsWith("/icongriditem.prefab") || lower.Contains("/icongriditem")))
                                        {
                                            _pfIconGridItem = _bundle.LoadAsset<GameObject>(a);
                                        }
                                        if (_pfIconPickerDialog != null && _pfIconGridItem != null) break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (_pfCreateTaskDialog == null)
                        {
                            try
                            {
                                var all = _bundle.GetAllAssetNames();
                                if (all != null)
                                {
                                    foreach (var a in all)
                                    {
                                        var lower = a.ToLowerInvariant();
                                        if (lower.EndsWith("/createtaskdialog.prefab") || lower.Contains("/createtaskdialog"))
                                        {
                                            _pfCreateTaskDialog = _bundle.LoadAsset<GameObject>(a);
                                            if (_pfCreateTaskDialog != null)
                                            {
                                                Mod.Log?.LogInfo($"CreateTaskDialog loaded via path: {a}");
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    var prefab = _bundle.LoadAsset<GameObject>("QuestBookCanvas");
                    if (prefab != null)
                    {
                        _root = UnityEngine.Object.Instantiate(prefab);
                        UnityEngine.Object.DontDestroyOnLoad(_root);
                        _root.SetActive(false);

                        _chaptersContent = _root.transform.Find("ChaptersPanel/ChaptersContent");
                        _graphPanel = _root.transform.Find("GraphPanel");

                        _pfChapterItem = _bundle.LoadAsset<GameObject>("ChapterItemPrefab");
                        _pfChapterPage = _bundle.LoadAsset<GameObject>("ChapterPagePrefab");
                        _pfNodeItem = _bundle.LoadAsset<GameObject>("NodeItemPrefab");
                        _pfEdgeLine = _bundle.LoadAsset<GameObject>("EdgeLinePrefab");
                        _pfContextMenu = _bundle.LoadAsset<GameObject>("ContextMenuPrefab");
                        _pfMenuButtonItem = _bundle.LoadAsset<GameObject>("MenuButtonItemPrefab");
                        _pfCreateChapterDialog = _bundle.LoadAsset<GameObject>("CreateChapterDialog");
                        _pfCreateTaskDialog = _bundle.LoadAsset<GameObject>("CreateTaskDialog");
                        _pfIconPickerDialog = _bundle.LoadAsset<GameObject>("IconPickerDialog");
                        _pfIconGridItem = _bundle.LoadAsset<GameObject>("IconGridItem");
                        _pfConfirmExitDialog = _bundle.LoadAsset<GameObject>("ConfirmExitDialog");
                        if (_pfCreateChapterDialog == null)
                        {
                            // 兼容以路径形式导出的资源名（通常为全小写 assets/.../createchapterdialog.prefab）
                            try
                            {
                                var all = _bundle.GetAllAssetNames();
                                if (all != null)
                                {
                                    foreach (var a in all)
                                    {
                                        var lower = a.ToLowerInvariant();
                                        if (lower.EndsWith("/createchapterdialog.prefab") || lower.Contains("/createchapterdialog"))
                                        {
                                            _pfCreateChapterDialog = _bundle.LoadAsset<GameObject>(a);
                                            if (_pfCreateChapterDialog != null)
                                            {
                                                Mod.Log?.LogInfo($"CreateChapterDialog loaded via path: {a}");
                                                break;
                                            }
                                        }
                                        if (_pfConfirmExitDialog == null && (lower.EndsWith("/confirmexitdialog.prefab") || lower.Contains("/confirmexitdialog")))
                                        {
                                            _pfConfirmExitDialog = _bundle.LoadAsset<GameObject>(a);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        if (_pfContextMenu == null)
                        {
                            Mod.Log?.LogWarning("ContextMenuPrefab not found in bundle (AB missing). Context menu will not show until prefab is fixed.");
                        }
                        if (_pfMenuButtonItem == null)
                        {
                            Mod.Log?.LogWarning("MenuButtonItemPrefab not found in bundle (AB missing). Context menu will not show until prefab is fixed.");
                        }
                        if (_pfCreateChapterDialog == null)
                        {
                            Mod.Log?.LogWarning("CreateChapterDialog not found in bundle. Ensure prefab name is 'CreateChapterDialog' in AssetBundle QuestBookUI.");
                        }

                        var existingEs = UnityEngine.Object.FindObjectOfType<EventSystem>();
                        if (existingEs == null)
                        {
                            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                            UnityEngine.Object.DontDestroyOnLoad(es);
                            var sim = es.GetComponent<StandaloneInputModule>();
                            if (sim != null) sim.forceModuleActive = true;
                            Mod.Log?.LogInfo("EventSystem created by UIManager. StandaloneInputModule.forceModuleActive=true");
                        }
                        else
                        {
                            var sim = existingEs.GetComponent<StandaloneInputModule>();
                            if (sim == null)
                            {
                                sim = existingEs.gameObject.AddComponent<StandaloneInputModule>();
                            }
                            sim.forceModuleActive = true;
                            Mod.Log?.LogInfo("EventSystem found. StandaloneInputModule ensured and forceModuleActive=true");
                        }

                        ContextMenuController.Initialize(_pfContextMenu, _pfMenuButtonItem, _root.transform);

                        // 不再在此阶段强行添加全局右键分发器/裁剪/光标守护；将重新设计输入与菜单逻辑

                        var canvas = _root.GetComponentInChildren<Canvas>();
                        if (canvas != null)
                        {
                            // 确保主Canvas位于最顶层渲染与事件层
                            canvas.overrideSorting = true;
                            if (canvas.sortingOrder < 20000) canvas.sortingOrder = 20000;
                            var raycaster = canvas.GetComponent<GraphicRaycaster>();
                            if (raycaster == null)
                            {
                                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                                Mod.Log?.LogInfo("GraphicRaycaster added to QuestBook Canvas.");
                            }
                        }

                        // 创建一个独立的全屏透明拦截Canvas（排序略低于主Canvas），用于拦截空白区域点击，防止穿透到底层游戏UI
                        var blocker = _root.transform.Find("QuestBookRaycastBlockerCanvas");
                        if (blocker == null)
                        {
                            var go = new GameObject("QuestBookRaycastBlockerCanvas");
                            go.transform.SetParent(_root.transform, false);
                            var rt = go.AddComponent<RectTransform>();
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                            var bCanvas = go.AddComponent<Canvas>();
                            bCanvas.overrideSorting = true;
                            bCanvas.sortingOrder = 19950; // 低于主Canvas(20000)，高于游戏ESC等
                            go.AddComponent<GraphicRaycaster>();
                            var img = go.AddComponent<Image>();
                            img.color = new Color(0, 0, 0, 0); // 全透明
                            img.raycastTarget = false; // 允许命中实际 UI 元素；阻断底层游戏由排序与禁用FPSInputModule保证

                            // 右键菜单：空白区域也可呼出（GraphBlank）
                            var binder = go.GetComponent<QuestBook.UI.ContextClickBinder>();
                            if (binder != null)
                            {
                                binder.enabled = false; // 拦截Canvas仅用于阻断穿透，不再在此处弹出菜单
                            }
                            Mod.Log?.LogInfo("QuestBookRaycastBlockerCanvas created (sortingOrder=5950).\n");
                        }

                        // 挂载右键诊断（仅一次），帮助定位未触发原因并兜底触发
                        if (_root.GetComponent<RightClickDebug>() == null)
                        {
                            _root.gameObject.AddComponent<RightClickDebug>();
                            Mod.Log?.LogInfo("RightClickDebug attached to UI root.");
                        }

                        // 挂载 CursorGuard（仅一次），在 UI 打开期间每帧巩固 Cursor.visible 与 lockState
                        if (_root.GetComponent<CursorGuard>() == null)
                        {
                            _root.gameObject.AddComponent<CursorGuard>();
                            Mod.Log?.LogInfo("CursorGuard attached to UI root.");
                        }
                    }
                }
            }
        }

        internal static void Open()
        {
            EnsureLoaded();
            if (_root != null)
            {
                _root.SetActive(true);
                if (_data != null)
                {
                    BuildUI();
                }

                _prevCursorVisible = Cursor.visible;
                _prevLockMode = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Mod.Log?.LogInfo("UI opened: Cursor.visible=true, Cursor.lockState=None");

                // 启用 StandaloneInputModule，禁用 FPSInputModule，降低拖拽阈值，确保拖拽/点击由 uGUI 处理
                var es = EventSystem.current;
                if (es != null)
                {
                    _prevDragThreshold = es.pixelDragThreshold;
                    es.pixelDragThreshold = 10;
                    _sim = es.GetComponent<StandaloneInputModule>();
                    if (_sim == null) _sim = es.gameObject.AddComponent<StandaloneInputModule>();
                    _sim.forceModuleActive = true;
                    _sim.enabled = true;
                    _fpsim = es.GetComponent<FPSInputModule>();
                    if (_fpsim != null)
                    {
                        _fpsimPrevEnabled = _fpsim.enabled;
                        _fpsim.enabled = false;
                    }
                    Mod.Log?.LogInfo($"EventSystem configured: pixelDragThreshold={es.pixelDragThreshold}, SIM.enabled={_sim.enabled}, FPSIM.disabled={( _fpsim!=null ? (!_fpsim.enabled).ToString() : "null")} ");
                }

                // 在帧末再巩固一次，避免被其他系统在本帧后半阶段改写
                var once = _root.AddComponent<OneShotAction>();
                once.Action = () =>
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    Mod.Log?.LogInfo("UI opened (end-of-frame reinforce): Cursor.visible=true, Cursor.lockState=None");
                };
            }
        }

        internal static void Close()
        {
            if (_root != null) _root.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = _prevLockMode;
            Mod.Log?.LogInfo("UI closed: Cursor.visible=false, lockState restored");

            // 还原输入模块与拖拽阈值
            var es = EventSystem.current;
            if (es != null && _prevDragThreshold >= 0)
            {
                es.pixelDragThreshold = _prevDragThreshold;
                _prevDragThreshold = -1;
            }
            if (_fpsim != null)
            {
                _fpsim.enabled = _fpsimPrevEnabled;
            }
        }

        internal static void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        private static string GetBundleBasePath()
        {
            return @"F:\\GN-RTS V9.4.2\\game\\QMods\\QuestBook\\Assets\\UI";
        }

        internal static string GetIconsDirectory()
        {
            try
            {
                var uiDir = GetBundleBasePath();
                if (string.IsNullOrEmpty(uiDir)) return null;
                var assetsDir = Path.GetDirectoryName(uiDir); // ...\Assets
                if (string.IsNullOrEmpty(assetsDir)) return null;
                var iconDir = Path.Combine(assetsDir, "Icon");
                return iconDir;
            }
            catch { return null; }
        }

        internal static Sprite LoadSpriteFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return null;
                if (!File.Exists(filePath)) return null;
                var data = File.ReadAllBytes(filePath);
                if (data == null || data.Length == 0) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool loaded = false;
                // Prefer static method UnityEngine.ImageConversion.LoadImage(Texture2D, byte[]) or (Texture2D, byte[], bool)
                try
                {
                    var icType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                               ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine");
                    if (icType != null)
                    {
                        var mi2 = icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
                        if (mi2 != null)
                        {
                            loaded = (bool)mi2.Invoke(null, new object[] { tex, data });
                        }
                        else
                        {
                            var mi3 = icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                            if (mi3 != null)
                            {
                                loaded = (bool)mi3.Invoke(null, new object[] { tex, data, false });
                            }
                        }
                    }
                }
                catch { }
                // Fallback to instance extension compiled as instance method in some environments
                if (!loaded)
                {
                    try
                    {
                        var mi = typeof(Texture2D).GetMethod("LoadImage", new Type[] { typeof(byte[]) });
                        if (mi != null)
                        {
                            loaded = (bool)mi.Invoke(tex, new object[] { data });
                        }
                    }
                    catch { }
                }
                if (!loaded)
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = Path.GetFileNameWithoutExtension(filePath);
                return sp;
            }
            catch { return null; }
        }

        private static void BuildUI()
        {
            if (_chaptersContent == null || _graphPanel == null) return;
            ClearChildren(_chaptersContent);
            _currentChapterIndex = -1;

            for (int i = 0; i < _data.Chapters.Count; i++)
            {
                var ch = _data.Chapters[i];
                var go = UnityEngine.Object.Instantiate(_pfChapterItem, _chaptersContent);
                go.name = $"ChapterItem_{ch.Id}";
                var labelTr = go.transform.Find("Label");
                var txt = labelTr != null ? labelTr.GetComponent<Text>() : null;
                if (txt != null) txt.text = ch.Id;
                var iconTr = go.transform.Find("Icon");
                var iconImg = iconTr != null ? iconTr.GetComponent<Image>() : null;
                if (iconImg != null)
                {
                    var iconPath = GetChapterIconPathOrNull(ch.Id);
                    if (!string.IsNullOrEmpty(iconPath))
                    {
                        var sp = LoadSpriteCached(iconPath);
                        if (sp != null)
                        {
                            iconImg.sprite = sp;
                            iconImg.preserveAspect = true;
                        }
                    }
                }
                var btn = go.GetComponent<Button>();
                int idx = i;
                if (btn != null) btn.onClick.AddListener(() => ShowChapter(idx));

                var binder = go.GetComponent<ContextClickBinder>();
                if (binder == null) binder = go.AddComponent<ContextClickBinder>();
                binder.Kind = MenuKind.ChapterItem;
                binder.ChapterId = ch.Id;
                binder.NodeId = null;
            }

            if (_data.Chapters.Count > 0)
                ShowChapter(0);

            // 章节空白处右键（ChaptersPanel 背景）
            var chaptersPanel = _root.transform.Find("ChaptersPanel");
            if (chaptersPanel != null)
            {
                var img = chaptersPanel.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                var binder = chaptersPanel.gameObject.GetComponent<ContextClickBinder>();
                if (binder == null) binder = chaptersPanel.gameObject.AddComponent<ContextClickBinder>();
                binder.Kind = MenuKind.ChaptersBlank;
                binder.ChapterId = null;
                binder.NodeId = null;
            }
        }

        private static void ShowChapter(int index)
        {
            if (index < 0 || index >= _data.Chapters.Count) return;
            if (_currentChapterPage != null)
            {
                UnityEngine.Object.Destroy(_currentChapterPage);
                _currentChapterPage = null;
            }
            _currentChapterIndex = index;
            var contentParent = _root != null ? _root.transform.Find("GraphPanel/Viewport/ContentRoot") : null;
            var parentForPage = contentParent != null ? contentParent : _graphPanel;
            _currentChapterPage = UnityEngine.Object.Instantiate(_pfChapterPage, parentForPage);
            _currentChapterPage.name = $"ChapterPage_{_data.Chapters[index].Id}";
            var pageRt = _currentChapterPage.GetComponent<RectTransform>();
            if (pageRt != null)
            {
                pageRt.anchorMin = Vector2.zero;
                pageRt.anchorMax = Vector2.one;
                pageRt.offsetMin = Vector2.zero;
                pageRt.offsetMax = Vector2.zero;
            }

            // 优先使用章节页面内部容器，保障每个章节的节点与连线独立
            var contentRoot = _currentChapterPage.transform.Find("ContentRoot") as RectTransform;
            var nodesContainer = _currentChapterPage.transform.Find("ContentRoot/NodesContainer") as RectTransform;
            var edgesContainer = _currentChapterPage.transform.Find("ContentRoot/EdgesContainer") as RectTransform;
            // 兼容旧结构：若章节页内容器缺失，回退到全局容器
            if (contentRoot == null || nodesContainer == null || edgesContainer == null)
            {
                contentRoot = _root != null ? _root.transform.Find("GraphPanel/Viewport/ContentRoot") as RectTransform : null;
                nodesContainer = _root != null ? _root.transform.Find("GraphPanel/Viewport/ContentRoot/NodesContainer") as RectTransform : null;
                edgesContainer = _root != null ? _root.transform.Find("GraphPanel/Viewport/ContentRoot/EdgesContainer") as RectTransform : null;
            }
            if (contentRoot == null || nodesContainer == null || edgesContainer == null) return;

            // 移除此前在 ChapterPage/GraphPanel 上添加的透明 Image 与 GraphBlank Binder（按用户要求改为基于正确层级判定）

            // 任务页面空白处（背景）右键
            var bg = _currentChapterPage.transform.Find("Background");
            if (bg != null)
            {
                var img = bg.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                var binder = bg.gameObject.GetComponent<ContextClickBinder>();
                if (binder == null) binder = bg.gameObject.AddComponent<ContextClickBinder>();
                binder.Kind = MenuKind.GraphBlank;
                binder.ChapterId = _data.Chapters[index].Id;
                binder.NodeId = null;
            }

            // 同步“拦截Canvas”上的右键菜单章节上下文
            var blocker = _root.transform.Find("QuestBookRaycastBlockerCanvas");
            if (blocker != null)
            {
                var binder2 = blocker.GetComponent<ContextClickBinder>();
                if (binder2 != null)
                {
                    binder2.enabled = false; // 不再通过拦截Canvas分发右键菜单
                }
            }

            RenderNodes(nodesContainer);
            RenderEdges(edgesContainer, nodesContainer);
        }

        private static void RenderNodes(RectTransform nodesContainer)
        {
            ClearChildren(nodesContainer);
            var chapter = _data.Chapters[_currentChapterIndex];
            foreach (var node in chapter.Nodes)
            {
                var go = UnityEngine.Object.Instantiate(_pfNodeItem, nodesContainer);
                go.name = $"Node_{node.Id}";
                // 确保节点根有可射线 UI，以便右键命中节点而非落到空白
                var rootImg = go.GetComponent<Image>();
                if (rootImg == null) rootImg = go.AddComponent<Image>();
                if (rootImg != null)
                {
                    if (rootImg.sprite == null)
                    {
                        // 透明占位，既不影响视觉又可被射线命中
                        var c = rootImg.color; rootImg.color = new Color(c.r, c.g, c.b, 0f);
                    }
                    rootImg.raycastTarget = true;
                }
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(node.PosX, -node.PosY);
                }
                var titleTr = go.transform.Find("Title");
                var titleTxt = titleTr != null ? titleTr.GetComponent<Text>() : null;
                if (titleTxt != null) titleTxt.text = string.IsNullOrEmpty(node.Title) ? node.Id : node.Title;

                Image iconImg = null;
                var iconTr = go.transform.Find("Icon");
                if (iconTr != null) iconImg = iconTr.GetComponent<Image>();
                if (iconImg == null) iconImg = go.GetComponent<Image>();
                if (iconImg != null && !string.IsNullOrEmpty(node.Icon))
                {
                    var abs = ResolveAssetsPath(node.Icon);
                    var sp = LoadSpriteCached(abs);
                    if (sp != null)
                    {
                        iconImg.sprite = sp;
                        iconImg.preserveAspect = true;
                        var c2 = iconImg.color; iconImg.color = new Color(c2.r, c2.g, c2.b, 1f);
                    }
                }

                var binder = go.GetComponent<ContextClickBinder>();
                if (binder == null) binder = go.AddComponent<ContextClickBinder>();
                binder.Kind = MenuKind.NodeItem;
                binder.ChapterId = chapter.Id;
                binder.NodeId = node.Id;

                // 挂载长按拖动（仅开发者模式生效）
                var drag = go.GetComponent<QuestBook.UI.NodeDraggable>();
                if (drag == null) drag = go.AddComponent<QuestBook.UI.NodeDraggable>();
                drag.Initialize(chapter.Id, node.Id);
            }
        }

        private static void RenderEdges(RectTransform edgesContainer, RectTransform nodesContainer)
        {
            ClearChildren(edgesContainer);
            var chapter = _data.Chapters[_currentChapterIndex];
            foreach (var e in chapter.Edges)
            {
                var fromNode = nodesContainer.Find($"Node_{e.From}") as RectTransform;
                var toNode = nodesContainer.Find($"Node_{e.To}") as RectTransform;
                if (fromNode == null || toNode == null) continue;

                var line = UnityEngine.Object.Instantiate(_pfEdgeLine, edgesContainer);
                line.name = $"Edge_{e.From}_{e.To}";
                var rt = line.GetComponent<RectTransform>();
                if (rt == null) continue;

                var start = fromNode.anchoredPosition;
                var end = toNode.anchoredPosition;
                var delta = end - start;
                var length = delta.magnitude;
                var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = start;
                rt.sizeDelta = new Vector2(length, 10f);
                rt.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        private static GameObject _createDialogInstance;
        private static GameObject _createTaskDialogInstance;
        internal static void OpenCreateChapterDialog()
        {
            EnsureLoaded();
            if (_pfCreateChapterDialog == null)
            {
                if (_bundle != null && _pfCreateChapterDialog == null)
                {
                    // 尝试多种常见命名/路径，避免资源名与路径不一致导致加载失败
                    string[] candidates = new[]
                    {
                        "CreateChapterDialog",
                        "Assets/Prefab/CreateChapterDialog",
                        "Assets/Prefab/CreateChapterDialog.prefab",
                        "Assets/CreateChapterDialog",
                        "Assets/CreateChapterDialog.prefab",
                        "Assets/Rows/CreateChapterDialog",
                        "Assets/Rows/CreateChapterDialog.prefab",
                    };
                    foreach (var name in candidates)
                    {
                        _pfCreateChapterDialog = _bundle.LoadAsset<GameObject>(name);
                        if (_pfCreateChapterDialog != null)
                        {
                            Mod.Log?.LogInfo($"CreateChapterDialog prefab loaded as: {name}");
                            break;
                        }
                    }
                    if (_pfCreateChapterDialog == null)
                    {
                        try
                        {
                            var all = _bundle.GetAllAssetNames();
                            if (all != null)
                            {
                                foreach (var a in all)
                                {
                                    var lower = a.ToLowerInvariant();
                                    if (lower.EndsWith("/createchapterdialog.prefab") || lower.Contains("/createchapterdialog"))
                                    {
                                        _pfCreateChapterDialog = _bundle.LoadAsset<GameObject>(a);
                                        if (_pfCreateChapterDialog != null)
                                        {
                                            Mod.Log?.LogInfo($"CreateChapterDialog prefab loaded via enumerate: {a}");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                if (_pfCreateChapterDialog == null)
                {
                    Mod.Log?.LogWarning("OpenCreateChapterDialog: prefab missing in AB.");
                    return;
                }
            }
            if (_createDialogInstance == null)
            {
                _createDialogInstance = UnityEngine.Object.Instantiate(_pfCreateChapterDialog, _root != null ? _root.transform : null);
                _createDialogInstance.name = "CreateChapterDialog(Runtime)";
                _createDialogInstance.transform.SetAsLastSibling();
                _createDialogInstance.transform.localScale = Vector3.one;
                _createDialogInstance.transform.localPosition = Vector3.zero;
                var rt = _createDialogInstance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    Mod.Log?.LogWarning("CreateChapterDialog root has no RectTransform; UI may not size correctly.");
                }
            }
            var cv = _createDialogInstance.GetComponent<Canvas>();
            if (cv == null) cv = _createDialogInstance.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.overrideSorting = true;
            if (cv.sortingOrder < 30050) cv.sortingOrder = 30050;
            if (_createDialogInstance.GetComponent<GraphicRaycaster>() == null)
                _createDialogInstance.AddComponent<GraphicRaycaster>();
            var cg = _createDialogInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = _createDialogInstance.AddComponent<CanvasGroup>();
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

            // 规范化所有子 Canvas，避免预制体内部使用了 ScreenSpaceCamera/WorldSpace 导致不可见
            try
            {
                var allCanvas = _createDialogInstance.GetComponentsInChildren<Canvas>(true);
                int baseOrder = 30050;
                for (int i = 0; i < allCanvas.Length; i++)
                {
                    var c = allCanvas[i];
                    c.enabled = true;
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.overrideSorting = true;
                    c.sortingOrder = baseOrder + i; // 保证父先子后，逐层递增
                }
                Mod.Log?.LogInfo($"CreateChapterDialog canvases normalized: count={allCanvas.Length}");
            }
            catch { }

            if (_createDialogInstance.GetComponent<QuestBook.UI.CreateChapterDialogController>() == null)
            {
                _createDialogInstance.AddComponent<QuestBook.UI.CreateChapterDialogController>();
            }
            _createDialogInstance.SetActive(true);
            var backdrop = _createDialogInstance.transform.Find("Backdrop");
            if (backdrop != null) backdrop.gameObject.SetActive(true);
            var panelTr = _createDialogInstance.transform.Find("Panel");
            if (panelTr != null) panelTr.gameObject.SetActive(true);
            // 诊断：输出一级子物体与可见性
            try
            {
                var childCount = _createDialogInstance.transform.childCount;
                Mod.Log?.LogInfo($"CreateChapterDialog opened. children={childCount}");
                for (int i = 0; i < childCount; i++)
                {
                    var ch = _createDialogInstance.transform.GetChild(i);
                    Mod.Log?.LogInfo($" - child[{i}] name={ch.name} activeSelf={ch.gameObject.activeSelf}");
                }
                var panel = _createDialogInstance.transform.Find("Panel") as RectTransform;
                if (panel != null)
                {
                    var r = panel.rect; Mod.Log?.LogInfo($"Panel rect: {r} anchoredPos={panel.anchoredPosition}");
                }
            }
            catch { }
        }

        internal static void CloseCreateChapterDialog()
        {
            if (_createDialogInstance != null)
            {
                UnityEngine.Object.Destroy(_createDialogInstance);
                _createDialogInstance = null;
                Mod.Log?.LogInfo("CreateChapterDialog closed.");
            }
        }

        internal static void OpenCreateTaskDialog(string chapterId)
        {
            EnsureLoaded();
            if (_pfCreateTaskDialog == null)
            {
                if (_bundle != null && _pfCreateTaskDialog == null)
                {
                    string[] candidates = new[]
                    {
                        "CreateTaskDialog",
                        "Assets/Prefab/CreateTaskDialog",
                        "Assets/Prefab/CreateTaskDialog.prefab",
                        "Assets/CreateTaskDialog",
                        "Assets/CreateTaskDialog.prefab",
                        "Assets/Rows/CreateTaskDialog",
                        "Assets/Rows/CreateTaskDialog.prefab",
                    };
                    foreach (var name in candidates)
                    {
                        _pfCreateTaskDialog = _bundle.LoadAsset<GameObject>(name);
                        if (_pfCreateTaskDialog != null)
                        {
                            Mod.Log?.LogInfo($"CreateTaskDialog prefab loaded as: {name}");
                            break;
                        }
                    }
                    if (_pfCreateTaskDialog == null)
                    {
                        try
                        {
                            var all = _bundle.GetAllAssetNames();
                            if (all != null)
                            {
                                foreach (var a in all)
                                {
                                    var lower = a.ToLowerInvariant();
                                    if (lower.EndsWith("/createtaskdialog.prefab") || lower.Contains("/createtaskdialog"))
                                    {
                                        _pfCreateTaskDialog = _bundle.LoadAsset<GameObject>(a);
                                        if (_pfCreateTaskDialog != null)
                                        {
                                            Mod.Log?.LogInfo($"CreateTaskDialog prefab loaded via enumerate: {a}");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                if (_pfCreateTaskDialog == null)
                {
                    Mod.Log?.LogWarning("OpenCreateTaskDialog: prefab missing in AB.");
                    return;
                }
            }
            if (_createTaskDialogInstance == null)
            {
                _createTaskDialogInstance = UnityEngine.Object.Instantiate(_pfCreateTaskDialog, _root != null ? _root.transform : null);
                _createTaskDialogInstance.name = "CreateTaskDialog(Runtime)";
                _createTaskDialogInstance.transform.SetAsLastSibling();
                _createTaskDialogInstance.transform.localScale = Vector3.one;
                _createTaskDialogInstance.transform.localPosition = Vector3.zero;
                var rt = _createTaskDialogInstance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }
            }
            var cv = _createTaskDialogInstance.GetComponent<Canvas>();
            if (cv == null) cv = _createTaskDialogInstance.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.overrideSorting = true;
            if (cv.sortingOrder < 30055) cv.sortingOrder = 30055;
            if (_createTaskDialogInstance.GetComponent<GraphicRaycaster>() == null)
                _createTaskDialogInstance.AddComponent<GraphicRaycaster>();
            var cg = _createTaskDialogInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = _createTaskDialogInstance.AddComponent<CanvasGroup>();
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

            var ctrl = _createTaskDialogInstance.GetComponent<QuestBook.UI.CreateTaskDialogController>();
            if (ctrl == null) ctrl = _createTaskDialogInstance.AddComponent<QuestBook.UI.CreateTaskDialogController>();
            try { ctrl.Initialize(chapterId); } catch { }
            _createTaskDialogInstance.SetActive(true);
            Mod.Log?.LogInfo("CreateTaskDialog opened.");
        }

        internal static void CloseCreateTaskDialog()
        {
            if (_createTaskDialogInstance != null)
            {
                UnityEngine.Object.Destroy(_createTaskDialogInstance);
                _createTaskDialogInstance = null;
                Mod.Log?.LogInfo("CreateTaskDialog closed.");
            }
        }

        internal static void OpenEditTaskDialog(string chapterId, string nodeId)
        {
            if (string.IsNullOrEmpty(chapterId) || string.IsNullOrEmpty(nodeId)) return;
            // 先打开创建对话框（用于复用布局与绑定），随后切换为编辑模式并预填
            OpenCreateTaskDialog(chapterId);
            if (_createTaskDialogInstance == null) return;
            var ctrl = _createTaskDialogInstance.GetComponent<QuestBook.UI.CreateTaskDialogController>();
            if (ctrl == null) return;

            // 收集当前节点与用户元数据
            string title = null, desc = null, iconRel = null;
            bool directUnlock = true, visibleBefore = false;
            int unlockCondIndex = 0, completionTypeIndex = 0, rewardModeIndex = 0, rewardPoolIndex = 0;
            var rewardItems = new List<UserRewardItem>();
            try
            {
                var ch = _data != null ? _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase)) : null;
                var node = ch != null ? ch.Nodes.Find(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase)) : null;
                if (node != null)
                {
                    title = node.Title;
                    desc = node.Description;
                    iconRel = node.Icon;
                    // 从运行时规则推导（若无用户元数据）
                    visibleBefore = string.Equals(node.VisibleRule, "always", StringComparison.OrdinalIgnoreCase);
                    if (string.Equals(node.UnlockRule, "direct", StringComparison.OrdinalIgnoreCase))
                    {
                        directUnlock = true;
                    }
                    else
                    {
                        directUnlock = false;
                        unlockCondIndex = string.Equals(node.UnlockRule, "prereq_all", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    }
                    // 奖励
                    if (node.Rewards != null)
                    {
                        foreach (var r in node.Rewards)
                        {
                            if (r == null || string.IsNullOrEmpty(r.Type)) continue;
                            if (string.Equals(r.Type, "item", StringComparison.OrdinalIgnoreCase))
                            {
                                rewardModeIndex = 0;
                                string iid; int cnt;
                                r.Params.TryGetValue("id", out iid);
                                if (!int.TryParse(r.Params != null && r.Params.ContainsKey("count") ? r.Params["count"] : null, out cnt)) cnt = 1;
                                if (!string.IsNullOrWhiteSpace(iid)) rewardItems.Add(new UserRewardItem { ItemId = iid, Count = cnt });
                            }
                            else if (string.Equals(r.Type, "pool", StringComparison.OrdinalIgnoreCase))
                            {
                                rewardModeIndex = 1;
                                int idx = 0;
                                int.TryParse(r.Params != null && r.Params.ContainsKey("index") ? r.Params["index"] : null, out idx);
                                rewardPoolIndex = idx;
                            }
                        }
                    }
                }
                // 若存在用户元数据，以其为准覆盖
                var ut = _userData != null ? _userData.CreatedTasks.Find(t => string.Equals(t.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase)) : null;
                if (ut != null)
                {
                    if (!string.IsNullOrEmpty(ut.Title)) title = ut.Title;
                    if (!string.IsNullOrEmpty(ut.Description)) desc = ut.Description;
                    directUnlock = ut.DirectUnlock;
                    visibleBefore = ut.VisibleBeforeUnlock;
                    unlockCondIndex = ut.UnlockConditionIndex;
                    if (!string.IsNullOrEmpty(ut.IconPath)) iconRel = ut.IconPath;
                    completionTypeIndex = ut.CompletionTypeIndex;
                    rewardModeIndex = ut.RewardModeIndex;
                    rewardPoolIndex = ut.RewardPoolIndex;
                    if (ut.RewardItems != null && ut.RewardItems.Count > 0)
                    {
                        rewardItems = new List<UserRewardItem>(ut.RewardItems);
                    }
                }
            }
            catch { }

            // 解析图标绝对路径并加载预览
            string iconAbs = null; Sprite iconSp = null;
            try
            {
                if (!string.IsNullOrEmpty(iconRel))
                {
                    iconAbs = ResolveAssetsPath(iconRel);
                    iconSp = LoadSpriteFromFile(iconAbs);
                }
            }
            catch { }

            // 解析前置：优先使用用户数据；若缺失则从连线反推
            List<string> prereqIds = new List<string>();
            try
            {
                var ut2 = _userData != null ? _userData.CreatedTasks.Find(t => string.Equals(t.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase)) : null;
                if (ut2 != null && ut2.PrereqNodeIds != null && ut2.PrereqNodeIds.Count > 0)
                {
                    prereqIds.AddRange(ut2.PrereqNodeIds);
                }
                else
                {
                    var ch2 = _data != null ? _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase)) : null;
                    if (ch2 != null && ch2.Edges != null)
                    {
                        for (int i = 0; i < ch2.Edges.Count; i++)
                        {
                            var e = ch2.Edges[i];
                            if (e != null && string.Equals(e.To, nodeId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.From))
                                prereqIds.Add(e.From);
                        }
                    }
                }
            }
            catch { }

            try
            {
                try { ctrl.Initialize(chapterId); } catch { }
                ctrl.EnterEditMode(nodeId,
                    title ?? string.Empty,
                    desc ?? string.Empty,
                    directUnlock,
                    visibleBefore,
                    unlockCondIndex,
                    iconAbs,
                    iconSp,
                    completionTypeIndex,
                    rewardModeIndex,
                    rewardItems,
                    rewardPoolIndex,
                    prereqIds);
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"OpenEditTaskDialog EnterEditMode error: {e.Message}");
            }
        }

        internal static bool TryCreateChapter(string id, string name, int order, bool visibleByDefault, out string finalId)
        {
            EnsureLoaded();
            if (_data == null) _data = new QuestBookData();
            var baseId = string.IsNullOrWhiteSpace(id) ? "chapter" : MakeSafeId(id);
            if (string.IsNullOrWhiteSpace(baseId)) baseId = "chapter";
            finalId = MakeUniqueChapterId(baseId);
            var ch = new Chapter { Id = finalId, Name = name, Order = order, VisibleByDefault = visibleByDefault };
            _data.Chapters.Add(ch);
            _data.Chapters.Sort((a, b) => a.Order.CompareTo(b.Order));
            if (IsOpen)
            {
                BuildUI();
                var targetIdLocal = finalId; // 避免在 lambda 中直接捕获 out 参数
                var idx = _data.Chapters.FindIndex(c => string.Equals(c.Id, targetIdLocal, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) ShowChapter(idx);
            }
            Mod.Log?.LogInfo($"Chapter created: id={finalId} name={name} order={order} visible={visibleByDefault}");
            return true;
        }

        private static string MakeSafeId(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else if (char.IsWhiteSpace(c)) sb.Append('_');
            }
            return sb.ToString();
        }

        private static string MakeUniqueChapterId(string baseId)
        {
            string id = baseId;
            int n = 1;
            while (_data.Chapters.Exists(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                id = baseId + "_" + n.ToString();
                n++;
            }
            return id;
        }

        private static string MakeUniqueNodeId(Chapter chapter, string baseId)
        {
            if (chapter == null) return baseId;
            string id = baseId;
            int n = 1;
            while (chapter.Nodes.Exists(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                id = baseId + "_" + n.ToString();
                n++;
            }
            return id;
        }

        internal static bool TryCreateTask(
            string chapterId,
            string title,
            string description,
            bool directUnlock,
            bool visibleBeforeUnlock,
            int unlockCondIndex,
            string iconPath,
            int completionTypeIndex,
            int rewardModeIndex,
            List<string> prereqNodeIds,
            List<UserRewardItem> rewardItems,
            int rewardPoolIndex,
            out string finalNodeId)
        {
            finalNodeId = null;
            EnsureLoaded();
            if (_data == null || string.IsNullOrEmpty(chapterId)) return false;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (ch == null) return false;

            var baseId = string.IsNullOrWhiteSpace(title) ? "task" : MakeSafeId(title);
            if (string.IsNullOrWhiteSpace(baseId)) baseId = "task";
            finalNodeId = MakeUniqueNodeId(ch, baseId);

            // 计算新建节点初始位置（来自最近一次右键的屏幕坐标 -> NodesContainer 局部坐标）
            float posX = 0f, posY = 0f;
            try
            {
                EnsurePendingLocalFromScreen();
                if (_pendingCreateNodeLocalPos.HasValue)
                {
                    var lp = _pendingCreateNodeLocalPos.Value;
                    posX = lp.x;
                    posY = -lp.y; // 数据坐标：PosY 与 anchoredPosition.y 取反
                }
            }
            catch { }

            var node = new Node
            {
                Id = finalNodeId,
                Title = title,
                Description = description,
                Icon = string.IsNullOrEmpty(iconPath) ? null : ToRelativeAssetsPath(iconPath),
                PosX = posX,
                PosY = posY,
                Resettable = true,
                VisibleRule = visibleBeforeUnlock ? "always" : "after_unlock",
                UnlockRule = directUnlock ? "direct" : (unlockCondIndex == 0 ? "prereq_any" : "prereq_all"),
            };

            // 基础奖励映射（后续可扩展为奖励池等）
            if (rewardModeIndex == 0 && rewardItems != null)
            {
                foreach (var it in rewardItems)
                {
                    if (it == null) continue;
                    if (string.IsNullOrWhiteSpace(it.ItemId)) continue;
                    var rew = new Reward { Type = "item" };
                    rew.Params["id"] = it.ItemId;
                    rew.Params["count"] = it.Count.ToString();
                    node.Rewards.Add(rew);
                }
            }
            else if (rewardModeIndex == 1)
            {
                // 预留：奖励池占位
                var rew = new Reward { Type = "pool" };
                rew.Params["index"] = rewardPoolIndex.ToString();
                node.Rewards.Add(rew);
            }

            ch.Nodes.Add(node);

            // 前置边：为选择的前置任务生成边（From=前置, To=当前）
            if (prereqNodeIds != null)
            {
                var toIdLocal = finalNodeId; // 避免在 lambda 中直接使用 out 参数
                for (int i = 0; i < prereqNodeIds.Count; i++)
                {
                    var pid = prereqNodeIds[i];
                    if (string.IsNullOrEmpty(pid)) continue;
                    if (!ch.Nodes.Exists(n => string.Equals(n.Id, pid, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!ch.Edges.Exists(e => string.Equals(e.From, pid, StringComparison.OrdinalIgnoreCase) && string.Equals(e.To, toIdLocal, StringComparison.OrdinalIgnoreCase)))
                    {
                        ch.Edges.Add(new Edge { From = pid, To = toIdLocal, RequireComplete = (unlockCondIndex == 1), HideUntilComplete = false });
                    }
                }
            }

            // 用户数据持久化（CreatedTasks）
            if (_userData == null) _userData = new UserData();
            var ut = new UserCreatedTask
            {
                ChapterId = chapterId,
                Id = finalNodeId,
                Title = title,
                Description = description,
                DirectUnlock = directUnlock,
                VisibleBeforeUnlock = visibleBeforeUnlock,
                UnlockConditionIndex = unlockCondIndex,
                IconPath = string.IsNullOrEmpty(iconPath) ? null : ToRelativeAssetsPath(iconPath),
                CompletionTypeIndex = completionTypeIndex,
                RewardModeIndex = rewardModeIndex,
                RewardPoolIndex = rewardPoolIndex,
                PosX = posX,
                PosY = posY,
                PrereqNodeIds = prereqNodeIds != null ? new List<string>(prereqNodeIds) : new List<string>(),
                RewardItems = rewardItems ?? new List<UserRewardItem>()
            };
            _userData.CreatedTasks.Add(ut);
            try { UserDataRepository.Save(_userData); } catch { }

            if (IsOpen)
            {
                RebuildChaptersAndSelect(chapterId);
            }
            Mod.Log?.LogInfo($"Task created: chapter={chapterId} nodeId={finalNodeId} title={title}");
            return true;
        }

        internal static bool UpdateTask(
            string chapterId,
            string nodeId,
            string title,
            string description,
            bool directUnlock,
            bool visibleBeforeUnlock,
            int unlockCondIndex,
            string iconPathAbs,
            int completionTypeIndex,
            int rewardModeIndex,
            List<string> prereqNodeIds,
            List<UserRewardItem> rewardItems,
            int rewardPoolIndex)
        {
            EnsureLoaded();
            if (_data == null || string.IsNullOrEmpty(chapterId) || string.IsNullOrEmpty(nodeId)) return false;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (ch == null) return false;
            var node = ch.Nodes.Find(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            // 更新运行时节点
            node.Title = title;
            node.Description = description;
            node.Icon = string.IsNullOrEmpty(iconPathAbs) ? null : ToRelativeAssetsPath(iconPathAbs);
            node.VisibleRule = visibleBeforeUnlock ? "always" : "after_unlock";
            node.UnlockRule = directUnlock ? "direct" : (unlockCondIndex == 0 ? "prereq_any" : "prereq_all");
            node.Rewards.Clear();
            if (rewardModeIndex == 0 && rewardItems != null)
            {
                foreach (var it in rewardItems)
                {
                    if (it == null || string.IsNullOrWhiteSpace(it.ItemId)) continue;
                    var rew = new Reward { Type = "item" };
                    rew.Params["id"] = it.ItemId;
                    rew.Params["count"] = Math.Max(1, it.Count).ToString();
                    node.Rewards.Add(rew);
                }
            }
            else if (rewardModeIndex == 1)
            {
                var rew = new Reward { Type = "pool" };
                rew.Params["index"] = rewardPoolIndex.ToString();
                node.Rewards.Add(rew);
            }

            // 更新前置边：先移除所有指向该节点的边，再根据新的多选添加
            if (ch.Edges != null)
            {
                ch.Edges.RemoveAll(e => string.Equals(e.To, nodeId, StringComparison.OrdinalIgnoreCase));
            }
            if (prereqNodeIds != null)
            {
                for (int i = 0; i < prereqNodeIds.Count; i++)
                {
                    var pid = prereqNodeIds[i];
                    if (string.IsNullOrEmpty(pid)) continue;
                    if (!ch.Nodes.Exists(n => string.Equals(n.Id, pid, StringComparison.OrdinalIgnoreCase))) continue;
                    ch.Edges.Add(new Edge { From = pid, To = nodeId, RequireComplete = (unlockCondIndex == 1), HideUntilComplete = false });
                }
            }

            // 更新/创建用户元数据并保存
            if (_userData == null) _userData = new UserData();
            var ut = _userData.CreatedTasks.Find(t => string.Equals(t.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (ut == null)
            {
                ut = new UserCreatedTask { ChapterId = chapterId, Id = nodeId };
                _userData.CreatedTasks.Add(ut);
            }
            ut.Title = title;
            ut.Description = description;
            ut.DirectUnlock = directUnlock;
            ut.VisibleBeforeUnlock = visibleBeforeUnlock;
            ut.UnlockConditionIndex = unlockCondIndex;
            ut.IconPath = string.IsNullOrEmpty(iconPathAbs) ? null : ToRelativeAssetsPath(iconPathAbs);
            ut.CompletionTypeIndex = completionTypeIndex;
            ut.RewardModeIndex = rewardModeIndex;
            ut.RewardPoolIndex = rewardPoolIndex;
            ut.PrereqNodeIds = prereqNodeIds != null ? new List<string>(prereqNodeIds) : new List<string>();
            ut.RewardItems = rewardItems ?? new List<UserRewardItem>();
            try { UserDataRepository.Save(_userData); } catch { }

            if (IsOpen)
            {
                RebuildChaptersAndSelect(chapterId);
            }
            Mod.Log?.LogInfo($"Task updated: chapter={chapterId} nodeId={nodeId} title={title}");
            return true;
        }

        internal static void DeleteTask(string chapterId, string nodeId)
        {
            if (_data == null || string.IsNullOrEmpty(chapterId) || string.IsNullOrEmpty(nodeId)) return;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (ch == null) return;
            // 删除相关连线
            ch.Edges.RemoveAll(e => string.Equals(e.From, nodeId, StringComparison.OrdinalIgnoreCase) || string.Equals(e.To, nodeId, StringComparison.OrdinalIgnoreCase));
            // 删除节点
            int removed = ch.Nodes.RemoveAll(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));

            // 删除用户元数据
            try
            {
                if (_userData != null)
                {
                    _userData.CreatedTasks.RemoveAll(t => string.Equals(t.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                    UserDataRepository.Save(_userData);
                }
            }
            catch { }

            if (removed > 0 && IsOpen)
            {
                RebuildChaptersAndSelect(chapterId);
            }
            Mod.Log?.LogInfo($"Task deleted: chapter={chapterId} nodeId={nodeId} removed={removed}");
        }
        internal static void SaveChapterVisual(string chapterId, string iconPath, string backgroundPath)
        {
            if (string.IsNullOrEmpty(chapterId)) return;
            if (_userData == null) _userData = new UserData();
            var st = _userData.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (st == null)
            {
                st = new UserChapterState { Id = chapterId };
                _userData.Chapters.Add(st);
            }
            if (!string.IsNullOrEmpty(iconPath)) st.IconPath = ToRelativeAssetsPath(iconPath);
            if (!string.IsNullOrEmpty(backgroundPath)) st.BackgroundPath = ToRelativeAssetsPath(backgroundPath);
            try { UserDataRepository.Save(_userData); } catch { }
        }

        private static string GetChapterIconPathOrNull(string chapterId)
        {
            try
            {
                var st = _userData != null ? _userData.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase)) : null;
                if (st == null || string.IsNullOrEmpty(st.IconPath)) return null;
                return ResolveAssetsPath(st.IconPath);
            }
            catch { return null; }
        }

        private static string GetChapterBackgroundPathOrNull(string chapterId)
        {
            try
            {
                var st = _userData != null ? _userData.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase)) : null;
                if (st == null || string.IsNullOrEmpty(st.BackgroundPath)) return null;
                return ResolveAssetsPath(st.BackgroundPath);
            }
            catch { return null; }
        }

        private static string ToRelativeAssetsPath(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return filePath;
                var baseDir = Paths.AssetsDir;
                var full = Path.GetFullPath(filePath);
                var baseFull = Path.GetFullPath(baseDir);
                if (full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = full.Substring(baseFull.Length).TrimStart('\\', '/');
                    return rel.Replace('/', '\\');
                }
                return filePath;
            }
            catch { return filePath; }
        }

        private static string ResolveAssetsPath(string maybeRelative)
        {
            try
            {
                if (string.IsNullOrEmpty(maybeRelative)) return maybeRelative;
                if (Path.IsPathRooted(maybeRelative)) return maybeRelative;
                return Path.Combine(Paths.AssetsDir, maybeRelative);
            }
            catch { return maybeRelative; }
        }

        internal static string ResolveAssetsPathPublic(string maybeRelative)
        {
            return ResolveAssetsPath(maybeRelative);
        }

        internal static void RebuildChaptersAndSelect(string chapterId)
        {
            if (_data == null) return;
            if (!IsOpen) return;
            BuildUI();
            if (!string.IsNullOrEmpty(chapterId))
            {
                var idx = _data.Chapters.FindIndex(c => c.Id == chapterId);
                if (idx >= 0) ShowChapter(idx);
            }
        }

        internal static int GetChapterOrder(string chapterId)
        {
            if (_data == null || string.IsNullOrEmpty(chapterId)) return 0;
            var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            return ch != null ? ch.Order : 0;
        }

        internal static void OpenEditChapterDialog(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return;
            OpenCreateChapterDialog();
            if (_createDialogInstance == null) return;
            var ctrl = _createDialogInstance.GetComponent<QuestBook.UI.CreateChapterDialogController>();
            if (ctrl == null) return;

            // 汇总元数据（描述、可见性、顺序）与可视资源（图标/背景）
            string description = null;
            bool visible = true;
            int order = 0;
            try
            {
                order = GetChapterOrder(chapterId);
                if (_userData != null)
                {
                    var meta = _userData.CreatedChapters.Find(m => string.Equals(m.Id, chapterId, StringComparison.OrdinalIgnoreCase));
                    if (meta != null)
                    {
                        if (!string.IsNullOrEmpty(meta.Description)) description = meta.Description;
                        visible = meta.VisibleByDefault;
                        if (meta.Order != 0) order = meta.Order;
                    }
                }
                // 若用户数据缺省，可回落到配置中的 VisibleByDefault
                if (_data != null)
                {
                    var ch = _data.Chapters.Find(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
                    if (ch != null)
                    {
                        visible = ch.VisibleByDefault;
                        if (order == 0) order = ch.Order;
                    }
                }
            }
            catch { }

            string iconPath = GetChapterIconPathOrNull(chapterId);
            string bgPath = GetChapterBackgroundPathOrNull(chapterId);

            // 进入编辑模式并预填
            try
            {
                ctrl.InitializeForEdit(chapterId, description, visible, iconPath, bgPath, order);
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"OpenEditChapterDialog InitializeForEdit error: {e.Message}");
            }
        }

        internal static void DeleteChapter(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId) || _data == null) return;
            int idx = _data.Chapters.FindIndex(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return;
            int deletedOrder = _data.Chapters[idx].Order;

            // 从主数据删除
            _data.Chapters.RemoveAt(idx);

            // 顺移后续章节的排序值（将 Order 大于被删章节的项减 1）
            for (int i = 0; i < _data.Chapters.Count; i++)
            {
                if (_data.Chapters[i].Order > deletedOrder)
                {
                    _data.Chapters[i].Order -= 1;
                }
            }

            // 从用户数据删除对应状态与元数据
            try
            {
                if (_userData != null)
                {
                    _userData.Chapters.RemoveAll(c => string.Equals(c.Id, chapterId, StringComparison.OrdinalIgnoreCase));

                    // 顺移用户创建章节的 Order，并删除被删章节的元数据
                    for (int i = _userData.CreatedChapters.Count - 1; i >= 0; i--)
                    {
                        var meta = _userData.CreatedChapters[i];
                        if (meta == null) continue;
                        if (string.Equals(meta.Id, chapterId, StringComparison.OrdinalIgnoreCase))
                        {
                            _userData.CreatedChapters.RemoveAt(i);
                            continue;
                        }
                        if (meta.Order > deletedOrder)
                        {
                            meta.Order -= 1;
                        }
                    }
                    UserDataRepository.Save(_userData);
                }
            }
            catch { }

            // 重建列表并选择合适索引（下方序号自然上移）
            if (IsOpen)
            {
                BuildUI();
                int choose = Mathf.Clamp(idx, 0, _data.Chapters.Count - 1);
                if (_data.Chapters.Count > 0) ShowChapter(choose);
            }
            Mod.Log?.LogInfo($"Chapter deleted: id={chapterId} atIdx={idx}");
        }

        internal static GameObject LoadFromBundle(string assetName)
        {
            EnsureLoaded();
            if (_bundle == null || string.IsNullOrEmpty(assetName)) return null;
            try
            {
                var go = _bundle.LoadAsset<GameObject>(assetName);
                if (go != null) return go;
                var all = _bundle.GetAllAssetNames();
                if (all != null)
                {
                    var needle = System.IO.Path.GetFileNameWithoutExtension(assetName).ToLowerInvariant();
                    foreach (var a in all)
                    {
                        var low = a.ToLowerInvariant();
                        if (low.EndsWith("/" + needle + ".prefab") || low.Contains("/" + needle))
                        {
                            go = _bundle.LoadAsset<GameObject>(a);
                            if (go != null) return go;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
