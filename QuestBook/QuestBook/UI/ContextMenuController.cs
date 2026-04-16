using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QuestBook.UI
{
    internal enum MenuKind
    {
        ChaptersBlank,
        ChapterItem,
        GraphBlank,
        NodeItem
    }

    internal static class ContextMenuController
    {
        private static GameObject _pfContextMenu;
        private static GameObject _pfMenuButtonItem;
        private static Transform _uiRoot;
        private static GameObject _menuInstance;
        private static Transform _itemsRoot;
        private static MenuKind _currentKind;
        private static string _currentChapterId;
        private static string _currentNodeId;
        private static Vector2 _lastScreenPos;

        internal static void Initialize(GameObject pfContextMenu, GameObject pfMenuButtonItem, Transform uiRoot)
        {
            _pfContextMenu = pfContextMenu;
            _pfMenuButtonItem = pfMenuButtonItem;
            _uiRoot = uiRoot;
            if (_menuInstance == null && _pfContextMenu != null && _uiRoot != null)
            {
                _menuInstance = UnityEngine.Object.Instantiate(_pfContextMenu, _uiRoot);
                _menuInstance.name = "ContextMenu";
                _itemsRoot = _menuInstance.transform.Find("Items");
                if (_itemsRoot == null)
                {
                    Mod.Log?.LogWarning("ContextMenu Initialize: 'Items' transform not found. Fallback to menu root.");
                    _itemsRoot = _menuInstance.transform;
                }
                // 固定 Items 的锚点/枢轴为左上，以确保与菜单根背景的相对位置稳定
                var itemsRt0 = _itemsRoot as RectTransform;
                if (itemsRt0 != null)
                {
                    itemsRt0.anchorMin = new Vector2(0f, 1f);
                    itemsRt0.anchorMax = new Vector2(0f, 1f);
                    itemsRt0.pivot = new Vector2(0f, 1f);
                    itemsRt0.anchoredPosition = Vector2.zero;
                }
                // 兜底：为菜单根添加黑色背景，根的尺寸由 Position 中根据 Items 内容计算并设置
                var rootBg = _menuInstance.GetComponent<Image>();
                if (rootBg == null) rootBg = _menuInstance.AddComponent<Image>();
                rootBg.color = new Color(0f, 0f, 0f, 0.85f);
                rootBg.raycastTarget = false;

                // 兜底：为Items添加布局组件，确保子项竖向排列且有间距与内边距
                var itemsGo = _itemsRoot.gameObject;
                var vlg = itemsGo.GetComponent<VerticalLayoutGroup>();
                if (vlg == null)
                {
                    vlg = itemsGo.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 10f;
                    vlg.padding = new RectOffset(12, 12, 10, 10);
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.childAlignment = TextAnchor.UpperLeft;
                }

                var csf = itemsGo.GetComponent<ContentSizeFitter>();
                if (csf == null)
                {
                    csf = itemsGo.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                var cg = _menuInstance.GetComponent<CanvasGroup>();
                if (cg == null) cg = _menuInstance.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // 确保菜单位于最上层渲染，避免被挡住
                var cv = _menuInstance.GetComponent<Canvas>();
                var hadCanvas = cv != null;
                if (cv == null) cv = _menuInstance.AddComponent<Canvas>();
                // 记录修改前
                Mod.Log?.LogInfo($"ContextMenu Initialize (before): hadCanvas={hadCanvas}, renderMode={cv.renderMode}, overrideSorting={cv.overrideSorting}, order={cv.sortingOrder}");
                // 强制顶层渲染
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.overrideSorting = true;
                cv.sortingOrder = 30000;
                // 记录修改后
                Mod.Log?.LogInfo($"ContextMenu Initialize (after): renderMode={cv.renderMode}, overrideSorting={cv.overrideSorting}, order={cv.sortingOrder}");
                var gr = _menuInstance.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (gr == null) gr = _menuInstance.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Mod.Log?.LogInfo($"ContextMenu Initialize: Canvas overrideSorting={cv.overrideSorting}, order={cv.sortingOrder}");
                _menuInstance.SetActive(false);
                Mod.Log?.LogInfo("ContextMenu Initialize: menu instantiated and hidden.");
            }
        }

        internal static void Hide()
        {
            if (_menuInstance != null) _menuInstance.SetActive(false);
        }

        internal static void ShowAt(Vector2 screenPos, MenuKind kind, string chapterId = null, string nodeId = null)
        {
            if (_menuInstance == null)
            {
                Mod.Log?.LogWarning("ContextMenu ShowAt: _menuInstance is null (not initialized)");
                return;
            }
            if (_itemsRoot == null)
            {
                Mod.Log?.LogWarning("ContextMenu ShowAt: _itemsRoot is null");
                return;
            }
            if (_pfMenuButtonItem == null)
            {
                Mod.Log?.LogWarning("ContextMenu ShowAt: _pfMenuButtonItem is null");
                return;
            }
            _currentKind = kind;
            _currentChapterId = chapterId;
            _currentNodeId = nodeId;
            _lastScreenPos = screenPos;
            ClearChildren(_itemsRoot);
            var entries = BuildEntries(kind);
            foreach (var e in entries)
            {
                var btn = UnityEngine.Object.Instantiate(_pfMenuButtonItem, _itemsRoot);
                btn.name = e.Id;
                var labelTr = btn.transform.Find("Label");
                var txt = labelTr != null ? labelTr.GetComponent<Text>() : null;
                if (txt != null) txt.text = e.Text;
                // 恢复按钮行高保障，避免文本挤压
                var le = btn.GetComponent<LayoutElement>();
                if (le == null) le = btn.AddComponent<LayoutElement>();
                le.minHeight = Mathf.Max(le.minHeight, 36f);
                le.preferredHeight = Mathf.Max(le.preferredHeight, 40f);
                if (le.preferredWidth <= 0f) le.preferredWidth = 220f;
                le.flexibleWidth = 0f;
                var b = btn.GetComponent<Button>();
                var localId = e.Id;
                if (b != null)
                {
                    b.interactable = !e.Disabled;
                    b.onClick.AddListener(() => OnEntryClicked(localId));
                }
                if (txt != null && e.Disabled)
                {
                    var c = txt.color; txt.color = new Color(c.r, c.g, c.b, 0.5f);
                }
            }

            // 先激活，再强制刷新布局，避免未激活状态下首选尺寸为 0
            _menuInstance.SetActive(true);
            Canvas.ForceUpdateCanvases();

            // 强制重建一次布局（Items 与 菜单根），便于获取正确 rect 尺寸
            var itemsRt = _itemsRoot as RectTransform;
            if (itemsRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRt);
            var menuRt = _menuInstance.GetComponent<RectTransform>();
            if (menuRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(menuRt);
            Canvas.ForceUpdateCanvases();

            Position(screenPos);
            _menuInstance.transform.SetAsLastSibling();
            // 再次兜底：显示前强制开启交互与顶层排序
            var cg = _menuInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = _menuInstance.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            var cv = _menuInstance.GetComponent<Canvas>();
            if (cv == null) cv = _menuInstance.AddComponent<Canvas>();
            Mod.Log?.LogInfo($"ContextMenu ShowAt (before): renderMode={cv.renderMode}, overrideSorting={cv.overrideSorting}, order={cv.sortingOrder}");
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.overrideSorting = true;
            cv.sortingOrder = 30000;
            Mod.Log?.LogInfo($"ContextMenu ShowAt (after): renderMode={cv.renderMode}, overrideSorting={cv.overrideSorting}, order={cv.sortingOrder}");
            _menuInstance.transform.localScale = Vector3.one;
            Mod.Log?.LogInfo($"ContextMenu ShowAt: activeSelf={_menuInstance.activeSelf}, alpha={cg.alpha}, interactable={cg.interactable}, blocks={cg.blocksRaycasts}");
            Mod.Log?.LogInfo($"ContextMenu show: kind={kind} entries={entries.Count} at={screenPos}");
        }

        private static void Position(Vector2 screenPos)
        {
            var rt = _menuInstance.GetComponent<RectTransform>();
            if (rt == null)
            {
                Mod.Log?.LogWarning("ContextMenu Position: RectTransform not found on menu instance.");
                return;
            }
            // 固定锚点到父左上角，默认 pivot 在左上（后续可根据空间翻转）
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var parentRt = _uiRoot as RectTransform;
            if (parentRt != null)
            {
                // 使用根 Canvas 进行屏幕->局部的两段转换，避免嵌套缩放造成的偏差
                Camera cam = null;
                Vector2 localPoint;
                var parentCanvas = parentRt.GetComponentInParent<Canvas>();
                var rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
                RectTransform rootRt = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : null;
                if (rootRt != null)
                {
                    if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        // 直接以父 RectTransform 为参考系计算 anchoredPosition，统一坐标系，避免下半屏镜像
                        Vector2 parentLocal;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPos, null, out parentLocal))
                        {
                            var pRect = parentRt.rect;
                            var anchored = new Vector2(parentLocal.x - pRect.xMin, parentLocal.y - pRect.yMax);
                            rt.anchoredPosition = anchored;
                        }
                        else
                        {
                            rt.position = screenPos;
                        }
                    }
                    else
                    {
                        if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                            cam = rootCanvas.worldCamera;
                        Vector2 rootLocal;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screenPos, cam, out rootLocal))
                        {
                            Vector3 world = rootRt.TransformPoint(rootLocal);
                            Vector3 local3 = parentRt.InverseTransformPoint(world);
                            localPoint = new Vector2(local3.x, local3.y);
                            // 将父本地坐标(localPoint)转换为以锚点(0,1)为原点的 anchoredPosition
                            var pRect = parentRt.rect;
                            var anchored = new Vector2(localPoint.x - pRect.xMin, localPoint.y - pRect.yMax);
                            rt.anchoredPosition = anchored;
                        }
                        else
                        {
                            rt.position = screenPos;
                        }
                    }
                }
                else
                {
                    // 无根 Canvas 时退回父 Rect 的直接换算
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPos, cam, out localPoint))
                    {
                        var pRect = parentRt.rect;
                        var anchored = new Vector2(localPoint.x - pRect.xMin, localPoint.y - pRect.yMax);
                        rt.anchoredPosition = anchored;
                    }
                    else
                        rt.position = screenPos;
                }

                // 显式遍历子项，计算 Items 的首选尺寸，设置菜单根 sizeDelta，确保黑底覆盖文本
                var itemsRt = _itemsRoot as RectTransform;
                float prefW = 0f, prefH = 0f;
                var vlg2 = _itemsRoot.GetComponent<VerticalLayoutGroup>();
                var padH = vlg2 != null ? (vlg2.padding != null ? (vlg2.padding.left + vlg2.padding.right) : 0) : 0;
                var padV = vlg2 != null ? (vlg2.padding != null ? (vlg2.padding.top + vlg2.padding.bottom) : 0) : 0;
                var spacing = vlg2 != null ? vlg2.spacing : 0f;
                int activeCount = 0;
                for (int i = 0; i < _itemsRoot.childCount; i++)
                {
                    var ch = _itemsRoot.GetChild(i) as RectTransform;
                    if (ch == null || !ch.gameObject.activeInHierarchy) continue;
                    activeCount++;
                    float cw = LayoutUtility.GetPreferredWidth(ch);
                    float chh = LayoutUtility.GetPreferredHeight(ch);
                    if (cw > prefW) prefW = cw;
                    prefH += chh;
                }
                if (activeCount > 1) prefH += spacing * (activeCount - 1);
                prefW += padH;
                prefH += padV;
                // 适度外沿留白
                float padX = 12f;
                float padY = 12f;
                var size = new Vector2(Mathf.Max(24f, Mathf.Ceil(prefW + padX)), Mathf.Max(24f, Mathf.Ceil(prefH + padY)));
                rt.sizeDelta = size;

                // 按用户要求：保持左上角对齐光标，进行水平与垂直钳制（不翻转 pivot）
                var pRect2 = parentRt.rect; // 父本地坐标
                var pos = rt.anchoredPosition; // 基于锚点(0,1)的坐标：原点在父左上
                float pWidth = pRect2.xMax - pRect2.xMin;
                float pHeight = pRect2.yMax - pRect2.yMin;
                // 水平范围 [0, pWidth - size.x]
                float maxX = Mathf.Max(0f, pWidth - size.x);
                pos.x = Mathf.Clamp(pos.x, 0f, maxX);
                // 纵向范围 [-(pHeight - size.y), 0]，保持左上对齐且不出屏幕
                float minY = Mathf.Min(0f, size.y - pHeight); // 注意：锚点在上，向下为负值
                pos.y = Mathf.Clamp(pos.y, minY, 0f);

                rt.pivot = new Vector2(0f, 1f); // 固定左上
                rt.anchoredPosition = pos;
                Mod.Log?.LogInfo($"ContextMenu Position: itemsPref=({prefW},{prefH}), size={size}, pivot={rt.pivot}, parentRectTLBR=([0,0]-[{pWidth},{-pHeight}]), clampX=[0,{maxX}], clampY=[{minY},0], anchoredPos(afterClamp)={pos}");
            }
            else
            {
                rt.position = screenPos;
            }
        }

        private struct Entry
        {
            public string Id;
            public string Text;
            public bool Disabled;
        }

        private static List<Entry> BuildEntries(MenuKind kind)
        {
            var list = new List<Entry>();
            if (kind == MenuKind.ChaptersBlank)
            {
                list.Add(new Entry { Id = "CreateChapter", Text = "新建章节" });
            }
            else if (kind == MenuKind.ChapterItem)
            {
                list.Add(new Entry { Id = "EditChapter", Text = "修改章节信息" });
                list.Add(new Entry { Id = "DeleteChapter", Text = "删除章节" });
                list.Add(new Entry { Id = "ResetChapterProgress", Text = "重置章节进度", Disabled = true });
                list.Add(new Entry { Id = "CompleteAllChapterTasks", Text = "立即完成章节全部任务", Disabled = true });
            }
            else if (kind == MenuKind.GraphBlank)
            {
                list.Add(new Entry { Id = "CreateTaskNode", Text = "新建任务节点" });
                list.Add(new Entry { Id = "InsertText", Text = "插入文本" });
                list.Add(new Entry { Id = "InsertImage", Text = "插入图片" });
                list.Add(new Entry { Id = "CreateRewardPool", Text = "新建奖励池" });
                list.Add(new Entry { Id = "PasteTaskAsNew", Text = "将剪切板的任务以新任务创建" });
            }
            else if (kind == MenuKind.NodeItem)
            {
                list.Add(new Entry { Id = "EditTask", Text = "修改任务信息" });
                list.Add(new Entry { Id = "DeleteTask", Text = "删除任务" });
                list.Add(new Entry { Id = "CompleteTask", Text = "立即完成当前任务" });
                list.Add(new Entry { Id = "ResetTaskProgress", Text = "重置当前任务进度" });
                list.Add(new Entry { Id = "CopyTaskToClipboard", Text = "将此任务复制到剪切板" });
            }
            return list;
        }

        private static void OnEntryClicked(string id)
        {
            Mod.Log?.LogInfo($"Menu clicked: kind={_currentKind} chapter={_currentChapterId} node={_currentNodeId} id={id}");
            if (id == "CreateChapter")
            {
                QuestBook.UIManager.OpenCreateChapterDialog();
                Hide();
                return;
            }
            if (id == "EditChapter")
            {
                if (!string.IsNullOrEmpty(_currentChapterId))
                {
                    QuestBook.UIManager.OpenEditChapterDialog(_currentChapterId);
                }
                Hide();
                return;
            }
            if (id == "EditTask")
            {
                if (!string.IsNullOrEmpty(_currentChapterId) && !string.IsNullOrEmpty(_currentNodeId))
                {
                    QuestBook.UIManager.OpenEditTaskDialog(_currentChapterId, _currentNodeId);
                }
                Hide();
                return;
            }
            if (id == "CreateTaskNode")
            {
                try { QuestBook.UIManager.SetPendingCreateNodeScreenPos(_lastScreenPos); } catch { }
                QuestBook.UIManager.OpenCreateTaskDialog(_currentChapterId);
                Hide();
                return;
            }
            if (id == "DeleteChapter")
            {
                if (!string.IsNullOrEmpty(_currentChapterId))
                {
                    QuestBook.UIManager.DeleteChapter(_currentChapterId);
                }
                Hide();
                return;
            }
            if (id == "DeleteTask")
            {
                if (!string.IsNullOrEmpty(_currentChapterId) && !string.IsNullOrEmpty(_currentNodeId))
                {
                    bool opened = false;
                    try
                    {
                        opened = QuestBook.UIManager.OpenConfirmExitDialog("确认删除该任务？此操作不可撤销。", () =>
                        {
                            QuestBook.UIManager.DeleteTask(_currentChapterId, _currentNodeId);
                        }, () => { });
                    }
                    catch { }
                    if (!opened)
                    {
                        QuestBook.UIManager.DeleteTask(_currentChapterId, _currentNodeId);
                    }
                }
                Hide();
                return;
            }
            Hide();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
