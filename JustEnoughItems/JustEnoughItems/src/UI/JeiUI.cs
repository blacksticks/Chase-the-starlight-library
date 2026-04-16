using HarmonyLib;
using JustEnoughItems.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JustEnoughItems;

namespace JustEnoughItems.UI
{
    public class JeiUI : MonoBehaviour
    {


        // 公开方法：便于在 Inspector 接线
        public void OpenDetailsById(string itemId)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId)) { Show(null, itemId); return; }
                JeiItem it = null;
                try { JustEnoughItems.JeiDataStore.BuildIfNeeded(); } catch { }
                try { JustEnoughItems.JeiDataStore.TryGetItem(itemId, out it); } catch { }
                Show(it, itemId);
            }
            catch { Show(null, itemId); }
        }

        // 局部挂载：将 JEI 嵌入到指定父节点（如 uGUI_BlueprintsTab.canvas）内
        public void InitAttach(RectTransform parent)
        {
            if (parent == null) return;
            try
            {
                _root = parent.gameObject;
                _rootCanvas = parent.GetComponentInParent<Canvas>();

                // 在父节点下创建列表容器（全拉伸）
                var listRt = _root.transform.Find("JEI_List") as RectTransform;
                if (listRt == null)
                {
                    listRt = CreateUIObject("JEI_List", parent);
                    listRt.anchorMin = Vector2.zero;
                    listRt.anchorMax = Vector2.one;
                    listRt.offsetMin = Vector2.zero;
                    listRt.offsetMax = Vector2.zero;
                }
                _content = listRt;

                // Tooltip（局部，且不拦截事件）
                if (_tooltipRt == null && _rootCanvas != null)
                {
                    var tip = CreateUIObject("Tooltip", _rootCanvas.transform as RectTransform);
                    _tooltipRt = tip;
                    var bg = tip.gameObject.AddComponent<Image>();
                    bg.color = new Color(0, 0, 0, 0.85f);
                    bg.raycastTarget = false;
                    _tooltipText = tip.gameObject.AddComponent<Text>();
                    _tooltipText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    _tooltipText.color = Color.white;
                    _tooltipText.alignment = TextAnchor.UpperLeft;
                    _tooltipText.raycastTarget = false;
                    tip.sizeDelta = new Vector2(220, 80);
                    tip.gameObject.SetActive(false);
                }

                // 从 DataStore 获取并构建
                try { JustEnoughItems.JeiDataStore.BuildIfNeeded(); } catch { }
                var snap = JustEnoughItems.JeiDataStore.Snapshot();
                var allItems = snap != null ? snap.Values.ToList() : new List<JeiItem>();
                if (allItems.Count == 0)
                {
                    try { JustEnoughItems.Plugin.Log?.LogWarning("JEI InitAttach: no items from DataStore"); } catch { }
                }
                BuildAllItemsGrid(allItems);

                if (_root != null) _root.SetActive(true);
                IsVisible = true;

                try { JustEnoughItems.Plugin.Log?.LogInfo("JEI InitAttach done under parent: " + GetPath(parent)); } catch { }
            }
            catch { }
        }
        public void ShowTooltipById(string itemId)
        {
            try
            {
                if (_tooltipRt == null) return;
                JeiItem it = null;
                try { JustEnoughItems.JeiDataStore.BuildIfNeeded(); } catch { }
                try { JustEnoughItems.JeiDataStore.TryGetItem(itemId, out it); } catch { }
                if (it == null) return;
                ShowTooltip(it);
            }
            catch { }
        }
        public void HideTooltipPublic() { HideTooltip(); }

        private class TabGroup
        {
            public string Title;
            public Action OnClick;
            public TabGroup(string title, Action onClick)
            {
                Title = title;
                OnClick = onClick;
            }
        }
        private GameObject _root;                 // AssetBundle 实例化的根节点（JEI_Root）
        private RectTransform _content;           // 物品列表容器（Scroll View/Viewport/Content）
        private GameObject _itemCellPrefab;       // 物品单元格预制体（ItemCell）
        private GameObject _categoryGroupPrefab;  // 分类块预制体（CategoryGroup）
        private Text _title;                      // 标题文本（可选）
        private RectTransform _tabs;              // 标签容器（可选）
        private GameObject _backButtonGO;         // 返回按钮（存在于预制体或运行时创建）
        private Button _backButton;
        private Canvas _rootCanvas;
        private RectTransform _tooltipRt;
        private Text _tooltipText;
        public bool IsVisible { get; private set; }
        private readonly List<GameObject> _hiddenSiblings = new List<GameObject>();

        private bool _initialized;
        private EventSystem _jeiEventSystem;
        private readonly List<EventSystem> _disabledEventSystems = new List<EventSystem>();
        private GraphicRaycaster _raycaster;
        private Image _panelImage;
        private bool _prevCursorVisible;
        private CursorLockMode _prevCursorLock;

        private void EnsureJeiEventSystemActive()
        {
            // 不再接管或禁用其他 EventSystem，交由预制体/游戏原有输入系统处理
        }

        private void RestoreEventSystems()
        {
            // 不做任何接管恢复操作
        }

        // AssetBundle 集成：由 JeiManager 调用
        public void Init(GameObject root, RectTransform content, GameObject itemCellPrefab, GameObject categoryGroupPrefab)
        {
            _root = root;
            _content = content;
            _itemCellPrefab = itemCellPrefab;
            _categoryGroupPrefab = categoryGroupPrefab;

            // 可选引用：尝试在根下查找标题与标签容器
            if (_root != null)
            {
                _title = _root.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Title") ?? _root.GetComponentInChildren<Text>(true);
                var tabsTr = _root.transform.Find("Tabs");
                if (tabsTr != null) _tabs = tabsTr as RectTransform;
            }

            // 不再创建额外 EventSystem 或 Raycaster，完全交由预制体负责

            try
            {
                _rootCanvas = _root?.GetComponentInChildren<Canvas>(true);
                // 如果预制体内没有 Canvas，则在根上创建一个
                if (_rootCanvas == null && _root != null)
                {
                    _rootCanvas = _root.AddComponent<Canvas>();
                    _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                // 统一提高排序，确保位于最顶层并能接收指针
                if (_rootCanvas != null)
                {
                    _rootCanvas.overrideSorting = true;
                    _rootCanvas.sortingOrder = 30000; // 提升到更高层级
                    // 确保 Canvas 上有 GraphicRaycaster 以接收指针事件
                    var gr = _rootCanvas.gameObject.GetComponent<GraphicRaycaster>();
                    if (gr == null) gr = _rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
                    gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
                    // 确保不被 CanvasGroup 屏蔽事件
                    var cg = _rootCanvas.GetComponent<CanvasGroup>() ?? _rootCanvas.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    // 输出 Canvas 排序信息以便调试
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Canvas: overrideSorting={_rootCanvas.overrideSorting}, sortingOrder={_rootCanvas.sortingOrder}, raycastTargets={cg.blocksRaycasts}"); } catch { }

                    // 为所有子 Canvas 也补齐 Raycaster 与排序（避免子 Canvas 无法接收指针）
                    try
                    {
                        var allCanvases = _rootCanvas.GetComponentsInChildren<Canvas>(true);
                        foreach (var c in allCanvases)
                        {
                            if (c == null) continue;
                            var cgr = c.gameObject.GetComponent<GraphicRaycaster>();
                            if (cgr == null) c.gameObject.AddComponent<GraphicRaycaster>();
                            c.overrideSorting = true;
                            if (c.sortingOrder < _rootCanvas.sortingOrder) c.sortingOrder = _rootCanvas.sortingOrder;
                        }
                    }
                    catch { }

                    // 创建一个覆盖全屏的 RaycastBlocker（直接放在根 Canvas 下），用于拦截空白区域指针，避免透传到底层 UI
                    try
                    {
                        var blocker = _rootCanvas.transform.Find("RaycastBlocker") as RectTransform;
                        if (blocker == null)
                        {
                            blocker = CreateUIObject("RaycastBlocker", _rootCanvas.transform as RectTransform);
                            blocker.anchorMin = Vector2.zero;
                            blocker.anchorMax = Vector2.one;
                            blocker.offsetMin = Vector2.zero;
                            blocker.offsetMax = Vector2.zero;
                            var img = blocker.gameObject.AddComponent<Image>();
                            img.color = new Color(0, 0, 0, 0.05f); // 轻微可见，便于确认命中
                            img.raycastTarget = true;
                        }
                        // 作为首个子节点：内容与图标在其之上，空白处仍会命中它
                        if (blocker != null) blocker.SetAsFirstSibling();

                        // 为 Blocker 添加空事件回调，消费点击/滚轮/拖拽，避免透传
                        try
                        {
                            var et = blocker.gameObject.GetComponent<EventTrigger>();
                            if (et == null) et = blocker.gameObject.AddComponent<EventTrigger>();
                            et.triggers ??= new List<EventTrigger.Entry>();
                            EventTrigger.Entry Make(EventTriggerType t)
                            {
                                var e = new EventTrigger.Entry { eventID = t };
                                e.callback.AddListener(_ => { /* consume */ });
                                return e;
                            }
                            et.triggers.Add(Make(EventTriggerType.PointerDown));
                            et.triggers.Add(Make(EventTriggerType.PointerClick));
                            et.triggers.Add(Make(EventTriggerType.PointerUp));
                            et.triggers.Add(Make(EventTriggerType.BeginDrag));
                            et.triggers.Add(Make(EventTriggerType.Drag));
                            et.triggers.Add(Make(EventTriggerType.EndDrag));
                            et.triggers.Add(Make(EventTriggerType.Scroll));
                        }
                        catch { }

                        // 诊断：输出 RaycastBlocker 信息
                        try
                        {
                            var img2 = blocker.GetComponent<Image>();
                            int idx = blocker.GetSiblingIndex();
                            int last = blocker.parent.childCount - 1;
                            JustEnoughItems.Plugin.Log?.LogInfo($"JEI Blocker: name={blocker.name}, siblingIndex={idx}/{last}, raycastTarget={img2?.raycastTarget}, alpha={img2?.color.a}");
                        }
                        catch { }
                    }
                    catch { }
                }
                // 简易 Tooltip（不拦截事件）
                try
                {
                    if (_tooltipRt == null && _rootCanvas != null)
                    {
                        var tip = CreateUIObject("Tooltip", _rootCanvas.transform as RectTransform);
                        _tooltipRt = tip;
                        var bg = tip.gameObject.AddComponent<Image>();
                        bg.color = new Color(0, 0, 0, 0.8f);
                        bg.raycastTarget = false;
                        var txtRt = CreateUIObject("Text", tip);
                        var txt = txtRt.gameObject.AddComponent<Text>();
                        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        txt.alignment = TextAnchor.UpperLeft;
                        txt.color = Color.white;
                        txt.fontSize = 12;
                        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.raycastTarget = false;
                        _tooltipText = txt;
                        tip.anchorMin = new Vector2(0, 1);
                        tip.anchorMax = new Vector2(0, 1);
                        tip.pivot = new Vector2(0, 1);
                        tip.sizeDelta = new Vector2(260, 120);
                        tip.gameObject.SetActive(false);
                        txtRt.anchorMin = Vector2.zero;
                        txtRt.anchorMax = Vector2.one;
                        txtRt.offsetMin = new Vector2(8, 8);
                        txtRt.offsetMax = new Vector2(-8, -8);
                        var cg = tip.gameObject.AddComponent<CanvasGroup>();
                        cg.blocksRaycasts = false;
                    }
                }
                catch { }
                // 若场景不存在 EventSystem，创建一个最小可用的
                if (EventSystem.current == null)
                {
                    var esGo = new GameObject("EventSystem");
                    esGo.AddComponent<EventSystem>();
                    esGo.AddComponent<StandaloneInputModule>();
                }

                var rootRt = _root.GetComponent<RectTransform>();
                if (rootRt != null && _root.transform.localScale != Vector3.one)
                {
                    _root.transform.localScale = Vector3.one;
                }

                // 不再修改 Panel 或其子节点的样式/锚点，由预制体决定

                // 不输出与 Canvas 排序/缩放相关的日志

                // 不再清理或改动背景，完全尊重预制体

                // Back 按钮：优先从预制体中查找名为 BackButton 的对象；若无则运行时创建一个简易按钮
                var backTr = _root.transform.Find("BackButton");
                if (backTr != null)
                {
                    _backButtonGO = backTr.gameObject;
                    _backButton = _backButtonGO.GetComponent<Button>() ?? _backButtonGO.AddComponent<Button>();
                }
                else
                {
                    JustEnoughItems.Plugin.Log?.LogError("JEI BackButton not found in prefab. Please add a Button named 'BackButton' under the JEI root.");
                }

                if (_backButton != null)
                {
                    _backButton.onClick.RemoveAllListeners();
                    _backButton.onClick.AddListener(() =>
                    {
                        try { Show(); } catch { }
                    });
                    _backButtonGO.SetActive(false); // 默认隐藏，详情页显示时再启用
                }


            }
            catch { }

            // 初始隐藏
            if (_root != null) _root.SetActive(false);
            _initialized = true;
        }

        public void Show(JeiItem data, string fallbackId)
        {
            if (!_initialized) return;
            // 确保数据存储就绪（扫描 + 补充 JSON 合并）
            try
            {
                JustEnoughItems.JeiDataStore.BuildIfNeeded();
                var cnt = JustEnoughItems.JeiDataStore.Snapshot()?.Count ?? 0;
                JustEnoughItems.Plugin.Log?.LogInfo($"JEI DataStore ready for details. Count={cnt}");
            }
            catch { }
            try
            {
                var dbgItemId = data?.ItemId ?? "<null>";
                var srcCnt = data?.Source?.Count ?? -1;
                var useCnt = data?.Usage?.Count ?? -1;
                JustEnoughItems.Plugin.Log?.LogInfo($"JEI Debug(UI.Show details): dataIsNull={(data == null)}, fallbackId='{fallbackId}', data.ItemId='{dbgItemId}', SourceCnt={srcCnt}, UsageCnt={useCnt}");
            }
            catch { }
            if (data == null)
            {
                if (_title != null) _title.text = $"物品未配置：{fallbackId}";
                if (_tabs != null) BuildTabs(new List<TabGroup>());
                if (_content != null)
                {
                    // 立即销毁，避免旧的“物品不存在”文本在本帧残留覆盖新内容
                    try { while (_content.childCount > 0) DestroyImmediate(_content.GetChild(0).gameObject); } catch { }
                    var msg = CreateUIObject("Empty", _content).gameObject.AddComponent<Text>();
                    msg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    msg.alignment = TextAnchor.MiddleCenter;
                    msg.text = "该物品不存在";
                }
            }
            else
            {
                if (_title != null) _title.text = data.ItemId;
                if (_tabs != null)
                {
                    var groups = new List<TabGroup>();
                    if (data.Source != null && data.Source.Count > 0)
                        groups.Add(new TabGroup("获得方式", () => BuildSourceView(data)));
                    if (data.Usage != null && data.Usage.Count > 0)
                        groups.Add(new TabGroup("用途", () => BuildUsageView(data)));
                    BuildTabs(groups);
                    // 默认显示第一个页签
                    if (groups.Count > 0)
                        groups[0].OnClick?.Invoke();
                    else
                    {
                        foreach (Transform child in _content) Destroy(child.gameObject);
                        var msg = CreateUIObject("Empty", _content).gameObject.AddComponent<Text>();
                        msg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        msg.alignment = TextAnchor.MiddleCenter;
                        msg.text = "没有可显示的来源/用途";
                    }
                }
            }
            JustEnoughItems.Plugin.Log?.LogInfo("JEI UI Show");
            if (_root != null) _root.SetActive(true);
            IsVisible = true;
            // 隐藏 Content 下除 JEI 外的所有兄弟节点
            try { HideOtherContentChildren(true); } catch { }
            EnsureJeiEventSystemActive();
            if (_backButtonGO != null) _backButtonGO.SetActive(true); // 详情页：显示返回按钮
            // 解锁并显示鼠标
            try { _prevCursorVisible = Cursor.visible; _prevCursorLock = Cursor.lockState; Cursor.visible = true; Cursor.lockState = CursorLockMode.None; } catch { }
        }

        public void Show()
        {
            if (!_initialized) return;
            try { JustEnoughItems.Plugin.Log?.LogInfo("JEI Debug(UI.Show list): called"); } catch { }
            JustEnoughItems.Plugin.Log?.LogInfo("JEI UI Toggle Show");
            if (_content == null)
            {
                JustEnoughItems.Plugin.Log?.LogError("JEI Show: _content is null. Please ensure prefab path matches '.../Scroll View/Viewport/Content'.");
            }
            // 确保数据存储就绪（不再读取 items.json，而是使用扫描+补充）
            try
            {
                JustEnoughItems.JeiDataStore.BuildIfNeeded();
                var cnt = JustEnoughItems.JeiDataStore.Snapshot()?.Count ?? 0;
                JustEnoughItems.Plugin.Log?.LogInfo($"JEI DataStore ready. Count={cnt}");
            }
            catch { }
            // 无特定物品时，展示 DataStore 中的全部条目
            if (_title != null) _title.text = "Just Enough Items";
            if (_tabs != null)
            {
                foreach (Transform child in _tabs) Destroy(child.gameObject);
            }
            var snap = JustEnoughItems.JeiDataStore.Snapshot();
            var allItems = snap != null ? snap.Values.ToList() : new List<JeiItem>();
            if (allItems.Count == 0)
            {
                // fallback message when no config items exist
                if (_content != null)
                {
                    foreach (Transform child in _content) Destroy(child.gameObject);
                    var msgRt = CreateUIObject("EmptyMessage", _content);
                    var msg = msgRt.gameObject.AddComponent<Text>();
                    msg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    msg.alignment = TextAnchor.MiddleCenter;
                    msg.text = "没有可显示的条目。可在此添加补充文件（可选）：\n" + JustEnoughItems.Config.JeiSupplementService.ConfigPath;
                    msgRt.anchorMin = new Vector2(0, 0);
                    msgRt.anchorMax = new Vector2(1, 1);
                    msgRt.offsetMin = msgRt.offsetMax = Vector2.zero;
                }
            }
            else
            {
                if (_categoryGroupPrefab != null)
                    BuildItemsByCategories(allItems);
                else
                    BuildAllItemsGrid(allItems);
            }
            if (_root != null) _root.SetActive(true);
            IsVisible = true;
            // 隐藏 Content 下除 JEI 外的所有兄弟节点
            try { HideOtherContentChildren(true); } catch { }
            EnsureJeiEventSystemActive();
            if (_backButtonGO != null) _backButtonGO.SetActive(false); // 列表页：隐藏返回按钮
            // 解锁并显示鼠标
            try { _prevCursorVisible = Cursor.visible; _prevCursorLock = Cursor.lockState; Cursor.visible = true; Cursor.lockState = CursorLockMode.None; } catch { }
        }

        public void Hide()
        {
            if (!IsVisible) return;
            try
            {
                // 恢复 Content 下被隐藏的兄弟节点
                try { HideOtherContentChildren(false); } catch { }
                if (_root != null) _root.SetActive(false);
                IsVisible = false;
            }
            catch { }
        }

        /// <summary>
        /// 公开方法：用于PDA页签显示控制
        /// </summary>
        public void ShowForPDATab()
        {
            if (IsVisible) return;
            try
            {
                if (_root != null) _root.SetActive(true);
                IsVisible = true;
            }
            catch { }
        }

        private void BuildItemsByCategories(List<JeiItem> items)
        {
            // 新版不再提供 categories.json，直接平铺显示以避免编译错误
            BuildAllItemsGrid(items);
        }

        private void BuildAllItemsGrid(List<JeiItem> items)
        {
            if (_content == null) return;
            // 仅清空并按预制体既有布局逐条添加
            foreach (Transform child in _content) Destroy(child.gameObject);

            int pad = 8; // 仅用于 Icon 内边距（不改父级布局）

            int index = 0;
            foreach (var it in items)
            {
                GameObject cardGo;
                if (_itemCellPrefab != null)
                {
                    cardGo = GameObject.Instantiate(_itemCellPrefab, _content);
                    cardGo.name = $"Item_{it.ItemId}";
                }
                else
                {
                    // 兼容：没有 ItemCell 预制体时，使用最小动态构建（仅限卡片本身，不改父级）
                    var card = CreateUIObject("ItemCard", _content);
                    cardGo = card.gameObject;
                    // 最小子控件：Icon 与 Name
                    var iconRt = CreateUIObject("Icon", card);
                    var iconImg = iconRt.gameObject.AddComponent<Image>();
                    iconImg.preserveAspect = true;
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = new Vector2(pad, pad);
                    iconRt.offsetMax = new Vector2(-pad, -pad);

                    var nameRt = CreateUIObject("Name", card);
                    var nameText = nameRt.gameObject.AddComponent<Text>();
                    nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    nameText.alignment = TextAnchor.LowerCenter;
                    nameText.raycastTarget = false;
                    nameText.color = Color.white;
                    nameRt.anchorMin = new Vector2(0, 0);
                    nameRt.anchorMax = new Vector2(1, 0);
                    nameRt.pivot = new Vector2(0.5f, 0);
                    nameRt.sizeDelta = new Vector2(0, 18);
                    nameRt.anchoredPosition = new Vector2(0, 2);
                }
                var cardRt = cardGo.GetComponent<RectTransform>() ?? cardGo.AddComponent<RectTransform>();

                // 不禁用预制体中的 Button，由预制体决定交互

                // 设置图标与文本（无图标则直接跳过该条目，不显示占位）
                var sprite = GetItemSprite(string.IsNullOrEmpty(it.Icon) ? it.ItemId : it.Icon);
                if (sprite == null)
                {
                    try { Destroy(cardGo); } catch { }
                    continue;
                }
                // 图标节点优先名为 Icon 的 Image，否则找第一个 Image；若找不到则不设置
                var iconTf = cardGo.transform.Find("CircleArea/Icon") ?? cardGo.transform.Find("Icon");
                if (iconTf == null)
                {
                    // 兜底：递归按名称查找 "Icon" 节点，避免多一层容器时找不到
                    iconTf = cardGo.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr != null && tr.name == "Icon");
                }
                var icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
                if (icon != null)
                {
                    if (sprite != null)
                    {
                        icon.sprite = sprite;
                        icon.color = Color.white;
                    }
                    // 如果没有 sprite，则不改颜色，保持预制体样式
                    icon.preserveAspect = true;
                    icon.raycastTarget = true; // 确保可接收悬停/点击
                    // 代码侧自动绑定事件
                    var trig = icon.gameObject.GetComponent<EventTrigger>();
                    if (trig == null) trig = icon.gameObject.AddComponent<EventTrigger>();
                    trig.triggers ??= new List<EventTrigger.Entry>();
                    var captured = it;
                    // Click -> 进入详情
                    var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                    click.callback.AddListener(_ => { Show(captured, captured.ItemId); });
                    trig.triggers.Add(click);
                    // 悬浮进入/离开
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener(_ => { ShowTooltip(captured); });
                    trig.triggers.Add(enter);
                    var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exit.callback.AddListener(_ => { HideTooltip(); });
                    trig.triggers.Add(exit);

                    // 双保险：添加 Button 并绑定 onClick
                    var btn = icon.gameObject.GetComponent<Button>();
                    if (btn == null) btn = icon.gameObject.AddComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => { Show(captured, captured.ItemId); });
                        btn.targetGraphic = icon; // 指向自身 Image
                    }

                    // 再加一个轻量点击/悬浮处理器（IPointerClick/Enter/Exit）
                    var h2 = icon.gameObject.GetComponent<JeiItemHandler>();
                    if (h2 == null) h2 = icon.gameObject.AddComponent<JeiItemHandler>();
                    h2.ui = this;
                    h2.data = captured;
                }
                else
                {
                    try { JustEnoughItems.Plugin.Log?.LogWarning($"JEI Icon not found for item (flat): {it.ItemId}. Expected 'CircleArea/Icon' or 'Icon'."); } catch { }
                }
                
                index++;
            }
            
        }

        private void HideOtherContentChildren(bool hide)
        {
            try
            {
                if (_root == null) return;
                var parent = _root.transform.parent;
                if (parent == null) return;
                if (hide)
                {
                    _hiddenSiblings.Clear();
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var ch = parent.GetChild(i);
                        if (ch == null) continue;
                        if (ch.gameObject == _root) continue;
                        if (ch.gameObject.activeSelf)
                        {
                            ch.gameObject.SetActive(false);
                            _hiddenSiblings.Add(ch.gameObject);
                        }
                    }
                }
                else
                {
                    foreach (var go in _hiddenSiblings)
                    {
                        if (go != null) go.SetActive(true);
                    }
                    _hiddenSiblings.Clear();
                }
            }
            catch { }
        }

        private Sprite GetItemSprite(string idOrTechType)
        {
            try
            {
                if (string.IsNullOrEmpty(idOrTechType)) return null;
                var token = idOrTechType.Trim();
                if (System.IO.Path.IsPathRooted(token))
                {
                    var rel = ToIconsRelative(token);
                    if (!string.IsNullOrEmpty(rel)) token = rel;
                }
                // 路径型：仅接受以 icons/ 开头
                if (token.Contains("/") || token.Contains("\\") || token.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    if (!token.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)) return null;
                    return JustEnoughItems.IconCache.GetByIconsRelative(token);
                }
                // 非路径：按 id 获取（ingredients/<id>.png）
                return JustEnoughItems.IconCache.GetById(token);
            }
            catch { return null; }
        }

        // 从磁盘路径读取图片并创建 Sprite
        private Sprite LoadSpriteFromFilePath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (!System.IO.File.Exists(path)) return null;
                var bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (UnityEngine.ImageConversion.LoadImage(tex, bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Icon: loaded file sprite size={tex.width}x{tex.height} from '{path}'"); } catch { }
                    return spr;
                }
                else
                {
                    try { JustEnoughItems.Plugin.Log?.LogWarning($"JEI Icon: LoadImage failed for file path '{path}'"); } catch { }
                }
            }
            catch { }
            return null;
        }

        private string ToIconsRelative(string abs)
        {
            try
            {
                if (string.IsNullOrEmpty(abs)) return abs;
                var norm = abs.Replace('\\', '/');
                var roots = new System.Collections.Generic.List<string>();
                var abIcons = JustEnoughItems.Config.ConfigService.NewIconsDirectory; // .../AssetBundles/icons
                if (!string.IsNullOrEmpty(abIcons)) roots.Add(abIcons.Replace('\\', '/'));
                try
                {
                    var assetBundlesDir = System.IO.Directory.GetParent(abIcons)?.FullName; // .../AssetBundles
                    var pluginsBase = System.IO.Directory.GetParent(assetBundlesDir ?? string.Empty)?.FullName; // .../JustEnoughItems
                    if (!string.IsNullOrEmpty(pluginsBase)) roots.Add(System.IO.Path.Combine(pluginsBase, "icons").Replace('\\', '/'));
                    // 不再支持 QMods 根路径映射（新版路径为 BepInEx/plugins/JustEnoughItems）
                }
                catch { }
                foreach (var r in roots)
                {
                    if (!string.IsNullOrEmpty(r))
                    {
                        var rr = r.EndsWith("/") ? r : (r + "/");
                        if (norm.StartsWith(rr, StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = norm.Substring(rr.Length);
                            return ("icons/" + rel).Replace("\\", "/");
                        }
                    }
                }
            }
            catch { }
            return abs;
        }

        // 兼容 Atlas.Sprite/Atlas+Sprite：统一转换为 UnityEngine.Sprite
        private Sprite TryConvertSprite(object obj)
        {
            try
            {
                if (obj == null) return null;
                if (obj is Sprite s) return s;
                var t = obj.GetType();
                var full = t.FullName ?? string.Empty;
                bool looksAtlasSprite =
                    full == "Atlas.Sprite" ||
                    full.EndsWith("Atlas.Sprite", StringComparison.Ordinal) ||
                    full.Contains("Atlas+Sprite") ||
                    (t.Name == "Sprite" && string.Equals(t.Namespace, "Atlas", StringComparison.Ordinal));
                if (looksAtlasSprite)
                {
                    // 优先尝试属性 'sprite'（部分封装直接暴露 UnityEngine.Sprite）
                    try
                    {
                        var piSprite = t.GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (piSprite != null)
                        {
                            var raw = piSprite.GetValue(obj) as Sprite;
                            if (raw != null)
                            {
                                try { JustEnoughItems.Plugin.Log?.LogInfo("JEI Icon: Atlas.Sprite -> property sprite hit"); } catch { }
                                return raw;
                            }
                        }
                    }
                    catch { }
                    // 退化：用 texture + rect 创建新 Sprite
                    try
                    {
                        var piTex = t.GetProperty("texture", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                   ?? t.GetProperty("atlasTexture", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        var piPPU = t.GetProperty("pixelsPerUnit", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        Texture2D tex = piTex?.GetValue(obj) as Texture2D;
                        if (tex == null)
                        {
                            var fiTex = t.GetField("m_Texture", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                            if (fiTex != null) tex = fiTex.GetValue(obj) as Texture2D;
                        }
                        var piRect = t.GetProperty("rect", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                    ?? t.GetProperty("uvRect", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        var rectObj = piRect?.GetValue(obj);
                        float ppu = 100f;
                        if (piPPU != null)
                        {
                            try { ppu = Convert.ToSingle(piPPU.GetValue(obj)); } catch { }
                        }
                        Rect r = default;
                        if (rectObj is Rect rr) r = rr;
                        if (tex != null)
                        {
                            if (r.width <= 0 || r.height <= 0) r = new Rect(0, 0, tex.width, tex.height);
                            var sp = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), ppu);
                            try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI Icon: Atlas.Sprite -> created Sprite size={r.width}x{r.height} ppu={ppu}"); } catch { }
                            return sp;
                        }
                        else
                        {
                            try { JustEnoughItems.Plugin.Log?.LogWarning($"JEI Icon: Atlas.Sprite convert failed (texture/rect missing), tex={(tex!=null)}, rectType={rectObj?.GetType().FullName}"); } catch { }
                        }
                    }
                    catch { }
                }
                else
                {
                    // 其他类型：尝试常见属性 'Sprite'
                    try
                    {
                        var pi = t.GetProperty("Sprite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (pi != null)
                        {
                            var raw = pi.GetValue(obj) as Sprite;
                            if (raw != null) return raw;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        public void ShowTooltip(JeiItem item)
        {
            try
            {
                if (_tooltipRt == null || _tooltipText == null || _rootCanvas == null) return;
                try { UnityEngine.Debug.Log($"[JEI][UI][Tooltip] item enter id='{item?.ItemId}'"); } catch { }
                var name = GetDisplayName(item?.ItemId ?? string.Empty);
                var desc = item?.Description ?? string.Empty;
                _tooltipText.text = string.IsNullOrEmpty(desc) ? ($"名称: {name}") : ($"名称: {name}\n描述: {desc}");
                try
                {
                    var logText = string.IsNullOrEmpty(desc) ? ($"名称: {name}") : ($"名称: {name}\n描述: {desc}");
                    UnityEngine.Debug.Log($"[JEI][UI][Tooltip] text='{logText}'");
                }
                catch { }
                _tooltipRt.gameObject.SetActive(true);
                _tooltipRt.SetAsLastSibling();
                UpdateTooltipPosition();
            }
            catch { }
        }

        // 简化重载：仅根据物品ID显示显示名 + ID（用于原材料等无需 JeiItem 的场景）
        public void ShowTooltip(string id)
        {
            try
            {
                if (_tooltipRt == null || _tooltipText == null || _rootCanvas == null) return;
                try { UnityEngine.Debug.Log($"[JEI][UI][TooltipById] enter id='{id}'"); } catch { }
                var name = GetDisplayName(id);
                _tooltipText.text = $"名称: {name}";
                try { UnityEngine.Debug.Log($"[JEI][UI][TooltipById] text='名称: {name}'"); } catch { }
                _tooltipRt.gameObject.SetActive(true);
                _tooltipRt.SetAsLastSibling();
                UpdateTooltipPosition();
            }
            catch { }
        }

        public void HideTooltip()
        {
            try
            {
                if (_tooltipRt == null) return;
                _tooltipRt.gameObject.SetActive(false);
            }
            catch { }
        }

        private void UpdateTooltipPosition()
        {
            if (_rootCanvas == null || _tooltipRt == null) return;
            var canvasRt = _rootCanvas.transform as RectTransform;
            if (canvasRt == null) return;
            Vector2 localCenter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, Input.mousePosition, null, out localCenter);
            var canvasSize = canvasRt.rect.size;
            Vector2 anchored = new Vector2(localCenter.x + canvasSize.x * 0.5f, localCenter.y - canvasSize.y * 0.5f);
            anchored += new Vector2(6, -6);
            var tipSize = _tooltipRt.sizeDelta;
            float minX = 0f;
            float maxX = Mathf.Max(0f, canvasSize.x - tipSize.x);
            float maxY = 0f;
            float minY = -Mathf.Max(0f, canvasSize.y - tipSize.y);
            anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
            _tooltipRt.anchoredPosition = anchored;
        }


        private static Type _giType;
        private static MethodInfo _miGetButtonDown;
        private void Update()
        {
            // 诊断：点击时打印 RaycastAll 命中（仅在 JEI 可见时）
            try
            {
                if (IsVisible && Input.GetMouseButtonDown(0) && EventSystem.current != null)
                {
                    var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                    var results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(ped, results);
                    JustEnoughItems.Plugin.Log?.LogInfo($"JEI RaycastAll hits={results.Count}");
                    int n = Mathf.Min(10, results.Count);
                    for (int i = 0; i < n; i++)
                    {
                        var go = results[i].gameObject;
                        JustEnoughItems.Plugin.Log?.LogInfo($"  [{i}] {go.name} path={GetPath(go.transform)} layer={go.layer}");
                    }
                }
            }
            catch { }

            // 跟随鼠标的 Tooltip 位置更新
            try
            {
                if (_tooltipRt != null && _tooltipRt.gameObject.activeSelf)
                    UpdateTooltipPosition();
            }
            catch { }

            if (!IsVisible) return;
            // ESC
            if (Input.GetKeyDown(KeyCode.Escape)) { JustEnoughItems.Plugin.Log?.LogInfo("JEI Update: ESC detected -> Hide"); Hide(); return; }
            // Mod Input 按钮
            if (JustEnoughItems.Plugin.JeiButtonEnum != null)
            {
                if (_giType == null)
                {
                    _giType = AccessTools.TypeByName("GameInput");
                    _miGetButtonDown = _giType?.GetMethod("GetButtonDown", BindingFlags.Public | BindingFlags.Static, null, new Type[] { JustEnoughItems.Plugin.JeiButtonEnumType }, null);
                }
                if (_miGetButtonDown != null)
                {
                    try
                    {
                        var pressed = (bool)_miGetButtonDown.Invoke(null, new object[] { JustEnoughItems.Plugin.JeiButtonEnum });
                        if (pressed) { JustEnoughItems.Plugin.Log?.LogInfo("JEI Update: ModInput button detected -> Hide"); Hide(); return; }
                    }
                    catch { }
                }
            }
            // 回退键
            if (Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value)) { JustEnoughItems.Plugin.Log?.LogInfo("JEI Update: Fallback OpenKey detected -> Hide"); Hide(); return; }

            // 不做任何兜底强制点击
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "/<null>";
            var parts = new System.Collections.Generic.List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return "/" + string.Join("/", parts);
        }

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private void StripDefaultBackdrops(RectTransform panelRt)
        {
            if (panelRt == null) return;
            var images = panelRt.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                // 跳过 Panel 自身与用户自定义 BG 节点
                if (ReferenceEquals(img, _panelImage)) continue;
                if (img.name.Equals("BG", StringComparison.OrdinalIgnoreCase)) continue;
                // 跳过滚动条相关（用户将自定义其样式）
                if (img.GetComponentInParent<Scrollbar>() != null) continue;

                bool isMask = img.GetComponent<Mask>() != null || img.GetComponent<RectMask2D>() != null;
                var spr = img.sprite;
                var nm = ((spr != null ? spr.name : string.Empty) + " " + img.gameObject.name).ToLowerInvariant();
                bool looksDefaultName = nm.Contains("background") || nm.Contains("panel") || nm.Contains("window") || nm.Contains("frame") || nm.Contains("round") || nm.Contains("bg");
                bool looksWhite = img.color.a > 0.85f && img.color.r > 0.9f && img.color.g > 0.9f && img.color.b > 0.9f;
                bool sliced = (img.type == Image.Type.Sliced) || (spr != null && spr.border.sqrMagnitude > 0);

                // 对带 Mask 的控件，仅将颜色透明，保留组件功能
                if (isMask)
                {
                    var c = img.color; c.a = 0f; img.color = c; img.raycastTarget = false; continue;
                }

                // 疑似默认圆角背景：直接禁用显示
                if (spr != null && (looksDefaultName || looksWhite || sliced))
                {
                    img.enabled = false;
                    var o = img.GetComponent<Outline>(); if (o) o.enabled = false;
                    var s = img.GetComponent<Shadow>(); if (s) s.enabled = false;
                    img.raycastTarget = false;
                }
            }
        }



        private void BuildTabs(List<TabGroup> groups)
        {
            foreach (Transform child in _tabs) Destroy(child.gameObject);
            float x = 10f;
            foreach (var g in groups)
            {
                var btnRt = CreateUIObject($"Tab_{g.Title}", _tabs);
                var btn = btnRt.gameObject.AddComponent<Button>();
                var img = btnRt.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0.1f);
                var txt = CreateUIObject("Text", btnRt).gameObject.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.alignment = TextAnchor.MiddleCenter;
                txt.text = g.Title;
                txt.raycastTarget = false; // let parent Button receive clicks
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { try { g.OnClick?.Invoke(); } catch { } });
                btnRt.anchorMin = new Vector2(0, 0);
                btnRt.anchorMax = new Vector2(0, 1);
                btnRt.pivot = new Vector2(0, 0.5f);
                btnRt.sizeDelta = new Vector2(120, 0);
                btnRt.anchoredPosition = new Vector2(x, 0);
                x += 130f;
            }
        }

        // ===== 新 schema 渲染 =====
        private void BuildSourceView(JeiItem item)
        {
            // 立即销毁，避免上一视图文本残留
            try { while (_content.childCount > 0) DestroyImmediate(_content.GetChild(0).gameObject); } catch { }
            var list = item?.Source ?? new List<JeiSourceTab>();
            if (list.Count == 0)
            {
                var msg = CreateUIObject("Empty_Source", _content).gameObject.AddComponent<Text>();
                msg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                msg.alignment = TextAnchor.MiddleCenter;
                msg.text = "没有配置来源信息";
                return;
            }

            RectTransform subTabs = null;
            RectTransform subContent = null;
            if (list.Count > 1)
            {
                subTabs = CreateUIObject("SubTabs_Source", _content);
                subTabs.anchorMin = new Vector2(0, 1);
                subTabs.anchorMax = new Vector2(1, 1);
                subTabs.pivot = new Vector2(0.5f, 1);
                subTabs.sizeDelta = new Vector2(0, 32);
                subTabs.anchoredPosition = new Vector2(0, -4);
            }
            subContent = CreateUIObject("SubContent_Source", _content);
            subContent.anchorMin = new Vector2(0, 0);
            subContent.anchorMax = new Vector2(1, 1);
            subContent.offsetMin = new Vector2(8, 8);
            subContent.offsetMax = new Vector2(-8, -40);

            Action<int> render = (idx) =>
            {
                try { while (subContent.childCount > 0) DestroyImmediate(subContent.GetChild(0).gameObject); } catch { }
                var tab = list[Mathf.Clamp(idx, 0, list.Count - 1)];
                RenderSourceSection(subContent, item, tab);
            };

            if (subTabs != null)
            {
                float x = 8f;
                for (int i = 0; i < list.Count; i++)
                {
                    int captured = i;
                    var btnRt = CreateUIObject($"SubTab_S_{i}", subTabs);
                    var btn = btnRt.gameObject.AddComponent<Button>();
                    var img = btnRt.gameObject.AddComponent<Image>();
                    img.color = new Color(1, 1, 1, 0.08f);
                    var txt = CreateUIObject("Text", btnRt).gameObject.AddComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.text = $"来源 {i + 1}";
                    txt.raycastTarget = false;
                    btn.onClick.AddListener(() => render(captured));
                    btnRt.anchorMin = new Vector2(0, 0);
                    btnRt.anchorMax = new Vector2(0, 1);
                    btnRt.pivot = new Vector2(0, 0.5f);
                    btnRt.sizeDelta = new Vector2(100, 0);
                    btnRt.anchoredPosition = new Vector2(x, 0);
                    x += 108f;
                }
            }

            render(0);
        }

        private void BuildUsageView(JeiItem item)
        {
            // 立即销毁，避免上一视图文本残留
            try { while (_content.childCount > 0) DestroyImmediate(_content.GetChild(0).gameObject); } catch { }
            var list = item?.Usage ?? new List<JeiUsageTab>();
            if (list.Count == 0)
            {
                var msg = CreateUIObject("Empty_Usage", _content).gameObject.AddComponent<Text>();
                msg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                msg.alignment = TextAnchor.MiddleCenter;
                msg.text = "没有配置用途信息";
                return;
            }

            RectTransform subTabs = null;
            RectTransform subContent = null;
            if (list.Count > 1)
            {
                subTabs = CreateUIObject("SubTabs_Usage", _content);
                subTabs.anchorMin = new Vector2(0, 1);
                subTabs.anchorMax = new Vector2(1, 1);
                subTabs.pivot = new Vector2(0.5f, 1);
                subTabs.sizeDelta = new Vector2(0, 32);
                subTabs.anchoredPosition = new Vector2(0, -4);
            }
            subContent = CreateUIObject("SubContent_Usage", _content);
            subContent.anchorMin = new Vector2(0, 0);
            subContent.anchorMax = new Vector2(1, 1);
            subContent.offsetMin = new Vector2(8, 8);
            subContent.offsetMax = new Vector2(-8, -40);

            Action<int> render = (idx) =>
            {
                foreach (Transform c in subContent) Destroy(c.gameObject);
                var tab = list[Mathf.Clamp(idx, 0, list.Count - 1)];
                RenderUsageSection(subContent, item, tab);
            };

            if (subTabs != null)
            {
                float x = 8f;
                for (int i = 0; i < list.Count; i++)
                {
                    int captured = i;
                    var btnRt = CreateUIObject($"SubTab_U_{i}", subTabs);
                    var btn = btnRt.gameObject.AddComponent<Button>();
                    var img = btnRt.gameObject.AddComponent<Image>();
                    img.color = new Color(1, 1, 1, 0.08f);
                    var txt = CreateUIObject("Text", btnRt).gameObject.AddComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.text = $"用途 {i + 1}";
                    txt.raycastTarget = false;
                    btn.onClick.AddListener(() => render(captured));
                    btnRt.anchorMin = new Vector2(0, 0);
                    btnRt.anchorMax = new Vector2(0, 1);
                    btnRt.pivot = new Vector2(0, 0.5f);
                    btnRt.sizeDelta = new Vector2(100, 0);
                    btnRt.anchoredPosition = new Vector2(x, 0);
                    x += 108f;
                }
            }

            render(0);
        }

        private void RenderSourceSection(RectTransform parent, JeiItem owner, JeiSourceTab tab)
        {
            if (tab == null) return;
            if (tab.IfFabricator)
            {
                RenderFabricatorRow(parent, tab.Fabricator, tab.Ingredient, owner?.ItemId, tab.Text);
            }
            else
            {
                RenderCustomSection(parent, tab.Image, tab.Text);
            }
        }

        private void RenderUsageSection(RectTransform parent, JeiItem owner, JeiUsageTab tab)
        {
            if (tab == null) return;
            if (tab.IfFabricator)
            {
                // Target 现为 List<string>，此旧版行渲染接口仅支持单个结果，这里取第一个作为结果占位
                var target = (tab.Target != null && tab.Target.Count > 0) ? tab.Target[0] : string.Empty;
                RenderFabricatorRow(parent, tab.Fabricator, tab.Ingredient, target, tab.Text);
            }
            else
            {
                var firstTarget = (tab.Target != null && tab.Target.Count > 0) ? tab.Target[0] : null;
                RenderCustomSection(parent, tab.Image, tab.Text, firstTarget);
            }
        }

        private void RenderFabricatorRow(RectTransform parent, string fabricatorId, List<string> ingredients, string resultId, string hintText)
        {
            var row = CreateUIObject("FabricatorRow", parent);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0, 1);
            row.sizeDelta = new Vector2(0, 96);
            row.anchoredPosition = new Vector2(0, -8);

            float x = 8f; float y = -8f;

            // Fabricator 图标 + 名称
            var fab = CreateUIObject("Fabricator", row);
            fab.anchorMin = new Vector2(0, 1); fab.anchorMax = new Vector2(0, 1); fab.pivot = new Vector2(0, 1);
            fab.sizeDelta = new Vector2(80, 80); fab.anchoredPosition = new Vector2(x, y);
            // 显示工作台显示名（来自 jei-fabricators.json），而非 ID
            string fabDisplay = string.IsNullOrEmpty(fabricatorId) ? "<未配置工作台>" : ResolveFabricatorDisplayName(fabricatorId);
            CreateIconWithName(fab, fabricatorId, fabDisplay, () => OpenById(fabricatorId));
            x += 90f;

            // 箭头
            x = CreateArrow(row, x, y);

            // 原料区域（合并计数）
            var ing = ingredients ?? new List<string>();
            var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ing)
            {
                if (string.IsNullOrEmpty(id)) continue;
                groups[id] = groups.TryGetValue(id, out var n) ? n + 1 : 1;
            }

            var ingRt = CreateUIObject("Ingredients", row);
            ingRt.anchorMin = new Vector2(0, 1); ingRt.anchorMax = new Vector2(0, 1); ingRt.pivot = new Vector2(0, 1);
            ingRt.sizeDelta = new Vector2(Mathf.Max(1, groups.Count) * 64 + (Mathf.Max(0, groups.Count - 1) * 6), 80);
            ingRt.anchoredPosition = new Vector2(x, y);
            foreach (var kv in groups)
            {
                var cell = CreateUIObject("Cell", ingRt);
                cell.anchorMin = new Vector2(0, 1); cell.anchorMax = new Vector2(0, 1); cell.pivot = new Vector2(0, 1);
                cell.sizeDelta = new Vector2(64, 80); cell.anchoredPosition = new Vector2(ingRt.childCount * 70, 0);
                CreateIconWithCount(cell, kv.Key, kv.Value, () => OpenById(kv.Key));
            }
            x += ingRt.sizeDelta.x + 10f;

            // 箭头
            x = CreateArrow(row, x, y);

            // 结果
            var res = CreateUIObject("Result", row);
            res.anchorMin = new Vector2(0, 1); res.anchorMax = new Vector2(0, 1); res.pivot = new Vector2(0, 1);
            res.sizeDelta = new Vector2(80, 80); res.anchoredPosition = new Vector2(x, y);
            var resName = string.IsNullOrEmpty(resultId) ? "?" : GetDisplayName(resultId);
            CreateIconWithName(res, resultId, resName, () => OpenById(resultId));

            // 说明文本
            if (!string.IsNullOrEmpty(hintText))
            {
                var t = CreateUIObject("Hint", row).gameObject.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.UpperLeft;
                t.text = hintText;
                t.color = Color.white;
                var rt = t.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(0, 18); rt.anchoredPosition = new Vector2(8, y - 86f);
            }
        }

        private void RenderCustomSection(RectTransform parent, string image, string text, string targetId = null)
        {
            float y = -8f; float pad = 8f;
            if (!string.IsNullOrEmpty(image))
            {
                var imgRt = CreateUIObject("Image", parent);
                imgRt.anchorMin = new Vector2(0, 1); imgRt.anchorMax = new Vector2(0, 1); imgRt.pivot = new Vector2(0, 1);
                imgRt.sizeDelta = new Vector2(240, 135); imgRt.anchoredPosition = new Vector2(pad, y);
                var img = imgRt.gameObject.AddComponent<Image>();
                var spr = GetItemSprite(image);
                if (spr == null)
                {
                    try { JustEnoughItems.Plugin.Log?.LogWarning($"JEI Custom Image not found: {image}"); } catch { }
                }
                else img.sprite = spr;
                img.preserveAspect = true;
                y -= imgRt.sizeDelta.y + 6f;
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var tgt = CreateUIObject("Target", parent);
                tgt.anchorMin = new Vector2(0, 1); tgt.anchorMax = new Vector2(0, 1); tgt.pivot = new Vector2(0, 1);
                tgt.sizeDelta = new Vector2(80, 80); tgt.anchoredPosition = new Vector2(pad, y);
                CreateIconWithName(tgt, targetId, GetDisplayName(targetId), () => OpenById(targetId));
                y -= 86f;
            }

            if (!string.IsNullOrEmpty(text))
            {
                var t = CreateUIObject("Text", parent).gameObject.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.UpperLeft;
                t.text = text;
                t.color = Color.white;
                var rt = t.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(0, 18); rt.anchoredPosition = new Vector2(pad, y);
            }
        }

        private float CreateArrow(RectTransform parent, float x, float y)
        {
            var ar = CreateUIObject("Arrow", parent);
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = new Vector2(0, 1); ar.pivot = new Vector2(0, 1);
            ar.sizeDelta = new Vector2(24, 24); ar.anchoredPosition = new Vector2(x, y - 28f);
            var txt = CreateUIObject("Text", ar).gameObject.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = ">>";
            txt.color = Color.white;
            var tRt = txt.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            return x + 30f;
        }

        private void CreateIconWithName(RectTransform parent, string idOrFile, string displayName, Action onClick)
        {
            var iconRt = CreateUIObject("Icon", parent);
            iconRt.anchorMin = new Vector2(0, 1); iconRt.anchorMax = new Vector2(0, 1); iconRt.pivot = new Vector2(0, 1);
            iconRt.sizeDelta = new Vector2(64, 64); iconRt.anchoredPosition = new Vector2(8, -8);
            var img = iconRt.gameObject.AddComponent<Image>();
            var spr = GetItemSprite(string.IsNullOrEmpty(idOrFile) ? "" : idOrFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || idOrFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? idOrFile : idOrFile);
            if (spr != null) img.sprite = spr; img.preserveAspect = true;
            var btn = iconRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img; btn.onClick.AddListener(() => { try { onClick?.Invoke(); } catch { } });
            // 悬浮提示（显示显示名+ID）
            var trig = iconRt.gameObject.GetComponent<EventTrigger>();
            if (trig == null) trig = iconRt.gameObject.AddComponent<EventTrigger>();
            trig.triggers ??= new List<EventTrigger.Entry>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { try { var txt = string.IsNullOrEmpty(displayName) ? (idOrFile ?? "") : displayName; _tooltipText.text = txt; _tooltipRt.gameObject.SetActive(true); } catch { } });
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { try { HideTooltip(); } catch { } });
            trig.triggers.Add(exit);

            var nameRt = CreateUIObject("Name", parent);
            nameRt.anchorMin = new Vector2(0, 1); nameRt.anchorMax = new Vector2(0, 1); nameRt.pivot = new Vector2(0, 1);
            nameRt.sizeDelta = new Vector2(100, 16); nameRt.anchoredPosition = new Vector2(8, -74);
            var nameText = nameRt.gameObject.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.text = string.IsNullOrEmpty(displayName) ? (idOrFile ?? "") : displayName;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
        }

        private void CreateIconWithCount(RectTransform parent, string idOrFile, int count, Action onClick)
        {
            var iconRt = CreateUIObject("Icon", parent);
            iconRt.anchorMin = new Vector2(0, 1); iconRt.anchorMax = new Vector2(0, 1); iconRt.pivot = new Vector2(0, 1);
            iconRt.sizeDelta = new Vector2(64, 64); iconRt.anchoredPosition = new Vector2(8, -8);
            var img = iconRt.gameObject.AddComponent<Image>();
            var spr = GetItemSprite(idOrFile);
            if (spr != null) img.sprite = spr; img.preserveAspect = true;
            var btn = iconRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img; btn.onClick.AddListener(() => { try { onClick?.Invoke(); } catch { } });
            // 悬浮提示（显示显示名+ID）
            var trig = iconRt.gameObject.GetComponent<EventTrigger>();
            if (trig == null) trig = iconRt.gameObject.AddComponent<EventTrigger>();
            trig.triggers ??= new List<EventTrigger.Entry>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { try { ShowTooltip(idOrFile); } catch { } });
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { try { HideTooltip(); } catch { } });
            trig.triggers.Add(exit);

            // 名称（游戏显示名）
            var nameRt = CreateUIObject("Name", parent);
            nameRt.anchorMin = new Vector2(0, 1); nameRt.anchorMax = new Vector2(0, 1); nameRt.pivot = new Vector2(0, 1);
            nameRt.sizeDelta = new Vector2(64, 14); nameRt.anchoredPosition = new Vector2(8, -74);
            var nameText = nameRt.gameObject.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.alignment = TextAnchor.UpperCenter;
            nameText.text = GetDisplayName(idOrFile);
            nameText.color = Color.white;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 8;
            nameText.resizeTextMaxSize = 14;
            nameText.raycastTarget = false;

            var cntRt = CreateUIObject("Count", parent);
            cntRt.anchorMin = new Vector2(0, 1); cntRt.anchorMax = new Vector2(0, 1); cntRt.pivot = new Vector2(0, 1);
            cntRt.sizeDelta = new Vector2(64, 14); cntRt.anchoredPosition = new Vector2(8, -90);
            var cntText = cntRt.gameObject.AddComponent<Text>();
            cntText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            cntText.alignment = TextAnchor.UpperCenter;
            cntText.text = count > 1 ? ("x" + count) : "x1";
            cntText.color = Color.white;
            cntText.raycastTarget = false;
        }

        // 从游戏解析显示名称（支持 TechType）
        private object ResolveTechType(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var t = AccessTools.TypeByName("TechType");
                if (t == null) return null;
                var name = token.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase) ? token.Substring("TechType.".Length) : token;
                return Enum.Parse(t, name, true);
            }
            catch { }
            return null;
        }

        private string GetDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            try
            {
                try { UnityEngine.Debug.Log($"[JEI][UI][Name] enter id='{id}'"); } catch { }
                // 若是路径，先取文件名（去扩展）
                string token = id;
                if (token.Contains("/") || token.Contains("\\"))
                {
                    try { token = System.IO.Path.GetFileNameWithoutExtension(token); } catch { }
                }
                // 去除 "TechType." 前缀（与 DetailPage 保持一致的规范化）
                if (token.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase))
                {
                    try { token = token.Substring("TechType.".Length); } catch { }
                }
                

                // 优先：名称缓存命中
                try
                {
                    if (JustEnoughItems.Plugin.NameCache != null && JustEnoughItems.Plugin.NameCache.TryGetValue(token, out var cached) && !string.IsNullOrEmpty(cached))
                    {
                        return cached;
                    }
                }
                catch { }
                // 仅通过独立 JSON 名称映射：ConfigService.ChineseNames
                try
                {
                    try { JustEnoughItems.Config.ConfigService.EnsureNamesLoaded(); } catch { }
                    var dict = JustEnoughItems.Config.ConfigService.ChineseNames;
                    if (dict != null && dict.TryGetValue(token, out var name) && !string.IsNullOrEmpty(name))
                    {
                        try { JustEnoughItems.Plugin.NameCache[token] = name; } catch { }
                        return name;
                    }
                }
                catch { }
            }
            catch { }
            return id;
        }

        // 解析工作台显示名：优先从 jei-fabricators.json 中按 Id 匹配的 DisplayName
        private string ResolveFabricatorDisplayName(string fabricatorId)
        {
            try
            {
                if (string.IsNullOrEmpty(fabricatorId)) return "<未配置工作台>";
                var list = JustEnoughItems.Config.FabricatorOverridesService.Current;
                foreach (var ov in list)
                {
                    if (ov == null) continue;
                    if (string.Equals(ov.Id ?? string.Empty, fabricatorId, StringComparison.OrdinalIgnoreCase))
                    {
                        var n = ov.DisplayName;
                        if (!string.IsNullOrEmpty(n)) return n;
                        break;
                    }
                }
            }
            catch { }
            return fabricatorId;
        }

        private void OpenById(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                var all = JustEnoughItems.Config.ConfigService.Current.Items?.ToList() ?? new List<JeiItem>();
                var it = all.FirstOrDefault(x => string.Equals(x?.ItemId, id, StringComparison.OrdinalIgnoreCase));
                Show(it, id);
            }
            catch { }
        }
    }

    // 顶层处理器：用于自动接线图标的点击与悬浮（避免嵌套类无法 AddComponent）
    public class JeiItemHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public JeiUI ui;
        public JeiItem data;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ui != null && data != null)
            {
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI IPointerClick -> {data.ItemId}"); } catch { }
                ui.Show(data, data.ItemId);
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ui != null && data != null)
            {
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI PointerEnter -> {data.ItemId}"); } catch { }
                ui.ShowTooltip(data);
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (ui != null)
            {
                try { JustEnoughItems.Plugin.Log?.LogInfo("JEI PointerExit"); } catch { }
                ui.HideTooltip();
            }
        }
    }
}
