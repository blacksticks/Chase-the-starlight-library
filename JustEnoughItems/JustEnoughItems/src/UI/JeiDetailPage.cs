using JustEnoughItems.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JustEnoughItems;

namespace JustEnoughItems.UI
{
    public class JeiDetailPage : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Button tabSourceButton;
        [SerializeField] private Button tabUsageButton;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Button closeButton;

        private JeiItem _data;
        private int _shownFrame = -1;
        private float _contentAccumulatedHeight = 0f;
        private GameObject _prefabMethodTabButton;
        private GameObject _prefabCraftLine;
        private RectTransform _methodTabs;
        private RectTransform _methodDetail;

        private void Awake()
        {
            try
            {
                // 运行时自动接线（按规范化路径查找）
                if (titleText == null) titleText = transform.Find("Header/TitleText")?.GetComponent<Text>();
                if (closeButton == null) closeButton = transform.Find("Header/CloseButton")?.GetComponent<Button>();
                if (closeButton == null)
                {
                    // 回退：在整棵子树中按名称匹配 "CloseButton"
                    var allBtns = GetComponentsInChildren<Button>(true);
                    foreach (var b in allBtns)
                    {
                        if (string.Equals(b.gameObject.name, "CloseButton", StringComparison.Ordinal))
                        {
                            closeButton = b; break;
                        }
                    }
                }
                if (tabSourceButton == null) tabSourceButton = transform.Find("Tabs/Tab_Source")?.GetComponent<Button>();
                if (tabUsageButton == null) tabUsageButton = transform.Find("Tabs/Tab_Usage")?.GetComponent<Button>();
                // 回退：在整棵子树中按名称查找页签按钮
                if (tabSourceButton == null)
                {
                    foreach (var b in GetComponentsInChildren<Button>(true))
                    {
                        if (string.Equals(b.gameObject.name, "Tab_Source", StringComparison.Ordinal)) { tabSourceButton = b; break; }
                    }
                }
                if (tabUsageButton == null)
                {
                    foreach (var b in GetComponentsInChildren<Button>(true))
                    {
                        if (string.Equals(b.gameObject.name, "Tab_Usage", StringComparison.Ordinal)) { tabUsageButton = b; break; }
                    }
                }
                if (contentRoot == null) contentRoot = transform.Find("Body/Scroll View/Viewport/Content") as RectTransform;
                if (contentRoot == null) contentRoot = transform.Find("Body/ScrollView/Viewport/Content") as RectTransform;
                if (contentRoot == null) contentRoot = transform.Find("Scroll View/Viewport/Content") as RectTransform;
                if (contentRoot == null) contentRoot = transform.Find("ScrollView/Viewport/Content") as RectTransform;
                if (contentRoot == null)
                {
                    // 最后回退：从整棵子树搜寻名为 Content 的 RectTransform
                    foreach (var rt in GetComponentsInChildren<RectTransform>(true))
                    {
                        if (string.Equals(rt.gameObject.name, "Content", StringComparison.Ordinal)) { contentRoot = rt; break; }
                    }
                    try { if (contentRoot == null) JustEnoughItems.Plugin.Log?.LogWarning("JEI: contentRoot not found (path + fallback failed)"); } catch { }
                }

                if (closeButton != null)
                {
                    // 确保按钮具备目标 Graphic 以显示高亮
                    var img = closeButton.targetGraphic as Image ?? closeButton.GetComponent<Image>();
                    if (img == null) img = closeButton.gameObject.AddComponent<Image>();
                    img.raycastTarget = true;
                    closeButton.targetGraphic = img;
                    closeButton.transition = Selectable.Transition.ColorTint;
                    closeButton.onClick.RemoveAllListeners();
                    closeButton.onClick.AddListener(() => { try { JustEnoughItems.Plugin.Log?.LogInfo("JEI: CloseButton clicked (DetailPage)"); } catch { } try { JeiManager.Hide(); } catch { } });
                }
                if (tabSourceButton != null)
                {
                    var img = tabSourceButton.targetGraphic as Image ?? tabSourceButton.GetComponent<Image>();
                    if (img == null) img = tabSourceButton.gameObject.AddComponent<Image>();
                    img.raycastTarget = true;
                    tabSourceButton.targetGraphic = img;
                    tabSourceButton.transition = Selectable.Transition.ColorTint;
                    tabSourceButton.onClick.RemoveAllListeners();
                    tabSourceButton.onClick.AddListener(() => { try { JustEnoughItems.Plugin.Log?.LogInfo("JEI: Tab clicked -> Source"); } catch { } try { RenderSource(); } catch { } });
                }
                if (tabUsageButton != null)
                {
                    var img = tabUsageButton.targetGraphic as Image ?? tabUsageButton.GetComponent<Image>();
                    if (img == null) img = tabUsageButton.gameObject.AddComponent<Image>();
                    img.raycastTarget = true;
                    tabUsageButton.targetGraphic = img;
                    tabUsageButton.transition = Selectable.Transition.ColorTint;
                    tabUsageButton.onClick.RemoveAllListeners();
                    tabUsageButton.onClick.AddListener(() => { try { JustEnoughItems.Plugin.Log?.LogInfo("JEI: Tab clicked -> Usage"); } catch { } try { RenderUsage(); } catch { } });
                }
                try
                {
                    if (tabSourceButton == null) JustEnoughItems.Plugin.Log?.LogWarning("JEI: Tab_Source button not found (path and fallback both failed)");
                    if (tabUsageButton == null) JustEnoughItems.Plugin.Log?.LogWarning("JEI: Tab_Usage button not found (path and fallback both failed)");
                }
                catch { }
                ClearContent();
            }
            catch { }
        }

        public void Init(JeiItem data)
        {
            _data = data;
            try
            {
                // 确保图标缓存进行一次性初始化（任意入口首次打开详情页均会触发，之后不再读取）
                try { JustEnoughItems.IconCache.InitializeOnce(); } catch { }
                try { _shownFrame = Time.frameCount; } catch { _shownFrame = -1; }
                TryLoadPrefabsFromBundle();
                if (titleText != null)
                {
                    var title = string.IsNullOrEmpty(_data?.DisplayName) ? GetDisplayName(_data?.ItemId) : _data.DisplayName;
                    titleText.text = title;
                }
                if (_data?.Source != null && _data.Source.Count > 0) RenderSource();
                else if (_data?.Usage != null && _data.Usage.Count > 0) RenderUsage();
                else ShowEmpty("没有可显示的来源/用途");
            }
            catch { }
        }

        private void RenderSource()
        {
            ClearContent();
            var list = _data?.Source;
            if (list == null || list.Count == 0) { ShowEmpty("没有配置来源信息"); return; }
            EnsureMethodContainers();
            BuildMethodTabs(list.Count, (i) => GetSourceTabIcon(list[i]), (i) => RenderSourceDetail(list[i]));
            RenderSourceDetail(list[0]);
        }

        private void RenderUsage()
        {
            ClearContent();
            var list = _data?.Usage;
            if (list == null || list.Count == 0) { ShowEmpty("没有配置用途信息"); return; }
            // 方案B：用途侧按工作台合并；同一目标可出现在多个工作台页签
            var grouped = GroupUsageByFabricator(list, _data?.ItemId);
            if (grouped == null || grouped.Count == 0) { ShowEmpty("没有配置用途信息"); return; }
            EnsureMethodContainers();
            BuildMethodTabs(grouped.Count, (i) => GetUsageTabIcon(grouped[i]), (i) => RenderUsageDetail(grouped[i]));
            RenderUsageDetail(grouped[0]);
        }

        private void EnsureMethodContainers()
        {
            if (contentRoot == null) return;
            for (int i = contentRoot.childCount - 1; i >= 0; i--) Destroy(contentRoot.GetChild(i).gameObject);
            _methodTabs = new GameObject("MethodTabs", typeof(RectTransform)).GetComponent<RectTransform>();
            _methodTabs.SetParent(contentRoot, false);
            _methodTabs.anchorMin = new Vector2(0, 1);
            _methodTabs.anchorMax = new Vector2(1, 1);
            _methodTabs.pivot = new Vector2(0.5f, 1);
            _methodTabs.sizeDelta = new Vector2(0, 40);
            _methodTabs.anchoredPosition = new Vector2(0, -4);
            var hlg = _methodTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            _methodDetail = new GameObject("MethodDetail", typeof(RectTransform)).GetComponent<RectTransform>();
            _methodDetail.SetParent(contentRoot, false);
            // 顶部锚定，向下延展，避免覆盖方法页签
            _methodDetail.anchorMin = new Vector2(0, 1);
            _methodDetail.anchorMax = new Vector2(1, 1);
            _methodDetail.pivot = new Vector2(0.5f, 1);
            _methodDetail.anchoredPosition = new Vector2(0, -4);
            _methodDetail.sizeDelta = new Vector2(0, 0);
        }

        private void BuildMethodTabs(int count, Func<int, Sprite> getIcon, Action<int> onClick)
        {
            if (_methodTabs == null) return;
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                GameObject btnGo = null;
                if (_prefabMethodTabButton != null)
                {
                    btnGo = Instantiate(_prefabMethodTabButton, _methodTabs);
                }
                else
                {
                    btnGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(_methodTabs, false);
                }
                btnGo.name = $"MethodTab_{i + 1}";
                var btn = btnGo.GetComponent<Button>() ?? btnGo.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { try { onClick(captured); } catch { } });

                // 优先使用子节点 Icon 显示图标；按钮背景交由 Unity 预制体控制
                Image iconImg = null;
                var t = btnGo.transform.Find("Icon");
                if (t != null) iconImg = t.GetComponent<Image>();
                if (iconImg == null)
                {
                    iconImg = btnGo.GetComponent<Image>() ?? btnGo.AddComponent<Image>();
                }
                var spr = getIcon?.Invoke(i);
                if (iconImg != null)
                {
                    iconImg.sprite = spr; iconImg.color = Color.white; iconImg.raycastTarget = true; iconImg.preserveAspect = true; iconImg.type = Image.Type.Simple;
                }
            }
        }

        private void RenderSourceDetail(JeiSourceTab tab)
        {
            if (_methodDetail == null) return;
            for (int i = _methodDetail.childCount - 1; i >= 0; i--) Destroy(_methodDetail.GetChild(i).gameObject);
            if (tab == null) return;
            if (tab.IfFabricator)
            {
                RenderFabricatorLine(
                    _methodDetail,
                    tab.Fabricator,
                    tab.Ingredient ?? new List<string>(),
                    _data?.ItemId ?? string.Empty,
                    tab.Text,
                    includeOwnerAsIngredient: false,
                    fabricatorIconOverride: string.IsNullOrEmpty(tab.Patch) ? null : tab.Patch,
                    usageLeftSingle: false,
                    usageLeftId: null,
                    fabricatorDisplayName: tab.FabricatorDisplayName
                );
            }
            else
            {
                var patch = string.IsNullOrEmpty(tab.Patch) ? tab.Image : tab.Patch;
                RenderCustomImage(_methodDetail, patch, tab.Text);
            }
        }

        private void RenderUsageDetail(JeiUsageTab tab)
        {
            if (_methodDetail == null) return;
            for (int i = _methodDetail.childCount - 1; i >= 0; i--) Destroy(_methodDetail.GetChild(i).gameObject);
            if (tab == null) return;
            if (tab.IfFabricator)
            {
                // 用途侧：忽略 Ingredient，右侧网格改为使用 Target 列表；左侧固定为当前物品
                var targets = tab.Target ?? new List<string>();
                try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI: Usage targets count={targets.Count}; sample=[{string.Join(",", targets.Take(Mathf.Min(3, targets.Count)).ToArray())}]"); } catch { }
                RenderFabricatorLine(
                    _methodDetail,
                    tab.Fabricator,
                    targets,
                    string.Empty,
                    tab.Text,
                    includeOwnerAsIngredient: false,
                    fabricatorIconOverride: string.IsNullOrEmpty(tab.Patch) ? null : tab.Patch,
                    usageLeftSingle: true,
                    usageLeftId: _data?.ItemId,
                    fabricatorDisplayName: tab.FabricatorDisplayName
                );
            }
            else
            {
                var patch = string.IsNullOrEmpty(tab.Patch) ? tab.Image : tab.Patch;
                string firstTarget = (tab.Target != null && tab.Target.Count > 0) ? tab.Target[0] : null;
                RenderCustomImage(_methodDetail, patch, tab.Text, firstTarget);
            }
        }

        private void RenderFabricatorLine(RectTransform parent, string fabricatorId, List<string> ingredients, string resultId, string hintText, bool includeOwnerAsIngredient = false, string fabricatorIconOverride = null, bool usageLeftSingle = false, string usageLeftId = null, string fabricatorDisplayName = null)
        {
            // 仅创建行，不做任何布局设定（交由 Unity 控制）
            bool fromPrefab = _prefabCraftLine != null;
            GameObject lineGo = fromPrefab ? Instantiate(_prefabCraftLine, parent) : new GameObject("CraftLine", typeof(RectTransform));
            var lineRt = lineGo.GetComponent<RectTransform>();
            lineGo.SetActive(true);

            // 方案A：仅在 Row 下查找 LeftGroup/Arrow/RightGroup；若无 Row，则不渲染行内容
            var row = lineGo.transform.Find("Row") as RectTransform;
            if (row == null)
            {
                try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: CraftLine missing Row; skip rendering groups"); } catch { }
                return;
            }
            Transform searchRoot = (Transform)row;

            // 左中右基础区块（兼容 LeftGroup/RightGroup 命名）
            var left = (searchRoot.Find("LeftGroup") as RectTransform) ?? (searchRoot.Find("Left") as RectTransform);
            if (left == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: Row missing LeftGroup"); } catch { } return; }
            left.gameObject.SetActive(true);

            var arrow = searchRoot.Find("Arrow") as RectTransform;
            if (arrow == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: Row missing Arrow"); } catch { } return; }
            arrow.gameObject.SetActive(true);

            var right = (searchRoot.Find("RightGroup") as RectTransform) ?? (searchRoot.Find("Right") as RectTransform);
            if (right == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: Row missing RightGroup"); } catch { } return; }
            right.gameObject.SetActive(true);

            // 工作台渲染：优先注入 CraftLine/TopFabricator 的 Image，仅设置图标，不改布局
            var fabIconIdOrPath = string.IsNullOrEmpty(fabricatorIconOverride) ? fabricatorId : fabricatorIconOverride;
            try { UnityEngine.Debug.Log($"JEI IconDBG[FabTop]: fabricatorId='{fabricatorId}', use='{fabIconIdOrPath}'"); } catch { }
            var topFab = lineGo.transform.Find("TopFabricator") as RectTransform;
            if (topFab == null)
            {
                var t = FindDeep(lineGo.transform, "TopFabricator");
                if (t != null) topFab = t as RectTransform;
            }
            if (topFab != null)
            {
                topFab.gameObject.SetActive(true);
                // 仅设置图标到 TopFabricator 体系内的 Image，不改动尺寸/位置/白底/交互
                Image topFabImg = null;
                // 1) 优先名为 "Image" 的子节点
                var childIcon = topFab.Find("Image");
                if (childIcon != null)
                {
                    topFabImg = childIcon.GetComponent<Image>() ?? childIcon.gameObject.AddComponent<Image>();
                }
                // 2) 其次任选 TopFabricator 子树中的第一个 Image
                if (topFabImg == null)
                {
                    foreach (var img in topFab.GetComponentsInChildren<Image>(true))
                    {
                        if (img != null) { topFabImg = img; break; }
                    }
                }
                // 3) 最后使用自身 Image
                if (topFabImg == null)
                {
                    topFabImg = topFab.GetComponent<Image>() ?? topFab.gameObject.AddComponent<Image>();
                }
                var _fabSprite = LoadSpriteFromIconsSafe(ResolveIconPreferredPath(fabIconIdOrPath));
                try { UnityEngine.Debug.Log($"JEI IconDBG[FabTop]: sprite={(_fabSprite!=null)} id='{fabIconIdOrPath}'"); } catch { }
                topFabImg.sprite = _fabSprite;
                topFabImg.preserveAspect = true; topFabImg.type = Image.Type.Simple;
                // 悬浮提示：显示工作台显示名（来自 jei-fabricators.json）
                // 名称优先使用图标覆盖的 ID（若提供且非路径），否则用 fabricatorId
                bool overrideIsPath = !string.IsNullOrEmpty(fabricatorIconOverride) && (fabricatorIconOverride.Contains("/") || fabricatorIconOverride.Contains("\\") || fabricatorIconOverride.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                var fabIdForName = (!string.IsNullOrEmpty(fabricatorIconOverride) && !overrideIsPath) ? fabricatorIconOverride : fabricatorId;
                var _resolvedFabName = ResolveFabricatorDisplayNameForItem(_data?.ItemId, fabIdForName, fabricatorDisplayName);
                AttachTooltip(topFabImg.gameObject, _resolvedFabName);

                // 显示名称文本：仅写入内容，样式与布局完全由资源包控制
                var nameTf = topFab.Find("Name") ?? FindDeep(topFab, "Name");
                if (nameTf != null)
                {
                    var nameText = nameTf.GetComponent<Text>();
                    if (nameText != null)
                    {
                        // 与悬浮提示一致：优先使用覆盖 ID（非路径）来解析名称
                        var resolvedDisplay = ResolveFabricatorDisplayNameForItem(_data?.ItemId, fabIdForName, fabricatorDisplayName);
                        // 统一使用内置 Arial 字体，避免预制体字体缺少中文字形导致显示异常
                        try { nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                        nameText.text = resolvedDisplay;
                        // 兼容：按用户提供的绝对路径再写一遍，确保目标 Text 被替换
                        try
                        {
                            var absGo = GameObject.Find("uGUI_PDAScreen(Clone)/Content/DetailPageRoot_Instance/Body/Inset/Scroll View/ViewPort/Content/MethodDetail/CraftLine(Clone)/TopFabricator/Name");
                            if (absGo != null)
                            {
                                var absTxt = absGo.GetComponent<Text>();
                                if (absTxt != null)
                                {
                                    try { absTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                                    absTxt.text = resolvedDisplay;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            if (usageLeftSingle && !string.IsNullOrEmpty(usageLeftId))
            {
                // 用途：左侧按矩阵渲染唯一当前物品（仅使用预制体已有网格容器）
                RectTransform ingBox = (left.Find("Ingredients") as RectTransform)
                                         ?? (left.Find("Usage") as RectTransform)
                                         ?? (left.Find("Grid") as RectTransform)
                                         ?? (left.Find("LeftGrid") as RectTransform);
                if (ingBox == null)
                {
                    foreach (var glg in left.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true))
                    {
                        if (glg != null) { ingBox = glg.GetComponent<RectTransform>(); break; }
                    }
                }
                if (ingBox == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: LeftGroup missing Ingredients/Usage/Grid/LeftGrid (no GridLayoutGroup found)"); } catch { } }
                if (ingBox != null)
                {
                    var cell = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
                    cell.SetParent(ingBox, false);
                    // 单个物品计数视为1
                    CreateIconWithCount(cell, usageLeftId, 1, () => OpenById(usageLeftId));
                }
            }
            else
            {
                // 来源：左侧为配方材料（兼容新命名 Content 与旧命名 Ingredients）
                RectTransform ingBox = (left.Find("Ingredients") as RectTransform)
                                         ?? (left.Find("Content") as RectTransform);
                if (ingBox == null)
                {
                    // 进一步兼容：在 LeftGroup 子树中寻找任意 GridLayoutGroup 作为容器
                    foreach (var glg in left.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true))
                    {
                        if (glg != null) { ingBox = glg.GetComponent<RectTransform>(); break; }
                    }
                    if (ingBox == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: LeftGroup missing Ingredients/Content (no GridLayoutGroup found)"); } catch { } }
                }
                var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ingredients) { if (string.IsNullOrEmpty(id)) continue; groups[id] = groups.TryGetValue(id, out var n) ? n + 1 : 1; }
                if (includeOwnerAsIngredient && !string.IsNullOrEmpty(_data?.ItemId)) { var k = _data.ItemId; groups[k] = groups.TryGetValue(k, out var n) ? n + 1 : 1; }
                foreach (var kv in groups)
                {
                    if (ingBox != null)
                    {
                        var cell = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
                        cell.SetParent(ingBox, false);
                        CreateIconWithCount(cell, kv.Key, kv.Value, () => OpenById(kv.Key));
                    }
                }
            }

            // 右侧内容（方案A）：用途侧为网格，来源侧为唯一结果
            if (usageLeftSingle)
            {
                // 用途侧：RightGroup/Usages 网格（仅使用预制体已有）。兼容多种命名。
                RectTransform usagesBox = (right.Find("Usages") as RectTransform)
                                            ?? (right.Find("Usage") as RectTransform)
                                            ?? (right.Find("Grid") as RectTransform)
                                            ?? (right.Find("RightGrid") as RectTransform)
                                            ?? (right.Find("Result") as RectTransform);
                string usagesBoxName = usagesBox != null ? usagesBox.name : null;
                if (usagesBox == null)
                {
                    // 进一步兼容：寻找任意 GridLayoutGroup 容器
                    foreach (var glg in right.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true))
                    {
                        if (glg != null) { usagesBox = glg.GetComponent<RectTransform>(); usagesBoxName = usagesBox?.name; break; }
                    }
                }
                if (usagesBox == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: RightGroup missing Usages/Usage/Grid/RightGrid (no GridLayoutGroup found)"); } catch { } }
                else { try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI: Right usages container = {usagesBoxName}"); } catch { } }
                var groupsR = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ingredients) { if (string.IsNullOrEmpty(id)) continue; groupsR[id] = groupsR.TryGetValue(id, out var n) ? n + 1 : 1; }
                foreach (var kv in groupsR)
                {
                    if (usagesBox != null)
                    {
                        var cell = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
                        cell.SetParent(usagesBox, false);
                        CreateIconWithCount(cell, kv.Key, kv.Value, () => OpenById(kv.Key));
                    }
                }
            }
            else
            {
                // 来源侧：优先按网格容器渲染单个结果；若无则回退到 Result 节点
                RectTransform usagesBoxR = (right.Find("Usages") as RectTransform)
                                            ?? (right.Find("Usage") as RectTransform)
                                            ?? (right.Find("Grid") as RectTransform)
                                            ?? (right.Find("RightGrid") as RectTransform);
                if (usagesBoxR == null)
                {
                    foreach (var glg in right.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true))
                    {
                        if (glg != null) { usagesBoxR = glg.GetComponent<RectTransform>(); break; }
                    }
                }
                if (usagesBoxR != null)
                {
                    var cell = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
                    cell.SetParent(usagesBoxR, false);
                    // 单个结果计数视为1
                    CreateIconWithCount(cell, resultId, 1, () => OpenById(resultId));
                }
                else
                {
                    // 回退：Result 单节点渲染
                    var res = (right.Find("Result") as RectTransform);
                    if (res == null) { try { JustEnoughItems.Plugin.Log?.LogWarning("JEI: RightGroup missing Result"); } catch { } return; }
                    var resName = string.IsNullOrEmpty(resultId) ? "?" : GetDisplayName(resultId);
                    if (res.Find("Icon") != null)
                        SetIconWithNameOnContainer(res, resultId, resName, () => OpenById(resultId), clearChildren: false);
                    else
                        CreateIconWithName(res, resultId, resName, () => OpenById(resultId));
                }
            }

            if (!string.IsNullOrEmpty(hintText))
            {
                var tip = lineGo.transform.Find("Tooltip") as RectTransform;
                if (tip == null) { tip = new GameObject("Tooltip", typeof(RectTransform)).GetComponent<RectTransform>(); tip.SetParent(lineRt, false); }
                var t = tip.GetComponent<Text>() ?? tip.gameObject.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.UpperLeft;
                t.color = Color.white;
                t.text = hintText;
                // 轻微上移（锚定到顶部，微小负向偏移）；不更改尺寸，保持布局受 Unity 控制
                var tipRt = tip as RectTransform;
                tipRt.anchorMin = new Vector2(0, 1);
                tipRt.anchorMax = new Vector2(1, 1);
                tipRt.pivot = new Vector2(0, 1);
                tipRt.anchoredPosition = new Vector2(0, -2f);
            }
        }

        private void RenderCustomImage(RectTransform parent, string imageFile, string hintText, string targetId = null)
        {
            var box = new GameObject("ImageSection", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent, false);
            box.anchorMin = new Vector2(0, 1); box.anchorMax = new Vector2(1, 1); box.pivot = new Vector2(0.5f, 1);
            box.sizeDelta = new Vector2(0, 220); box.anchoredPosition = new Vector2(0, -8);

            var img = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(box, false);
            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(256, 192); rt.anchoredPosition = new Vector2(0, -8);
            img.sprite = LoadSpriteFromIconsSafe(string.IsNullOrEmpty(imageFile) ? (targetId ?? _data?.ItemId) : imageFile);
            img.color = Color.white;

            if (!string.IsNullOrEmpty(hintText))
            {
                var t = new GameObject("Hint", typeof(RectTransform)).AddComponent<Text>();
                t.transform.SetParent(box, false);
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.UpperCenter; t.color = Color.white; t.text = hintText;
                var tr = t.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1);
                tr.sizeDelta = new Vector2(0, 22); tr.anchoredPosition = new Vector2(0, -204);
            }
        }

        private void TryLoadPrefabsFromBundle()
        {
            try
            {
                if (_prefabMethodTabButton != null && _prefabCraftLine != null) return;
                foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (string.Equals(b.name, "jeiui", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_prefabMethodTabButton == null) _prefabMethodTabButton = b.LoadAsset<GameObject>("MethodTabButton");
                        if (_prefabCraftLine == null) _prefabCraftLine = b.LoadAsset<GameObject>("CraftLine");
                        break;
                    }
                }
            }
            catch { }
        }

        private Sprite GetSourceTabIcon(JeiSourceTab tab)
        {
            if (tab == null) return LoadPlaceholder();
            if (!tab.IfFabricator && !string.IsNullOrEmpty(tab.TabIcon)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.TabIcon));
            if (!string.IsNullOrEmpty(tab.Patch)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Patch));
            if (tab.IfFabricator && !string.IsNullOrEmpty(tab.Fabricator)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Fabricator));
            if (!string.IsNullOrEmpty(tab.Image)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Image));
            // 非工作台页签兜底图标
            return LoadSpriteFromIconsSafe("icons/nonblueprints/Icon.png");
        }

        private Sprite GetUsageTabIcon(JeiUsageTab tab)
        {
            if (tab == null) return LoadPlaceholder();
            if (!tab.IfFabricator && !string.IsNullOrEmpty(tab.TabIcon)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.TabIcon));
            if (!string.IsNullOrEmpty(tab.Patch)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Patch));
            if (tab.IfFabricator && !string.IsNullOrEmpty(tab.Fabricator)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Fabricator));
            if (!string.IsNullOrEmpty(tab.Image)) return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Image));
            if (tab.Target != null && tab.Target.Count > 0 && !string.IsNullOrEmpty(tab.Target[0]))
                return LoadSpriteFromIconsSafe(ResolveIconPreferredPath(tab.Target[0]));
            // 非工作台页签兜底图标
            return LoadSpriteFromIconsSafe("icons/nonblueprints/Icon.png");
        }

        // 将用途 IfFabricator=true 的 Target，按 jei-fabricators.json 的 IncludeItems 分配到对应工作台分组；
        // 允许一个 Target 同时出现在多个工作台（方案B）。未匹配的放入“未配置工作台”。
        private List<JeiUsageTab> GroupUsageByFabricator(List<JeiUsageTab> original, string ownerItemId)
        {
            try
            {
                var result = new List<JeiUsageTab>();
                if (original == null || original.Count == 0) return result;

                var overrides = FabricatorOverridesService.Current;

                // 分组容器：fabricatorId -> tab 与去重集
                var tabMap = new Dictionary<string, JeiUsageTab>(StringComparer.OrdinalIgnoreCase);
                var setMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                // 先按配置顺序建立分组，保证页签顺序
                foreach (var ov in overrides)
                {
                    if (ov == null) continue;
                    var key = ov.Id ?? string.Empty;
                    if (!tabMap.ContainsKey(key))
                    {
                        tabMap[key] = new JeiUsageTab
                        {
                            IfFabricator = true,
                            Fabricator = key,
                            FabricatorDisplayName = ov.DisplayName ?? string.Empty,
                            Target = new List<string>(),
                            Patch = string.IsNullOrEmpty(ov.Icon) ? string.Empty : ov.Icon
                        };
                        setMap[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                // 未配置工作台分组（最后一个）
                const string UnknownKey = "";
                if (!tabMap.ContainsKey(UnknownKey))
                {
                    tabMap[UnknownKey] = new JeiUsageTab
                    {
                        IfFabricator = true,
                        Fabricator = string.Empty,
                        FabricatorDisplayName = "<未配置工作台>",
                        Target = new List<string>(),
                        Patch = string.Empty
                    };
                    setMap[UnknownKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                // 处理原始用途页签
                var passthrough = new List<JeiUsageTab>();
                foreach (var tab in original)
                {
                    if (tab == null) continue;
                    if (!tab.IfFabricator)
                    {
                        passthrough.Add(tab);
                        continue;
                    }
                    var targets = tab.Target ?? new List<string>();
                    foreach (var t in targets)
                    {
                        if (string.IsNullOrEmpty(t)) continue;
                        // 找到所有命中的工作台（方案B：可多重归属）
                        var matched = new List<string>();
                        foreach (var ov in overrides)
                        {
                            if (ov == null) continue;
                            var arr = ov.IncludeItems ?? new List<string>();
                            bool hit = arr.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase));
                            if (hit) matched.Add(ov.Id ?? string.Empty);
                        }
                        if (matched.Count == 0)
                        {
                            if (!setMap[UnknownKey].Contains(t))
                            {
                                setMap[UnknownKey].Add(t);
                                tabMap[UnknownKey].Target.Add(t);
                            }
                        }
                        else
                        {
                            foreach (var key in matched)
                            {
                                if (!setMap.ContainsKey(key)) setMap[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                if (!tabMap.ContainsKey(key))
                                {
                                    tabMap[key] = new JeiUsageTab
                                    {
                                        IfFabricator = true,
                                        Fabricator = key,
                                        FabricatorDisplayName = key,
                                        Target = new List<string>(),
                                        Patch = string.Empty
                                    };
                                }
                                if (!setMap[key].Contains(t))
                                {
                                    setMap[key].Add(t);
                                    tabMap[key].Target.Add(t);
                                }
                            }
                        }
                    }
                }

                // 输出：按配置顺序的工作台组（仅保留有内容），再追加未配置组（若有内容），最后追加透传页签
                foreach (var ov in overrides)
                {
                    if (ov == null) continue;
                    var key = ov.Id ?? string.Empty;
                    if (tabMap.TryGetValue(key, out var g) && g != null && g.Target != null && g.Target.Count > 0)
                        result.Add(g);
                }
                if (tabMap[UnknownKey].Target.Count > 0) result.Add(tabMap[UnknownKey]);
                result.AddRange(passthrough);

                return result;
            }
            catch { return original ?? new List<JeiUsageTab>(); }
        }

        private Sprite GetItemIconFallback()
        {
            if (!string.IsNullOrEmpty(_data?.Patch)) return LoadSpriteFromIconsSafe(_data.Patch);
            if (!string.IsNullOrEmpty(_data?.Icon)) return LoadSpriteFromIconsSafe(_data.Icon);
            return LoadSpriteFromIconsSafe(_data?.ItemId);
        }

        private Sprite LoadSpriteFromIconsSafe(string nameOrPath)
        {
            try
            {
                if (string.IsNullOrEmpty(nameOrPath)) return LoadPlaceholder();
                var token = nameOrPath.Trim();
                if (System.IO.Path.IsPathRooted(token))
                {
                    var rel = ToIconsRelative(token);
                    if (!string.IsNullOrEmpty(rel)) token = rel;
                }
                // 仅从缓存读取：icons/ 相对路径或 id
                Sprite cached = null;
                if (token.Contains("/") || token.Contains("\\") || token.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    if (token.StartsWith("icons/", StringComparison.OrdinalIgnoreCase))
                        cached = IconCache.GetByIconsRelative(token);
                }
                else
                {
                    cached = IconCache.GetById(token);
                }
                return cached ?? LoadPlaceholder();
            }
            catch { return LoadPlaceholder(); }
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

        private Sprite TryGetGameSpriteById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                var tTechType = AccessType("TechType");
                if (tTechType == null) return null;
                object ttObj = null;
                try { ttObj = Enum.Parse(tTechType, NormalizeId(id), true); UnityEngine.Debug.Log($"JEI IconDBG[Game]: TechType.Parse('{id}') success"); } catch { UnityEngine.Debug.Log($"JEI IconDBG[Game]: TechType.Parse('{id}') failed"); ttObj = null; }
                if (ttObj == null) return null;

                // 优先 CraftData.GetItemSprite（与 JEI.UI 的策略保持一致）
                var tCraftData = AccessType("CraftData");
                if (tCraftData != null)
                {
                    var miGetItemSprite = tCraftData.GetMethod("GetItemSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { tTechType }, null);
                    if (miGetItemSprite != null)
                    {
                        try {
                            var obj = miGetItemSprite.Invoke(null, new object[] { ttObj });
                            var s = TryConvertSprite(obj);
                            UnityEngine.Debug.Log($"JEI IconDBG[Game]: CraftData.GetItemSprite -> type={(obj?.GetType().FullName ?? "<null>")}, sprite={(s!=null)}");
                            if (s != null) return s;
                        } catch { }
                    }
                }
                var tSpriteManager = AccessType("SpriteManager");
                if (tSpriteManager != null)
                {
                    var miGet = tSpriteManager.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { tTechType }, null);
                    if (miGet != null)
                    {
                        try {
                            var obj = miGet.Invoke(null, new object[] { ttObj });
                            var s = TryConvertSprite(obj);
                            UnityEngine.Debug.Log($"JEI IconDBG[Game]: SpriteManager.Get -> type={(obj?.GetType().FullName ?? "<null>")}, sprite={(s!=null)}");
                            if (s != null) return s;
                        } catch { }
                    }
                    var miGetUI = tSpriteManager.GetMethod("GetUISprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { tTechType }, null);
                    if (miGetUI != null)
                    {
                        try {
                            var obj = miGetUI.Invoke(null, new object[] { ttObj });
                            var s = TryConvertSprite(obj);
                            UnityEngine.Debug.Log($"JEI IconDBG[Game]: SpriteManager.GetUISprite -> type={(obj?.GetType().FullName ?? "<null>")}, sprite={(s!=null)}");
                            if (s != null) return s;
                        } catch { }
                    }
                    // 进一步：尝试 Group 重载 (Group, TechType)
                    try
                    {
                        var tGroup = tSpriteManager.GetNestedType("Group", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        var miGet2 = tSpriteManager.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { tGroup, tTechType }, null);
                        if (tGroup != null && miGet2 != null)
                        {
                            var values = Enum.GetValues(tGroup);
                            foreach (var gv in values)
                            {
                                object obj2 = null; Sprite s2 = null;
                                try { obj2 = miGet2.Invoke(null, new object[] { gv, ttObj }); s2 = TryConvertSprite(obj2); } catch { }
                                try { UnityEngine.Debug.Log($"JEI IconDBG[Game]: SpriteManager.Get(group={gv}) -> type={(obj2?.GetType().FullName ?? "<null>")}, sprite={(s2!=null)}"); } catch { }
                                if (s2 != null) return s2;
                            }
                        }
                    }
                    catch { }
                    // 再尝试 Group 重载 (Group, string id)
                    try
                    {
                        var tGroup = tSpriteManager.GetNestedType("Group", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        var miGet3 = tSpriteManager.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { tGroup, typeof(string) }, null);
                        if (tGroup != null && miGet3 != null)
                        {
                            var values = Enum.GetValues(tGroup);
                            foreach (var gv in values)
                            {
                                object obj3 = null; Sprite s3 = null;
                                try { obj3 = miGet3.Invoke(null, new object[] { gv, id }); s3 = TryConvertSprite(obj3); } catch { }
                                try { UnityEngine.Debug.Log($"JEI IconDBG[Game]: SpriteManager.Get(group={gv}, id) -> type={(obj3?.GetType().FullName ?? "<null>")}, sprite={(s3!=null)}"); } catch { }
                                if (s3 != null) return s3;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // 从磁盘路径读取图片并创建 Sprite
        private Sprite LoadSpriteFromPathInternal(string path)
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
                    try { UnityEngine.Debug.Log($"JEI IconDBG[Load]: loaded file sprite size={tex.width}x{tex.height} from '{path}'"); } catch { }
                    return spr;
                }
                else
                {
                    try { UnityEngine.Debug.Log($"JEI IconDBG[Load]: LoadImage failed for '{path}'"); } catch { }
                }
            }
            catch { }
            return null;
        }

        // 兼容 Atlas.Sprite：统一转换为 UnityEngine.Sprite
        private Sprite TryConvertSprite(object obj)
        {
            try
            {
                if (obj == null) return null;
                if (obj is Sprite s) return s;
                var t = obj.GetType();
                var full = t.FullName ?? string.Empty;
                if (full == "Atlas.Sprite" || full.EndsWith("Atlas.Sprite", StringComparison.Ordinal))
                {
                    try
                    {
                        var piSprite = t.GetProperty("sprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                        if (piSprite != null)
                        {
                            var raw = piSprite.GetValue(obj) as Sprite;
                            if (raw != null) return raw;
                        }
                    }
                    catch { }
                    try
                    {
                        var piTex = t.GetProperty("texture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                        var piRect = t.GetProperty("rect", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                        var piPPU = t.GetProperty("pixelsPerUnit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                        var tex = piTex?.GetValue(obj) as Texture2D;
                        var rectObj = piRect?.GetValue(obj);
                        float ppu = 100f;
                        if (piPPU != null) { try { ppu = Convert.ToSingle(piPPU.GetValue(obj)); } catch { } }
                        if (tex != null && rectObj is Rect r && r.width > 0 && r.height > 0)
                        {
                            return Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), ppu);
                        }
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        var pi = t.GetProperty("Sprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
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

        private Sprite LoadPlaceholder()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    tex.SetPixel(x, y, new Color(0.2f, 0.2f, 0.2f, 1));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void CreateIconWithName(RectTransform parent, string id, string display, Action onClick)
        {
            var root = new GameObject("IconWithName", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); root.sizeDelta = new Vector2(80, 80);
            var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(root, false);
            var rtImg = img.GetComponent<RectTransform>(); rtImg.sizeDelta = new Vector2(64, 64); rtImg.anchoredPosition = new Vector2(8, -8);
            try { UnityEngine.Debug.Log($"JEI IconDBG[Name]: id='{id}'"); } catch { }
            var _spr1 = LoadSpriteFromIconsSafe(ResolveIconPreferredPath(id));
            try { UnityEngine.Debug.Log($"JEI IconDBG[Name]: id='{id}', sprite={(_spr1!=null)}"); } catch { }
            img.sprite = _spr1;
            var btn = img.gameObject.AddComponent<Button>(); btn.onClick.AddListener(() => { try { onClick?.Invoke(); } catch { } });
            AttachTooltip(img.gameObject, BuildItemTooltipText(id, 1));
            var txt = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
            txt.transform.SetParent(root, false);
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); txt.alignment = TextAnchor.UpperCenter; txt.color = Color.white; txt.text = display ?? string.Empty;
            var rtTxt = txt.GetComponent<RectTransform>(); rtTxt.sizeDelta = new Vector2(80, 16); rtTxt.anchoredPosition = new Vector2(0, -72);
        }

        private void CreateIconWithCount(RectTransform parent, string id, int count, Action onClick)
        {
            var box = new GameObject("IconWithCount", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent, false); box.sizeDelta = new Vector2(64, 80);
            var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(box, false);
            var rtImg = img.GetComponent<RectTransform>(); rtImg.sizeDelta = new Vector2(56, 56); rtImg.anchoredPosition = new Vector2(4, -4);
            try { UnityEngine.Debug.Log($"JEI IconDBG[Count]: id='{id}', count={count}"); } catch { }
            var _spr2 = LoadSpriteFromIconsSafe(ResolveIconPreferredPath(id));
            try { UnityEngine.Debug.Log($"JEI IconDBG[Count]: id='{id}', sprite={(_spr2!=null)}"); } catch { }
            img.sprite = _spr2;
            var btn = img.gameObject.AddComponent<Button>(); btn.onClick.AddListener(() => { try { onClick?.Invoke(); } catch { } });
            AttachTooltip(img.gameObject, BuildItemTooltipText(id, count));
            var txt = new GameObject("Count", typeof(RectTransform)).AddComponent<Text>();
            txt.transform.SetParent(box, false); txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); txt.alignment = TextAnchor.UpperCenter; txt.color = Color.white; txt.text = count > 1 ? ("*" + count) : string.Empty;
            var rtTxt = txt.GetComponent<RectTransform>(); rtTxt.sizeDelta = new Vector2(64, 16); rtTxt.anchoredPosition = new Vector2(0, -48);
        }

        private void AttachTooltip(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            var et = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et == null) et = target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            RectTransform tip = null; RectTransform tipParent = null;

            UnityEngine.Events.UnityAction<BaseEventData> onEnter = (e) =>
            {
                try
                {
                    var tr = target.transform as RectTransform; if (tr == null) return;
                    // 选择不被裁剪的父级：找到最近的 Mask/RectMask2D 祖先，并使用其父物体作为浮层容器
                    RectTransform FindTooltipParent(Transform start)
                    {
                        Transform cur = start;
                        RectTransform lastRt = start as RectTransform;
                        while (cur != null)
                        {
                            var hasMask = cur.GetComponent<UnityEngine.UI.Mask>() != null || cur.GetComponent<RectMask2D>() != null;
                            if (hasMask)
                            {
                                // 使用带 Mask 节点的父级，跳出裁剪区域
                                return (cur.parent as RectTransform) ?? lastRt;
                            }
                            lastRt = cur as RectTransform;
                            cur = cur.parent;
                        }
                        // 没找到 Mask，使用最后一个可用的 RectTransform（通常是窗口根）
                        return lastRt;
                    }
                    tipParent = FindTooltipParent(tr);
                    if (tipParent == null) return;

                    // 优先：使用预制体自带 HoverTip（兼容 HoverTip 在 ScrollArea 等子层级）
                    try
                    {
                        var prefabTip = tipParent.Find("HoverTip") as RectTransform;
                        if (prefabTip == null)
                        {
                            foreach (var rt in tipParent.GetComponentsInChildren<RectTransform>(true))
                            {
                                if (rt != null && rt.name == "HoverTip") { prefabTip = rt; break; }
                            }
                        }
                        if (prefabTip != null)
                        {
                            prefabTip.gameObject.SetActive(true);
                            var ttxt = prefabTip.Find("Text")?.GetComponent<Text>();
                            if (ttxt != null)
                            {
                                try { ttxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                                ttxt.text = text;
                            }
                            tip = prefabTip;
                            try { UnityEngine.Debug.Log($"[JEI][Detail][HoverTip] prefab used path set to text='{text}'"); } catch { }
                            // 兼容写入：尝试按绝对路径写入 Left/Right 的 HoverTip/Text
                            try
                            {
                                var absLeft = GameObject.Find("uGUI_PDAScreen(Clone)/Content/DetailPageRoot_Instance/Body/Inset/Scroll View/ViewPort/Content/MethodDetail/CraftLine(Clone)/Row/LeftGroup/ScrollArea/HoverTip/Text");
                                if (absLeft != null)
                                {
                                    var leftTxt = absLeft.GetComponent<Text>();
                                    if (leftTxt != null)
                                    {
                                        try { leftTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                                        leftTxt.text = text;
                                    }
                                }
                            }
                            catch { }
                            try
                            {
                                var absRight = GameObject.Find("uGUI_PDAScreen(Clone)/Content/DetailPageRoot_Instance/Body/Inset/Scroll View/ViewPort/Content/MethodDetail/CraftLine(Clone)/Row/RightGroup/ScrollAreaRight/HoverTip/Text");
                                if (absRight != null)
                                {
                                    var rightTxt = absRight.GetComponent<Text>();
                                    if (rightTxt != null)
                                    {
                                        try { rightTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                                        rightTxt.text = text;
                                    }
                                }
                            }
                            catch { }
                            return;
                        }
                    }
                    catch { }

                    if (tip != null)
                    {
                        tip.gameObject.SetActive(true);
                        var ttxt = tip.Find("Text")?.GetComponent<Text>();
                        if (ttxt != null)
                        {
                            try { ttxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                            ttxt.text = text;
                        }
                        return;
                    }
                    tip = new GameObject("HoverTip", typeof(RectTransform)).GetComponent<RectTransform>();
                    tip.SetParent(tipParent, false);
                    // 统一用中心锚点，后续用转换坐标把它放到“图标下方”
                    tip.anchorMin = new Vector2(0.5f, 0.5f);
                    tip.anchorMax = new Vector2(0.5f, 0.5f);
                    tip.pivot = new Vector2(0.5f, 1f); // 顶部对齐，向下展开
                    tip.sizeDelta = new Vector2(260, 40);
                    var bg = tip.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.6f); bg.raycastTarget = false; tip.SetAsLastSibling();
                    var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
                    t.transform.SetParent(tip, false);
                    t.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); t.alignment = TextAnchor.UpperLeft; t.color = Color.white; t.text = text; t.resizeTextForBestFit = false; t.raycastTarget = false;
                    var trt = t.GetComponent<RectTransform>();
                    // 填满背景并留出内边距（6px）
                    trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0, 1);
                    trt.sizeDelta = new Vector2(-12, -12); trt.anchoredPosition = new Vector2(6, -6);

                    // 计算“图标底部中心”在 tipParent 下的本地坐标，将浮窗放在其正下方（偏移 6px）
                    var corners = new Vector3[4];
                    tr.GetWorldCorners(corners); // 0:左下 1:左上 2:右上 3:右下
                    var bottomCenterWorld = (corners[0] + corners[3]) * 0.5f;
                    var screen = RectTransformUtility.WorldToScreenPoint(null, bottomCenterWorld);
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tipParent, screen, null, out local))
                    {
                        tip.anchoredPosition = local + new Vector2(0, -6f);
                    }
                }
                catch { }
            };

            UnityEngine.Events.UnityAction<BaseEventData> onExit = (e) =>
            {
                try { if (tip != null) tip.gameObject.SetActive(false); } catch { }
            };

            var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener(onEnter);
            et.triggers.Add(entryEnter);

            var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            entryExit.callback.AddListener(onExit);
            et.triggers.Add(entryExit);
        }

        private string BuildItemTooltipText(string id, int count)
        {
            var idNorm = NormalizeId(id);
            var name = GetDisplayName(idNorm);
            if (count > 1)
            {
                // 仅显示显示名与数量（* 表示数量），不再显示物品ID
                var txt = $"{name} * {count}";
                try { UnityEngine.Debug.Log($"[JEI][Detail][TooltipText] id='{id}' norm='{idNorm}' text='{txt}'"); } catch { }
                return txt;
            }
            try { UnityEngine.Debug.Log($"[JEI][Detail][TooltipText] id='{id}' norm='{idNorm}' text='{name}'"); } catch { }
            return name;
        }

        // 在指定根下按名称深度查找第一个命中的 Transform
        private Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur != null && cur.name == name) return cur;
                for (int i = 0; i < cur.childCount; i++) stack.Push(cur.GetChild(i));
            }
            return null;
        }

        private string GetDescription(string id) { return string.Empty; }

        private void SetIconWithNameOnContainer(RectTransform container, string idOrPath, string display, Action onClick, bool clearChildren = true)
        {
            if (container == null) return;
            // 可选清空容器（默认清）
            if (clearChildren)
            {
                for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
            }

            // 复用或创建图标节点
            var iconTr = container.Find("Icon") as RectTransform;
            Image img;
            if (iconTr == null)
            {
                img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(container, false);
            }
            else
            {
                img = iconTr.GetComponent<Image>();
                if (img == null) img = iconTr.gameObject.AddComponent<Image>();
            }
            var rtImg = img.GetComponent<RectTransform>();
            // 若容器已有预设尺寸，则尽量留边；否则使用默认 64x64
            var size = container.sizeDelta;
            if (size.x <= 0 || size.y <= 0) size = new Vector2(84, 84);
            var iconSize = new Vector2(Mathf.Min(64, size.x - 16), Mathf.Min(64, size.y - 20));
            rtImg.sizeDelta = iconSize;
            rtImg.anchorMin = new Vector2(0, 1); rtImg.anchorMax = new Vector2(0, 1); rtImg.pivot = new Vector2(0, 1);
            rtImg.anchoredPosition = new Vector2(8, -8);
            img.sprite = LoadSpriteFromIconsSafe(ResolveIconPreferredPath(idOrPath));
            var btn = img.gameObject.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => { try { onClick(); } catch { } });

            // 复用或创建名称节点
            var nameTr = container.Find("Name") as RectTransform;
            Text txt;
            if (nameTr == null)
            {
                txt = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
                txt.transform.SetParent(container, false);
            }
            else
            {
                txt = nameTr.GetComponent<Text>();
                if (txt == null) txt = nameTr.gameObject.AddComponent<Text>();
            }
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.UpperLeft; txt.color = Color.white; txt.text = display ?? string.Empty;
            var rtTxt = txt.GetComponent<RectTransform>();
            rtTxt.anchorMin = new Vector2(0, 1); rtTxt.anchorMax = new Vector2(0, 1); rtTxt.pivot = new Vector2(0, 1);
            rtTxt.sizeDelta = new Vector2(Mathf.Max(80, size.x - 8), 16);
            rtTxt.anchoredPosition = new Vector2(8, -8 - iconSize.y - 4);
        }

        private string ResolveIconPreferredPath(string idOrPath)
        {
            if (string.IsNullOrEmpty(idOrPath)) return idOrPath;
            // 路径类：
            bool isPathLike = idOrPath.Contains("/") || idOrPath.Contains("\\") || idOrPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || idOrPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || idOrPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
            if (isPathLike)
            {
                var token = idOrPath.Trim();
                // 绝对路径 -> 转 icons 相对（以 IconCache 可识别的 "icons/" 前缀）
                try
                {
                    if (System.IO.Path.IsPathRooted(token))
                    {
                        var rel = ToIconsRelative(token);
                        if (!string.IsNullOrEmpty(rel)) return rel.Replace('\\', '/');
                        return token.Replace('\\', '/');
                    }
                }
                catch { }
                // 相对路径但未带 icons/ 前缀 -> 归一化为 icons/xxx
                if (!token.StartsWith("icons/", StringComparison.OrdinalIgnoreCase))
                {
                    return ("icons/" + token.Replace('\\', '/'));
                }
                return token.Replace('\\', '/');
            }
            // 非路径：视为物品ID，交给 _byId 命中（优先 AssetBundle sprites.name，再退 ingredients/<id>.png）
            return idOrPath;
        }

        private string GetDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            try
            {
                // 若是路径，先取文件名（去扩展），避免把路径当作 id
                string idNorm = id;
                if (idNorm.Contains("/") || idNorm.Contains("\\"))
                {
                    try { idNorm = System.IO.Path.GetFileNameWithoutExtension(idNorm); } catch { }
                }
                idNorm = NormalizeId(idNorm);
                

                // 优先：名称缓存命中
                try
                {
                    if (JustEnoughItems.Plugin.NameCache != null && JustEnoughItems.Plugin.NameCache.TryGetValue(idNorm, out var cached) && !string.IsNullOrEmpty(cached))
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
                    if (dict != null && dict.TryGetValue(idNorm, out var name) && !string.IsNullOrEmpty(name))
                    {
                        try { JustEnoughItems.Plugin.NameCache[idNorm] = name; } catch { }
                        return name;
                    }
                }
                catch { }

                // 已移除对 items.json 的回退
            }
            catch { }
            return id;
        }

        private static string NormalizeId(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Trim();
            if (t.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)) t = t.Substring("TechType.".Length);
            return t.Trim();
        }

        private static Type AccessType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName, false); if (t != null) return t; } catch { }
            }
            return null;
        }

        // 根据当前物品从工作台覆盖清单中解析显示名，优先使用覆盖项 DisplayName
        private string ResolveFabricatorDisplayNameForItem(string currentItemId, string fabricatorId, string fallbackDisplay)
        {
            try
            {
                // 优先匹配 jei-fabricators.json
                var list = JustEnoughItems.Config.FabricatorOverridesService.Current;
                if (list != null && list.Count > 0 && !string.IsNullOrEmpty(currentItemId))
                {
                    string itemNorm = NormalizeId(currentItemId);
                    foreach (var fo in list)
                    {
                        if (fo == null || fo.IncludeItems == null) continue;
                        foreach (var it in fo.IncludeItems)
                        {
                            if (string.IsNullOrEmpty(it)) continue;
                            if (string.Equals(NormalizeId(it), itemNorm, StringComparison.Ordinal))
                            {
                                if (!string.IsNullOrEmpty(fo.DisplayName)) return fo.DisplayName;
                                // 若覆盖未提供显示名，优先使用外部传入（通常来自 JSON）的工作台名
                                if (!string.IsNullOrEmpty(fallbackDisplay)) return fallbackDisplay;
                                // 最后回退到游戏显示名
                                return GetDisplayName(string.IsNullOrEmpty(fo.Id) ? fabricatorId : fo.Id);
                            }
                        }
                    }
                }

                // 若未通过 IncludeItems 命中，尝试按工作台 Id 匹配覆盖项
                if (list != null && list.Count > 0)
                {
                    string fabIdNorm = NormalizeId(fabricatorId);
                    foreach (var fo in list)
                    {
                        if (fo == null) continue;
                        if (!string.IsNullOrEmpty(fo.Id) && string.Equals(NormalizeId(fo.Id), fabIdNorm, StringComparison.Ordinal))
                        {
                            if (!string.IsNullOrEmpty(fo.DisplayName)) return fo.DisplayName;
                            break;
                        }
                    }
                }
            }
            catch { }

            // 次选：使用传入的 fabricatorDisplayName（来自数据管线/JSON）
            if (!string.IsNullOrEmpty(fallbackDisplay)) return fallbackDisplay;
            // 兜底：从游戏语言表解析 fabricatorId
            return GetDisplayName(fabricatorId);
        }


        private void OpenById(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            try { JeiManager.ShowForItem(id); } catch { }
        }

        private void ClearContent()
        {
            if (contentRoot == null) return;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
            _contentAccumulatedHeight = 0f;
            try { contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, 0f); } catch { }
        }

        private void ShowEmpty(string text)
        {
            var go = new GameObject("EmptyText");
            go.transform.SetParent(contentRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            float h = 48f;
            rt.sizeDelta = new Vector2(0, h);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            t.color = Color.white;
            PositionNext(rt, h);
        }

        private void CreateHeader(string text)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(contentRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            float h = 32f;
            rt.sizeDelta = new Vector2(0, h);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            t.color = new Color(1f, 1f, 1f, 0.95f);
            t.fontSize = 20;
            PositionNext(rt, h);
        }

        private void CreateRow(string label, string value, Sprite icon)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(contentRoot, false);
            var rt = row.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            float h = 28f;
            rt.sizeDelta = new Vector2(0, h);

            // Label
            var lGo = new GameObject("Label");
            lGo.transform.SetParent(row.transform, false);
            var lRt = lGo.AddComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0, 0);
            lRt.anchorMax = new Vector2(0.3f, 1);
            lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
            var lText = lGo.AddComponent<Text>();
            lText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lText.alignment = TextAnchor.MiddleLeft;
            lText.text = label;
            lText.color = new Color(1f, 1f, 1f, 0.85f);

            // Value
            var vGo = new GameObject("Value");
            vGo.transform.SetParent(row.transform, false);
            var vRt = vGo.AddComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0.3f, 0);
            vRt.anchorMax = new Vector2(1, 1);
            vRt.offsetMin = Vector2.zero; vRt.offsetMax = Vector2.zero;
            var vText = vGo.AddComponent<Text>();
            vText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            vText.alignment = TextAnchor.MiddleLeft;
            vText.text = value;
            vText.color = Color.white;
            PositionNext(rt, h);
        }

        private void CreateParagraph(string text)
        {
            var go = new GameObject("Paragraph");
            go.transform.SetParent(contentRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            float h = 24f;
            rt.sizeDelta = new Vector2(0, h);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            PositionNext(rt, h);
        }

        private void PositionNext(RectTransform rt, float height)
        {
            try
            {
                rt.anchoredPosition = new Vector2(0f, -_contentAccumulatedHeight);
                _contentAccumulatedHeight += height + 6f; // 简单间距
                if (contentRoot != null)
                {
                    var cur = contentRoot.sizeDelta;
                    if (_contentAccumulatedHeight > cur.y)
                        contentRoot.sizeDelta = new Vector2(cur.x, _contentAccumulatedHeight);
                }
            }
            catch { }
        }

        private void Update()
        {
            // 兜底：页面内直接监听关闭按键，避免外部热键异常时无法关闭
            try
            {
                // 首帧抑制：打开当帧与下一帧忽略关闭按键，避免同帧开关造成闪烁
                try { if (_shownFrame >= 0 && Time.frameCount <= _shownFrame + 1) return; } catch { }
                bool viaConfig = false;
                try { viaConfig = Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value); } catch { }
                bool viaJ = Input.GetKeyDown(KeyCode.J);
                if (viaConfig || viaJ)
                {
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI: Page Update close via key (DetailPage), viaConfig={viaConfig}, viaJ={viaJ}"); } catch { }
                    JeiManager.Hide();
                }
            }
            catch { }
        }
    }
}
