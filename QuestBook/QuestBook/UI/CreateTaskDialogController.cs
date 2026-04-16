using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using QuestBook;
using QuestBook.Models;

namespace QuestBook.UI
{
    internal class CreateTaskDialogController : MonoBehaviour
    {
        private Button _confirmBtn;
        private Button _cancelBtn;
        private RectTransform _panelRt;
        private RectTransform _formContentRt;
        private ScrollRect _formScrollRect;

        private Toggle _unlockToggle;
        private Transform _rowPrereq;
        private Transform _rowVisibleBefore;
        private Transform _rowUnlockCond;
        private Dropdown _unlockCondDropdown;

        private Button _chooseIconBtn;
        private Image _iconPreviewImg;

        private Dropdown _completionTypeDropdown;
        private Dropdown _rewardModeDropdown;

        private Transform _rowRewardItems;
        private Transform _rowRewardPool;
        private Dropdown _rewardPoolDropdown;

        private Transform _itemsContent;
        private Button _addItemButton;
        private GameObject _pfRewardItemRow;

        private Transform _prereqItemsContent;
        private Dictionary<string, Toggle> _prereqToggles = new Dictionary<string, Toggle>(StringComparer.OrdinalIgnoreCase);
        private ScrollRect _prereqListScrollRect;
        private GameObject _pfPrereqItemRow;
        private Scrollbar _prereqVScrollbar;

        

        private string _selectedIconPath;
        private string _chapterId;
        private bool _isEditMode;
        private string _editingNodeId;

        internal void Initialize(string chapterId)
        {
            _chapterId = chapterId;
            TryBindRoots();
            BindFooterButtons();
            BindUnlockGroup();
            BindIconPickers();
            BindCompletionType();
            BindRewardModeAndRows();
            // 打开页面时刷新一次前置任务列表
            BindPrereqMultiSelect();
            EnsureDefaultRewardRow();
        }

        private void BindFooterButtons()
        {
            var tConfirm = transform.Find("Panel/Footer/ConfirmButton");
            if (tConfirm != null)
            {
                _confirmBtn = tConfirm.GetComponent<Button>();
                if (_confirmBtn == null) _confirmBtn = tConfirm.gameObject.AddComponent<Button>();
                _confirmBtn.onClick.RemoveAllListeners();
                _confirmBtn.onClick.AddListener(OnConfirmClicked);
            }
            var tCancel = transform.Find("Panel/Footer/CancelButton");
            if (tCancel != null)
            {
                _cancelBtn = tCancel.GetComponent<Button>();
                if (_cancelBtn == null) _cancelBtn = tCancel.gameObject.AddComponent<Button>();
                _cancelBtn.onClick.RemoveAllListeners();
                _cancelBtn.onClick.AddListener(OnCancelClicked);
            }
        }

        private void TryBindRoots()
        {
            var tPanel = transform.Find("Panel");
            _panelRt = tPanel as RectTransform;
            var tForm = transform.Find("Panel/FormScroll/FormViewport/FormContent");
            _formContentRt = tForm as RectTransform;
            var tFormScroll = transform.Find("Panel/FormScroll");
            _formScrollRect = tFormScroll != null ? tFormScroll.GetComponent<ScrollRect>() : null;
        }

        private void BindUnlockGroup()
        {
            _rowPrereq = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_PrereqMultiSelect");
            _rowVisibleBefore = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_VisibleBeforeUnlock");
            _rowUnlockCond = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_UnlockCondition");

            var tToggle = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_UnlockToggle/Control/Toggle");
            if (tToggle != null)
            {
                _unlockToggle = tToggle.GetComponent<Toggle>();
                if (_unlockToggle != null)
                {
                    _unlockToggle.onValueChanged.RemoveAllListeners();
                    _unlockToggle.onValueChanged.AddListener(OnUnlockToggleChanged);
                }
            }
            var tDrop = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_UnlockCondition/Control/Dropdown");
            if (tDrop != null)
            {
                _unlockCondDropdown = tDrop.GetComponent<Dropdown>();
                if (_unlockCondDropdown != null)
                {
                    _unlockCondDropdown.options.Clear();
                    _unlockCondDropdown.options.Add(new Dropdown.OptionData("完成任意一项前置任务"));
                    _unlockCondDropdown.options.Add(new Dropdown.OptionData("完成全部前置任务"));
                    _unlockCondDropdown.value = 0;
                    _unlockCondDropdown.RefreshShownValue();
                }
            }
            ApplyUnlockVisibility();
        }

        private void OnUnlockToggleChanged(bool isOn)
        {
            ApplyUnlockVisibility();
            if (!isOn)
            {
                // 非直接解锁时，若尚未构建列表则构建
                if (_prereqToggles == null || _prereqToggles.Count == 0)
                {
                    BindPrereqMultiSelect();
                }
            }
        }

        private void ApplyUnlockVisibility()
        {
            bool direct = _unlockToggle != null ? _unlockToggle.isOn : true;
            bool show = !direct;
            if (_rowPrereq != null) _rowPrereq.gameObject.SetActive(show);
            if (_rowVisibleBefore != null) _rowVisibleBefore.gameObject.SetActive(show);
            if (_rowUnlockCond != null) _rowUnlockCond.gameObject.SetActive(show);
            if (_formContentRt != null) LayoutRebuilder.MarkLayoutForRebuild(_formContentRt);
        }

        private void BindIconPickers()
        {
            var tChoose = FindDeep(transform, "ChooseIconButton");
            if (tChoose != null)
            {
                _chooseIconBtn = tChoose.GetComponent<Button>();
                if (_chooseIconBtn == null) _chooseIconBtn = tChoose.gameObject.AddComponent<Button>();
                _chooseIconBtn.onClick.RemoveAllListeners();
                _chooseIconBtn.onClick.AddListener(OnChooseIconClicked);
            }
            var tPrev = FindDeep(transform, "IconPreview");
            if (tPrev != null)
            {
                _iconPreviewImg = tPrev.GetComponent<Image>();
                if (_iconPreviewImg == null) _iconPreviewImg = tPrev.gameObject.AddComponent<Image>();
                _iconPreviewImg.preserveAspect = true;
            }
        }

        private void OnChooseIconClicked()
        {
            try
            {
                bool opened = UIManager.OpenIconPicker((path, sp) =>
                {
                    if (!string.IsNullOrEmpty(path) && sp != null)
                    {
                        _selectedIconPath = path;
                        if (_iconPreviewImg != null) _iconPreviewImg.sprite = sp;
                    }
                });
                if (opened) return;
            }
            catch { }
            try
            {
                string dir = UIManager.GetIconsDirectory();
                string p;
                bool ok = UIManager.TryPickFileWithDialog(dir, "PNG 文件|*.png|所有文件|*.*", "选择任务图标", out p);
                if (!ok || string.IsNullOrEmpty(p)) return;
                var sp = UIManager.LoadSpriteFromFile(p);
                if (sp != null)
                {
                    _selectedIconPath = p;
                    if (_iconPreviewImg != null) _iconPreviewImg.sprite = sp;
                }
            }
            catch { }
        }

        private void BindCompletionType()
        {
            var t = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_CompletionType/Control/Dropdown");
            if (t != null)
            {
                _completionTypeDropdown = t.GetComponent<Dropdown>();
                if (_completionTypeDropdown != null)
                {
                    _completionTypeDropdown.options.Clear();
                    _completionTypeDropdown.options.Add(new Dropdown.OptionData("手动完成"));
                    _completionTypeDropdown.options.Add(new Dropdown.OptionData("脚本触发"));
                    _completionTypeDropdown.options.Add(new Dropdown.OptionData("条件驱动"));
                    _completionTypeDropdown.value = 0;
                    _completionTypeDropdown.RefreshShownValue();
                }
            }
        }

        private void BindRewardModeAndRows()
        {
            _rowRewardItems = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardItems");
            _rowRewardPool = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardPoolPicker");
            var tPool = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardPoolPicker/Control/Dropdown");
            if (tPool != null)
            {
                _rewardPoolDropdown = tPool.GetComponent<Dropdown>();
            }

            var t = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardMode/Control/Dropdown");
            if (t != null)
            {
                _rewardModeDropdown = t.GetComponent<Dropdown>();
                if (_rewardModeDropdown != null)
                {
                    if (_rewardModeDropdown.options.Count == 0)
                    {
                        _rewardModeDropdown.options.Add(new Dropdown.OptionData("物品清单"));
                        _rewardModeDropdown.options.Add(new Dropdown.OptionData("奖励池"));
                        _rewardModeDropdown.value = 0;
                    }
                    _rewardModeDropdown.onValueChanged.RemoveAllListeners();
                    _rewardModeDropdown.onValueChanged.AddListener(OnRewardModeChanged);
                    OnRewardModeChanged(_rewardModeDropdown.value);
                }
            }

            var tItemsContent = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardItems/Control/ItemsScroll/Viewport/ItemsContent");
            _itemsContent = tItemsContent;
            var tAddBtn = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_RewardItems/Control/AddItemBar/AddItemButton");
            if (tAddBtn != null)
            {
                _addItemButton = tAddBtn.GetComponent<Button>();
                if (_addItemButton == null) _addItemButton = tAddBtn.gameObject.AddComponent<Button>();
                _addItemButton.onClick.RemoveAllListeners();
                _addItemButton.onClick.AddListener(() => AddRewardRow(null, 1));
            }

            _pfRewardItemRow = UIManager.LoadFromBundle("RewardItemRow");
        }

        private void BindPrereqMultiSelect()
        {
            var listRoot = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_PrereqMultiSelect/Control/ListScroll");
            _prereqListScrollRect = listRoot != null ? listRoot.GetComponent<ScrollRect>() : null;
            var tItemsContent = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_PrereqMultiSelect/Control/ListScroll/ListViewport/ListContent");
            if (tItemsContent == null)
            {
                // 回退：先定位 Row，再在其下深度查找 ListContent
                var row = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_PrereqMultiSelect");
                if (row == null) row = FindDeep(transform, "Row_PrereqMultiSelect");
                if (row != null)
                {
                    var alt = FindDeep(row, "ListContent");
                    if (alt != null) tItemsContent = alt;
                    if (_prereqListScrollRect == null)
                    {
                        var altRoot = FindDeep(row, "ListScroll");
                        if (altRoot != null) _prereqListScrollRect = altRoot.GetComponent<ScrollRect>();
                    }
                }
            }
            _prereqItemsContent = tItemsContent;
            // 绑定垂直滚动条（若存在）
            _prereqVScrollbar = null;
            try
            {
                var sbTr = SafeFind("Panel/FormScroll/FormViewport/FormContent/Row_PrereqMultiSelect/Control/ListScroll/ScrollbarVertical");
                if (sbTr == null && listRoot != null)
                {
                    sbTr = listRoot.GetComponentInChildren<Scrollbar>(true) != null ? listRoot.GetComponentInChildren<Scrollbar>(true).transform : null;
                }
                if (sbTr != null) _prereqVScrollbar = sbTr.GetComponent<Scrollbar>();
                if (_prereqListScrollRect != null && _prereqVScrollbar != null)
                {
                    _prereqListScrollRect.verticalScrollbar = _prereqVScrollbar;
                    _prereqListScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
            }
            catch { }
            _pfPrereqItemRow = UIManager.LoadFromBundle("PrereqItemRow");
            if (_prereqToggles == null || _prereqToggles.Count == 0) BuildPrereqList(null);
        }

        private void BuildPrereqList(List<string> preselected)
        {
            if (_prereqItemsContent == null) return;
            var listScroll = _prereqListScrollRect;
            // 确保 ListContent 拥有基础布局组件
            var contentRt = _prereqItemsContent as RectTransform;
            var vlg = _prereqItemsContent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = _prereqItemsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6f;
            var csf = _prereqItemsContent.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = _prereqItemsContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (listScroll != null && listScroll.content == null && contentRt != null)
            {
                listScroll.content = contentRt;
            }
            // 清空
            for (int i = _prereqItemsContent.childCount - 1; i >= 0; i--)
            {
                var ch = _prereqItemsContent.GetChild(i);
                if (ch != null) UnityEngine.Object.Destroy(ch.gameObject);
            }
            _prereqToggles.Clear();

            var pairs = UIManager.GetChapterNodeList(_chapterId);
            // 编辑模式下排除自身
            string excludeId = _isEditMode ? _editingNodeId : null;
            for (int i = 0; i < pairs.Count; i++)
            {
                var id = pairs[i].Key;
                var title = pairs[i].Value;
                if (!string.IsNullOrEmpty(excludeId) && string.Equals(id, excludeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject row = null;
                if (_pfPrereqItemRow != null)
                {
                    row = UnityEngine.Object.Instantiate(_pfPrereqItemRow, _prereqItemsContent);
                    row.name = "PrereqItem_" + id;
                }
                else
                {
                    row = new GameObject("PrereqItem_" + id, typeof(RectTransform));
                    row.transform.SetParent(_prereqItemsContent, false);
                }
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 32f);
                var le = row.GetComponent<LayoutElement>();
                if (le == null) le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 32f;
                le.minHeight = 28f;
                le.flexibleWidth = 1f;

                var tg = row.GetComponentInChildren<Toggle>(true);
                Text text = row.GetComponentInChildren<Text>(true);
                if (tg == null)
                {
                    var toggleGo = new GameObject("Toggle", typeof(RectTransform));
                    toggleGo.transform.SetParent(row.transform, false);
                    var tRt = toggleGo.GetComponent<RectTransform>();
                    tRt.anchorMin = new Vector2(0f, 0.5f);
                    tRt.anchorMax = new Vector2(0f, 0.5f);
                    tRt.pivot = new Vector2(0f, 0.5f);
                    tRt.anchoredPosition = new Vector2(12f, 0f);
                    tRt.sizeDelta = new Vector2(20f, 20f);
                    tg = toggleGo.AddComponent<Toggle>();
                    var bg = toggleGo.AddComponent<Image>();
                    bg.color = new Color(1f, 1f, 1f, 0.1f);
                    tg.targetGraphic = bg;
                    var markGo = new GameObject("Checkmark", typeof(RectTransform));
                    markGo.transform.SetParent(toggleGo.transform, false);
                    var mkImg = markGo.AddComponent<Image>();
                    mkImg.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
                    tg.graphic = mkImg;
                }
                if (text == null)
                {
                    var labelGo = new GameObject("Label", typeof(RectTransform));
                    labelGo.transform.SetParent(row.transform, false);
                    var lRt = labelGo.GetComponent<RectTransform>();
                    lRt.anchorMin = new Vector2(0f, 0.5f);
                    lRt.anchorMax = new Vector2(1f, 0.5f);
                    lRt.pivot = new Vector2(0f, 0.5f);
                    lRt.anchoredPosition = new Vector2(40f, 0f);
                    lRt.sizeDelta = new Vector2(-40f, 20f);
                    text = labelGo.AddComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.color = Color.white;
                    text.alignment = TextAnchor.MiddleLeft;
                }
                text.text = string.IsNullOrEmpty(title) ? id : title;

                bool sel = preselected != null && preselected.Contains(id);
                tg.isOn = sel;

                _prereqToggles[id] = tg;
            }
            var listRt = _prereqItemsContent as RectTransform;
            if (listRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);
            if (_formContentRt != null) LayoutRebuilder.MarkLayoutForRebuild(_formContentRt);

            // 动态决定是否启用内层滚动，以避免与外层滚动冲突
            if (_prereqListScrollRect != null)
            {
                var viewport = _prereqListScrollRect.viewport != null ? _prereqListScrollRect.viewport : _prereqListScrollRect.transform as RectTransform;
                float contentH = 0f;
                if (listRt != null)
                {
                    try { contentH = LayoutUtility.GetPreferredHeight(listRt); } catch { contentH = listRt.rect.height; }
                }
                float viewportH = viewport != null ? viewport.rect.height : 0f;
                bool needInnerScroll = contentH > (viewportH + 1f);
                _prereqListScrollRect.enabled = needInnerScroll;

                // 当不需要内层滚动时，关闭 Viewport 上的 Image.raycastTarget 以便事件传递到外层
                var vpImg = viewport != null ? viewport.GetComponent<Image>() : null;
                if (vpImg != null) vpImg.raycastTarget = needInnerScroll;
                var bgImg = _prereqListScrollRect.GetComponent<Image>();
                if (bgImg != null) bgImg.raycastTarget = needInnerScroll;
                if (_prereqVScrollbar != null)
                {
                    _prereqVScrollbar.gameObject.SetActive(needInnerScroll);
                }
            }
        }

        private void ApplyPrereqSelection(List<string> prereqIds)
        {
            if (_prereqToggles == null || _prereqToggles.Count == 0 || prereqIds == null) return;
            var set = new HashSet<string>(prereqIds, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _prereqToggles)
            {
                var tg = kv.Value;
                if (tg != null) tg.isOn = set.Contains(kv.Key);
            }
        }

        private List<string> CollectPrereqSelections()
        {
            var list = new List<string>();
            if (_prereqToggles != null)
            {
                foreach (var kv in _prereqToggles)
                {
                    var id = kv.Key;
                    var tg = kv.Value;
                    if (tg != null && tg.isOn) list.Add(id);
                }
            }
            return list;
        }

        private void OnRewardModeChanged(int value)
        {
            bool items = (value == 0);
            if (_formContentRt != null) LayoutRebuilder.MarkLayoutForRebuild(_formContentRt);
        }

        private void ScheduleRestore(ScrollRect sr, ref Coroutine handle, float pos, int frames)
        {
            if (sr == null) return;
            try
            {
                sr.StopMovement();
                sr.velocity = Vector2.zero;
                sr.verticalNormalizedPosition = Mathf.Clamp01(pos);
                Canvas.ForceUpdateCanvases();
            }
            catch { }
            if (handle != null) StopCoroutine(handle);
            handle = StartCoroutine(RestoreScrollCo(sr, pos, frames));
        }

        private IEnumerator RestoreScrollCo(ScrollRect sr, float pos, int frames)
        {
            sr.StopMovement();
            sr.velocity = Vector2.zero;
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                if (sr != null) sr.verticalNormalizedPosition = Mathf.Clamp01(pos);
            }
            if (sr != null) Canvas.ForceUpdateCanvases();
        }

        private void EnsureDefaultRewardRow()
        {
            if (_rowRewardItems == null || !_rowRewardItems.gameObject.activeInHierarchy) return;
            if (_itemsContent != null && _itemsContent.childCount == 0)
            {
                AddRewardRow(null, 1);
            }
        }

        private void AddRewardRow(string itemId, int count)
        {
            if (_rowRewardItems == null || !_rowRewardItems.gameObject.activeInHierarchy) return;
            if (_itemsContent == null) return;
            GameObject rowGo = null;
            if (_pfRewardItemRow != null)
            {
                rowGo = UnityEngine.Object.Instantiate(_pfRewardItemRow, _itemsContent);
            }
            else
            {
                rowGo = new GameObject("RewardItemRow");
                rowGo.transform.SetParent(_itemsContent, false);
            }
            rowGo.name = "RewardItemRow";

            var idInput = FindOrCreateInput(rowGo.transform, "ItemIdInput", out var _);
            var cntInput = FindOrCreateInput(rowGo.transform, "CountInput", out var _);
            var remBtnTr = rowGo.transform.Find("RemoveButton");
            Button remBtn = remBtnTr != null ? remBtnTr.GetComponent<Button>() : null;
            if (remBtn == null && remBtnTr != null) remBtn = remBtnTr.gameObject.AddComponent<Button>();
            if (remBtn != null)
            {
                remBtn.onClick.RemoveAllListeners();
                remBtn.onClick.AddListener(() => {
                    if (rowGo != null) UnityEngine.Object.Destroy(rowGo);
                    if (_formContentRt != null) LayoutRebuilder.MarkLayoutForRebuild(_formContentRt);
                });
            }

            if (idInput != null) idInput.text = itemId ?? string.Empty;
            if (cntInput != null) cntInput.text = count.ToString();

            if (_formContentRt != null) LayoutRebuilder.MarkLayoutForRebuild(_formContentRt);
        }

        private void OnCancelClicked()
        {
            try
            {
                bool opened = UIManager.OpenConfirmExitDialog("确认退出新建任务？未保存的内容将丢失。", () =>
                {
                    UIManager.CloseCreateTaskDialog();
                }, () => { });
                if (!opened)
                {
                    UIManager.CloseCreateTaskDialog();
                }
            }
            catch
            {
                UIManager.CloseCreateTaskDialog();
            }
        }

        private void OnConfirmClicked()
        {
            try
            {
                string title = GetInputText("Panel/FormScroll/FormViewport/FormContent/Row_TaskName");
                string desc = GetInputText("Panel/FormScroll/FormViewport/FormContent/Row_TaskDesc");
                bool directUnlock = _unlockToggle != null ? _unlockToggle.isOn : true;
                bool visibleBeforeUnlock = GetToggleValue("Panel/FormScroll/FormViewport/FormContent/Row_VisibleBeforeUnlock");
                int unlockCondIndex = _unlockCondDropdown != null ? _unlockCondDropdown.value : 0;
                int completionTypeIndex = _completionTypeDropdown != null ? _completionTypeDropdown.value : 0;
                int rewardModeIndex = _rewardModeDropdown != null ? _rewardModeDropdown.value : 0;
                int rewardPoolIndex = _rewardPoolDropdown != null ? _rewardPoolDropdown.value : 0;

                var items = CollectRewardItems();
                List<string> prereqIds = null;
                if (!directUnlock)
                {
                    prereqIds = CollectPrereqSelections();
                }
                if (_isEditMode)
                {
                    bool ok = UIManager.UpdateTask(
                        _chapterId,
                        _editingNodeId,
                        title,
                        desc,
                        directUnlock,
                        visibleBeforeUnlock,
                        unlockCondIndex,
                        _selectedIconPath,
                        completionTypeIndex,
                        rewardModeIndex,
                        prereqIds,
                        items,
                        rewardPoolIndex);
                    if (!ok) Mod.Log?.LogWarning("UpdateTask failed.");
                }
                else
                {
                    string finalId;
                    bool ok = UIManager.TryCreateTask(
                        _chapterId,
                        title,
                        desc,
                        directUnlock,
                        visibleBeforeUnlock,
                        unlockCondIndex,
                        _selectedIconPath,
                        completionTypeIndex,
                        rewardModeIndex,
                        prereqIds,
                        items,
                        rewardPoolIndex,
                        out finalId);
                    if (!ok)
                    {
                        Mod.Log?.LogWarning("TryCreateTask failed: invalid chapter or data.");
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"CreateTask confirm error: {e.Message}");
            }
            UIManager.CloseCreateTaskDialog();
        }

        internal void EnterEditMode(
            string nodeId,
            string title,
            string desc,
            bool directUnlock,
            bool visibleBeforeUnlock,
            int unlockCondIndex,
            string iconPathAbs,
            Sprite iconSprite,
            int completionTypeIndex,
            int rewardModeIndex,
            List<UserRewardItem> rewardItems,
            int rewardPoolIndex,
            List<string> prereqIds)
        {
            _isEditMode = true;
            _editingNodeId = nodeId;
            TryBindRoots();
            BindFooterButtons();
            BindUnlockGroup();
            BindIconPickers();
            BindCompletionType();
            BindRewardModeAndRows();
            BindPrereqMultiSelect();

            SetInputText("Panel/FormScroll/FormViewport/FormContent/Row_TaskName", title ?? string.Empty);
            SetInputText("Panel/FormScroll/FormViewport/FormContent/Row_TaskDesc", desc ?? string.Empty);

            if (_unlockToggle != null) _unlockToggle.isOn = directUnlock;
            ApplyUnlockVisibility();
            var tgVisible = transform.Find("Panel/FormScroll/FormViewport/FormContent/Row_VisibleBeforeUnlock");
            if (tgVisible != null)
            {
                var tcomp = tgVisible.GetComponentInChildren<Toggle>(true);
                if (tcomp != null) tcomp.isOn = visibleBeforeUnlock;
            }
            if (_unlockCondDropdown != null)
            {
                _unlockCondDropdown.value = Mathf.Clamp(unlockCondIndex, 0, Mathf.Max(0, _unlockCondDropdown.options.Count - 1));
                _unlockCondDropdown.RefreshShownValue();
            }

            _selectedIconPath = iconPathAbs;
            if (_iconPreviewImg != null && iconSprite != null) _iconPreviewImg.sprite = iconSprite;

            if (_completionTypeDropdown != null)
            {
                _completionTypeDropdown.value = Mathf.Clamp(completionTypeIndex, 0, Mathf.Max(0, _completionTypeDropdown.options.Count - 1));
                _completionTypeDropdown.RefreshShownValue();
            }

            if (_rewardModeDropdown != null)
            {
                _rewardModeDropdown.value = Mathf.Clamp(rewardModeIndex, 0, Mathf.Max(0, _rewardModeDropdown.options.Count - 1));
                _rewardModeDropdown.RefreshShownValue();
                OnRewardModeChanged(_rewardModeDropdown.value);
            }

            if (rewardModeIndex == 0)
            {
                // 填充物品奖励
                if (_itemsContent != null)
                {
                    for (int i = _itemsContent.childCount - 1; i >= 0; i--)
                    {
                        var ch = _itemsContent.GetChild(i);
                        if (ch != null && ch.name.StartsWith("RewardItemRow", StringComparison.OrdinalIgnoreCase))
                            UnityEngine.Object.Destroy(ch.gameObject);
                    }
                    if (rewardItems != null && rewardItems.Count > 0)
                    {
                        foreach (var it in rewardItems)
                        {
                            if (it == null) continue;
                            AddRewardRow(it.ItemId, Math.Max(1, it.Count));
                        }
                    }
                    else
                    {
                        EnsureDefaultRewardRow();
                    }
                }
            }
            else
            {
                if (_rewardPoolDropdown != null)
                {
                    _rewardPoolDropdown.value = Mathf.Clamp(rewardPoolIndex, 0, Mathf.Max(0, _rewardPoolDropdown.options.Count - 1));
                    _rewardPoolDropdown.RefreshShownValue();
                }
            }

            // 预填前置多选：仅应用选择；若列表尚未构建则构建一次
            if (!directUnlock)
            {
                if (_prereqToggles == null || _prereqToggles.Count == 0)
                {
                    BindPrereqMultiSelect();
                }
                ApplyPrereqSelection(prereqIds);
            }

            // 可选：调整标题/确认按钮文本
            var titleTr = transform.Find("Panel/Header/Title");
            var titleText = titleTr != null ? titleTr.GetComponent<UnityEngine.UI.Text>() : null;
            if (titleText != null) titleText.text = "修改任务";
            var confirmTr = transform.Find("Panel/Footer/ConfirmButton/Text");
            var confirmText = confirmTr != null ? confirmTr.GetComponent<UnityEngine.UI.Text>() : null;
            if (confirmText != null) confirmText.text = "保存";
        }

        private List<UserRewardItem> CollectRewardItems()
        {
            var list = new List<UserRewardItem>();
            if (_itemsContent == null) return list;
            for (int i = 0; i < _itemsContent.childCount; i++)
            {
                var row = _itemsContent.GetChild(i);
                if (row == null) continue;
                if (!row.name.StartsWith("RewardItemRow", StringComparison.OrdinalIgnoreCase)) continue;
                var idInput = FindInputIn(row, "ItemIdInput");
                var cntInput = FindInputIn(row, "CountInput");
                string id = idInput != null ? idInput.text?.Trim() : null;
                if (string.IsNullOrEmpty(id)) continue;
                int count = 1;
                if (cntInput != null)
                {
                    int.TryParse(cntInput.text, out count);
                    if (count <= 0) count = 1;
                }
                list.Add(new UserRewardItem { ItemId = id, Count = count });
            }
            return list;
        }

        private InputField FindOrCreateInput(Transform parent, string name, out Text textComp)
        {
            textComp = null;
            var tr = parent.Find(name);
            if (tr == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                tr = go.transform;
            }
            var input = tr.GetComponent<InputField>();
            if (input == null) input = tr.gameObject.AddComponent<InputField>();
            var txt = tr.GetComponentInChildren<Text>();
            if (txt == null)
            {
                var tgo = new GameObject("Text");
                tgo.transform.SetParent(tr, false);
                txt = tgo.AddComponent<Text>();
            }
            input.textComponent = txt;
            textComp = txt;
            return input;
        }

        private InputField FindInputIn(Transform root, string name)
        {
            if (root == null) return null;
            var t = root.Find(name);
            if (t != null)
            {
                var inp = t.GetComponent<InputField>();
                if (inp != null) return inp;
            }
            return root.GetComponentInChildren<InputField>(true);
        }

        private string GetInputText(string rowPath)
        {
            var row = SafeFind(rowPath);
            if (row == null) return null;
            var inp = row.GetComponentInChildren<InputField>(true);
            return inp != null ? inp.text : null;
        }

        private void SetInputText(string rowPath, string value)
        {
            var row = SafeFind(rowPath);
            if (row == null) return;
            var inp = row.GetComponentInChildren<InputField>(true);
            if (inp != null) inp.text = value ?? string.Empty;
        }

        private bool GetToggleValue(string rowPath)
        {
            var row = SafeFind(rowPath);
            if (row == null) return false;
            var tg = row.GetComponentInChildren<Toggle>(true);
            return tg != null ? tg.isOn : false;
        }

        private Transform SafeFind(string path)
        {
            return transform.Find(path);
        }

        private Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
