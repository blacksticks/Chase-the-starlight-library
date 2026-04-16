using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using QuestBook;

namespace QuestBook.UI
{
    internal class CreateChapterDialogController : MonoBehaviour
    {
        private Button _closeBtn;
        private Button _cancelBtn;
        private Button _confirmBtn;
        private Button _backdropBtn;
        private Transform _backdropTr;
        private RectTransform _panelRt;
        private RectTransform _formContentRt;
        private Button _chooseIconBtn;
        private Button _chooseBgBtn;
        private Transform _rowToggleTr;
        private Toggle _rowToggle;
        private Transform _advancedSectionTr;
        private Transform _rowPrereqRuntime;
        private bool? _lastToggleState;
        private Image _iconPreviewImg;
        private Sprite _runtimeIconSprite;
        private string _selectedIconPath;
        private Image _bgPreviewImg;
        private Sprite _runtimeBgSprite;
        private string _selectedBgPath;
        private bool _isEditMode;
        private string _editingChapterId;
        private int _editingOrder;
        

        private void Awake()
        {
            TryBind();
            EnsureVisibility();
            EnsureDefaultIconPreview();
        }

        private void OnEnable()
        {
            TryBind();
            EnsureVisibility();
            EnsureDefaultIconPreview();
        }

        private void TryBind()
        {
            if (_backdropTr == null)
            {
                _backdropTr = transform.Find("Backdrop");
                if (_backdropTr != null) _backdropTr.gameObject.SetActive(true);
            }
            if (_panelRt == null)
            {
                var t = transform.Find("Panel");
                _panelRt = t as RectTransform;
                if (_panelRt != null) _panelRt.gameObject.SetActive(true);
            }
            if (_formContentRt == null)
            {
                var t = transform.Find("Panel/FormScroll/FormViewport/FormContent");
                _formContentRt = t as RectTransform;
                if (_formContentRt != null)
                {
                    // 兼容：若在预制体中已预放置 Row_PrereqMultiSelect，则作为运行时对象复用
                    var pre = _formContentRt.Find("Row_PrereqMultiSelect");
                    if (pre != null)
                    {
                        _rowPrereqRuntime = pre;
                    }
                }
                else
                {
                    var t2 = FindDeepCI(transform, "FormContent");
                    _formContentRt = t2 as RectTransform;
                    if (_formContentRt == null)
                    {
                        Mod.Log?.LogWarning("FormContent not found via exact or CI deep search.");
                    }
                }
            }
            
            if (_advancedSectionTr == null)
            {
                _advancedSectionTr = _formContentRt != null ? _formContentRt.Find("AdvancedSection") : null;
                if (_advancedSectionTr == null && _formContentRt != null)
                {
                    _advancedSectionTr = FindDeep(_formContentRt, "AdvancedSection");
                }
            }
            // 让 Panel 处于最上方，避免被 Backdrop 覆盖无法点击
            if (_panelRt != null) _panelRt.SetAsLastSibling();
            if (_closeBtn == null)
            {
                var t = transform.Find("Panel/Header/CloseButton");
                if (t != null)
                {
                    _closeBtn = EnsureButtonRaycastable(t);
                    _closeBtn.onClick.RemoveAllListeners();
                }
            }
            if (_cancelBtn == null)
            {
                var t = transform.Find("Panel/Footer/CancelButton");
                if (t != null)
                {
                    _cancelBtn = EnsureButtonRaycastable(t);
                    _cancelBtn.onClick.RemoveAllListeners();
                    _cancelBtn.onClick.AddListener(OnCancelClicked);
                }
            }
            if (_confirmBtn == null)
            {
                var t = transform.Find("Panel/Footer/ConfirmButton");
                if (t != null)
                {
                    _confirmBtn = EnsureButtonRaycastable(t);
                    _confirmBtn.onClick.RemoveAllListeners();
                    _confirmBtn.onClick.AddListener(OnConfirmClicked);
                }
            }
            if (_backdropBtn == null)
            {
                var t = transform.Find("Backdrop");
                if (t != null)
                {
                    _backdropBtn = t.GetComponent<Button>();
                    if (_backdropBtn == null) _backdropBtn = t.gameObject.AddComponent<Button>();
                    var img = t.GetComponent<Image>();
                    if (img == null) img = t.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.5f);
                    img.raycastTarget = true;
                    _backdropBtn.transition = Selectable.Transition.None;
                    _backdropBtn.onClick.RemoveAllListeners();
                }
            }

            // 绑定选择图标/背景按钮
            if (_chooseIconBtn == null)
            {
                var t = FindDeep(_panelRt != null ? _panelRt.transform : transform, "ChooseIconButton");
                if (t != null)
                {
                    _chooseIconBtn = EnsureButtonRaycastable(t);
                    _chooseIconBtn.onClick.RemoveAllListeners();
                    _chooseIconBtn.onClick.AddListener(OnChooseIconClicked);
                }
            }
            if (_iconPreviewImg == null)
            {
                Transform t = null;
                t = transform.Find("Panel/FormScroll/FormViewport/FormContent/Row_IconPicker/Control/IconPreview");
                if (t == null) t = transform.Find("Panel/FormScroll/FormViewport/FormContent/FormContentRow_IconPicker/Control/IconPreview");
                if (t == null) t = FindDeep(transform, "IconPreview");
                if (t != null)
                {
                    _iconPreviewImg = t.GetComponent<Image>();
                    if (_iconPreviewImg == null) _iconPreviewImg = t.gameObject.AddComponent<Image>();
                    _iconPreviewImg.preserveAspect = true;
                }
            }
            if (_bgPreviewImg == null)
            {
                Transform t = null;
                t = transform.Find("Panel/FormScroll/FormViewport/FormContent/Row_BackgroundPicker/Control/BgPreview");
                if (t == null) t = transform.Find("Panel/FormScroll/FormViewport/FormContent/FormContentRow_BackgroundPicker/Control/BgPreview");
                if (t == null) t = FindDeep(transform, "BgPreview");
                if (t != null)
                {
                    _bgPreviewImg = t.GetComponent<Image>();
                    if (_bgPreviewImg == null) _bgPreviewImg = t.gameObject.AddComponent<Image>();
                    _bgPreviewImg.preserveAspect = true;
                }
            }
            if (_chooseBgBtn == null)
            {
                var t = FindDeep(_panelRt != null ? _panelRt.transform : transform, "ChooseBackgroundButton");
                if (t != null)
                {
                    _chooseBgBtn = EnsureButtonRaycastable(t);
                    _chooseBgBtn.onClick.RemoveAllListeners();
                    _chooseBgBtn.onClick.AddListener(OnChooseBackgroundClicked);
                }
            }

            // 绑定 Row_Toggle 控制 Row_PrereqMultiSelect 显隐（按用户给定精确路径）
            if (_rowToggleTr == null || _rowToggle == null)
            {
                // 精确定位行
                _rowToggleTr = transform.Find("Panel/FormScroll/FormViewport/FormContent/Row_Toggle");
                if (_rowToggleTr == null && _formContentRt != null)
                {
                    _rowToggleTr = _formContentRt.Find("Row_Toggle");
                }
                if (_rowToggleTr == null && _formContentRt != null)
                {
                    _rowToggleTr = FindDeep(_formContentRt, "Row_Toggle");
                }
                if (_rowToggleTr == null)
                {
                    Mod.Log?.LogWarning("Row_Toggle not found under FormContent (exact/deep). Check hierarchy and names.");
                    if (_formContentRt != null)
                    {
                        // 枚举一次子节点帮助定位
                        int cc = _formContentRt.childCount;
                        Mod.Log?.LogInfo($"FormContent children count={cc}");
                        for (int i = 0; i < cc; i++)
                        {
                            var ch = _formContentRt.GetChild(i);
                            Mod.Log?.LogInfo($" - child[{i}] name={ch.name}");
                        }
                    }
                }

                // 精确定位 Toggle
                var exactToggleTr = transform.Find("Panel/FormScroll/FormViewport/FormContent/Row_Toggle/Control/Toggle");
                if (exactToggleTr != null)
                {
                    _rowToggle = exactToggleTr.GetComponent<Toggle>();
                }
                if (_rowToggle == null && _rowToggleTr != null)
                {
                    _rowToggle = _rowToggleTr.GetComponentInChildren<Toggle>(true);
                    if (_rowToggle == null)
                    {
                        var tt = _rowToggleTr.Find("Toggle");
                        if (tt != null) _rowToggle = tt.GetComponent<Toggle>();
                    }
                }
                if (_rowToggle == null)
                {
                    Mod.Log?.LogWarning("Toggle component not found at Row_Toggle/Control/Toggle nor within Row_Toggle subtree.");
                }
                if (_rowToggle != null)
                {
                    _rowToggle.onValueChanged.RemoveListener(OnRowToggleChanged);
                    _rowToggle.onValueChanged.AddListener(OnRowToggleChanged);
                    _lastToggleState = _rowToggle.isOn;
                }
            }
            ApplyToggleVisibility();
        }

        private void EnsureVisibility()
        {
            try
            {
                // Root CanvasGroup 保证可见与可交互
                var rootCg = GetComponent<CanvasGroup>();
                if (rootCg == null) rootCg = gameObject.AddComponent<CanvasGroup>();
                rootCg.alpha = 1f; rootCg.interactable = true; rootCg.blocksRaycasts = true;

                // Backdrop & Panel 必须激活
                if (_backdropTr != null && !_backdropTr.gameObject.activeSelf) _backdropTr.gameObject.SetActive(true);
                if (_panelRt != null && !_panelRt.gameObject.activeSelf) _panelRt.gameObject.SetActive(true);

                // 确保 Panel 在 Backdrop 之上，避免遮挡点击
                if (_panelRt != null) _panelRt.SetAsLastSibling();

                // Panel 的最小可视尺寸与居中
                if (_panelRt != null)
                {
                    var rect = _panelRt.rect;
                    bool tooSmall = rect.width < 50f || rect.height < 50f;
                    if (tooSmall)
                    {
                        _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                        _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                        _panelRt.sizeDelta = new Vector2(840f, 640f);
                    }
                    _panelRt.anchoredPosition = Vector2.zero;
                    _panelRt.localScale = Vector3.one;
                }

                
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"CreateChapterDialog EnsureVisibility error: {e.Message}");
            }
        }

        private void Update()
        {
            // 兜底：动态重绑与状态轮询，确保显隐生效
            if (_rowToggle == null || _rowToggleTr == null)
            {
                TryBind();
            }
            if (_rowToggle != null)
            {
                bool cur = _rowToggle.isOn;
                if (!_lastToggleState.HasValue || _lastToggleState.Value != cur)
                {
                    _lastToggleState = cur;
                    ApplyToggleVisibility();
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) OnCancelClicked();
        }

        private void OnCloseClicked()
        {
            UIManager.CloseCreateChapterDialog();
        }

        private void OnCancelClicked()
        {
            try
            {
                bool opened = UIManager.OpenConfirmExitDialog("确认退出新建章节？未保存的内容将丢失。", () =>
                {
                    UIManager.CloseCreateChapterDialog();
                }, () => { });
                if (!opened)
                {
                    UIManager.CloseCreateChapterDialog();
                }
            }
            catch
            {
                UIManager.CloseCreateChapterDialog();
            }
        }

        private void EnsureDefaultIconPreview()
        {
            if (_iconPreviewImg == null) return;
            if (_iconPreviewImg.sprite != null && _runtimeIconSprite == null) return;
            var dir = UIManager.GetIconsDirectory();
            if (string.IsNullOrEmpty(dir)) return;
            var path = Path.Combine(dir, "ChapterI.png");
            if (!File.Exists(path)) return;
            var sp = UIManager.LoadSpriteFromFile(path);
            if (sp != null)
            {
                SetIconPreview(sp, path);
            }
        }

        private void SetIconPreview(Sprite sp, string srcPath)
        {
            if (sp == null || _iconPreviewImg == null) return;
            if (_runtimeIconSprite != null)
            {
                if (_runtimeIconSprite.texture != null) Destroy(_runtimeIconSprite.texture);
                Destroy(_runtimeIconSprite);
                _runtimeIconSprite = null;
            }
            _iconPreviewImg.sprite = sp;
            _iconPreviewImg.preserveAspect = true;
            _runtimeIconSprite = sp;
            _selectedIconPath = srcPath;
        }

        private void OnDestroy()
        {
            if (_runtimeIconSprite != null)
            {
                if (_runtimeIconSprite.texture != null) Destroy(_runtimeIconSprite.texture);
                Destroy(_runtimeIconSprite);
                _runtimeIconSprite = null;
            }
            if (_runtimeBgSprite != null)
            {
                if (_runtimeBgSprite.texture != null) Destroy(_runtimeBgSprite.texture);
                Destroy(_runtimeBgSprite);
                _runtimeBgSprite = null;
            }
        }

        private void OnChooseIconClicked()
        {
            // 优先使用游戏内 IconPickerDialog；若缺失则回退到系统文件对话框
            try
            {
                bool opened = UIManager.OpenIconPicker((path, sp) =>
                {
                    if (!string.IsNullOrEmpty(path) && sp != null)
                        SetIconPreview(sp, path);
                });
                if (opened) return;
            }
            catch { }
            try
            {
                var dir = UIManager.GetIconsDirectory();
                string path;
                bool ok = UIManager.TryPickFileWithDialog(dir, "PNG 文件|*.png|所有文件|*.*", "选择章节图标", out path);
                if (!ok || string.IsNullOrEmpty(path)) return;
                var sp = UIManager.LoadSpriteFromFile(path);
                if (sp != null) SetIconPreview(sp, path);
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"ChooseIcon error: {e.Message}");
            }
        }

        private void OnChooseBackgroundClicked()
        {
            try
            {
                bool opened = UIManager.OpenBackgroundPicker((path, sp) =>
                {
                    if (!string.IsNullOrEmpty(path) && sp != null)
                        SetBgPreview(sp, path);
                });
                if (opened) return;
            }
            catch { }
            // 兜底：系统文件对话框
            try
            {
                var dir = UIManager.GetBackgroundsDirectory();
                string path;
                bool ok = UIManager.TryPickFileWithDialog(dir, "图片文件|*.png;*.jpg;*.jpeg|所有文件|*.*", "选择背景", out path);
                if (!ok || string.IsNullOrEmpty(path)) return;
                var sp = UIManager.LoadSpriteFromFile(path);
                if (sp != null) SetBgPreview(sp, path);
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"ChooseBackground error: {e.Message}");
            }
        }

        private void OnRowToggleChanged(bool isOn)
        {
            ApplyToggleVisibility();
        }

        private void ApplyToggleVisibility()
        {
            try
            {
                // 用户明确要求：Toggle 为 true 时显示 Row_PrereqMultiSelect；false 时不显示
                bool on = _rowToggle != null ? _rowToggle.isOn : false;
                if (on)
                {
                    if (_rowPrereqRuntime == null)
                    {
                        // 若预制体中已存在禁用的占位行，则优先复用
                        if (_formContentRt != null)
                        {
                            var pre = _formContentRt.Find("Row_PrereqMultiSelect");
                            if (pre != null)
                            {
                                _rowPrereqRuntime = pre;
                            }
                        }
                        var pf = UIManager.LoadFromBundle("Assets/Prefab/Rows/Row_PrereqMultiSelect");
                        if (pf == null) pf = UIManager.LoadFromBundle("Row_PrereqMultiSelect");
                        if (pf == null)
                        {
                            Mod.Log?.LogWarning("Row_PrereqMultiSelect prefab not found in AB.");
                            return;
                        }
                        if (_rowPrereqRuntime == null)
                        {
                            // 父容器固定为 FormContent，且插在 Row_Toggle 下一行
                            Transform parent = _formContentRt != null ? _formContentRt : (_rowToggleTr != null ? _rowToggleTr.parent : transform);
                            var go = Instantiate(pf, parent);
                            go.name = "Row_PrereqMultiSelect(Runtime)";
                            _rowPrereqRuntime = go.transform;
                        }
                    }
                    // 可见并排位在 Row_Toggle 之后
                    _rowPrereqRuntime.gameObject.SetActive(true);
                    if (_rowToggleTr != null && _rowPrereqRuntime.parent == _rowToggleTr.parent)
                    {
                        _rowPrereqRuntime.SetSiblingIndex(_rowToggleTr.GetSiblingIndex() + 1);
                    }
                }
                else
                {
                    if (_rowPrereqRuntime != null)
                    {
                        _rowPrereqRuntime.gameObject.SetActive(false);
                    }
                }
                if (_formContentRt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_formContentRt);
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"ApplyToggleVisibility error: {e.Message}");
            }
        }

        private void OnConfirmClicked()
        {
            try
            {
                // 新规则映射（创建/编辑通用）
                string inputId = GetInputValue(new[] { "Row_TextField" });
                string description = GetInputValue(new[] { "Row_TextArea" });
                bool visible = GetToggleValue(new[] { "Row_Toggle" }, true);

                if (_isEditMode && !string.IsNullOrEmpty(_editingChapterId))
                {
                    // 编辑模式：不创建新章节，仅更新元数据与可视资源
                    string id = _editingChapterId;
                    string name = id; // 名称统一用 id 展示
                    int order = _editingOrder;
                    try
                    {
                        UIManager.SaveCreatedChapterMeta(id, name, order, visible, description);
                        UIManager.SaveChapterVisual(id, _selectedIconPath, _selectedBgPath);
                        UIManager.RebuildChaptersAndSelect(id);
                    }
                    catch (Exception ex)
                    {
                        Mod.Log?.LogWarning($"EditChapter save error: {ex.Message}");
                    }
                }
                else
                {
                    // 创建模式
                    string id = inputId;
                    int order = 0;
                    string name = id;
                    if (string.IsNullOrWhiteSpace(id)) id = "chapter";

                    string finalId;
                    var ok = UIManager.TryCreateChapter(id, name, order, visible, out finalId);
                    Mod.Log?.LogInfo($"CreateChapterDialog: Confirm creating -> id={id} name={name} order={order} visible={visible} result={ok} finalId={finalId}");
                    if (ok && !string.IsNullOrEmpty(finalId))
                    {
                        // 持久化：保存所选图标与背景路径
                        try
                        {
                            UIManager.SaveCreatedChapterMeta(finalId, name, order, visible, description);
                            UIManager.SaveChapterVisual(finalId, _selectedIconPath, _selectedBgPath);
                            // 由于 TryCreateChapter 中已 BuildUI，但当时尚未保存用户数据，这里再刷新一次确保图标立即应用
                            UIManager.RebuildChaptersAndSelect(finalId);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"CreateChapterDialog confirm error: {e.Message}");
            }
            UIManager.CloseCreateChapterDialog();
        }

        internal void InitializeForEdit(string chapterId, string description, bool visible, string iconPath, string bgPath, int order)
        {
            _isEditMode = true;
            _editingChapterId = chapterId;
            _editingOrder = order;

            // 预填 ID（只读）
            try
            {
                if (_formContentRt == null)
                {
                    var t = transform.Find("Panel/FormScroll/FormViewport/FormContent");
                    _formContentRt = t as RectTransform;
                }
                if (_formContentRt != null)
                {
                    // Row_TextField
                    var rowId = _formContentRt.Find("Row_TextField");
                    if (rowId != null)
                    {
                        var inp = rowId.GetComponentInChildren<InputField>(true);
                        if (inp != null)
                        {
                            inp.text = chapterId ?? string.Empty;
                            inp.interactable = false; // 编辑时不允许修改 ID
                        }
                    }
                    // Row_TextArea
                    var rowDesc = _formContentRt.Find("Row_TextArea");
                    if (rowDesc != null)
                    {
                        var inp2 = rowDesc.GetComponentInChildren<InputField>(true);
                        if (inp2 != null)
                        {
                            inp2.text = description ?? string.Empty;
                        }
                    }
                    // Row_Toggle
                    var rowTg = _formContentRt.Find("Row_Toggle");
                    if (rowTg != null)
                    {
                        var tg = rowTg.GetComponentInChildren<Toggle>(true);
                        if (tg != null) tg.isOn = visible;
                        _rowToggle = tg;
                        _rowToggleTr = rowTg;
                        _lastToggleState = tg != null ? (bool?)tg.isOn : null;
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"InitializeForEdit fill fields error: {e.Message}");
            }

            // 图标与背景预览
            try
            {
                if (!string.IsNullOrEmpty(iconPath))
                {
                    var sp = UIManager.LoadSpriteFromFile(iconPath);
                    if (sp != null)
                    {
                        SetIconPreview(sp, iconPath);
                    }
                }
                if (!string.IsNullOrEmpty(bgPath))
                {
                    var sp2 = UIManager.LoadSpriteFromFile(bgPath);
                    if (sp2 != null)
                    {
                        SetBgPreview(sp2, bgPath);
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"InitializeForEdit set previews error: {e.Message}");
            }

            // 调整确认按钮文本（可选，不影响功能）
            try
            {
                var t = transform.Find("Panel/Footer/ConfirmButton/Label");
                var txt = t != null ? t.GetComponent<Text>() : null;
                if (txt != null) txt.text = "保存";
            }
            catch { }
        }

        private string GetInputValue(string[] rowNames)
        {
            if (_formContentRt == null)
            {
                return null;
            }
            foreach (var row in rowNames)
            {
                // 典型路径：FormContent/Row_X/InputField
                var t = _formContentRt.Find(row);
                if (t == null) continue;
                // 优先直接找 InputField 组件
                var inp = t.GetComponentInChildren<InputField>(true);
                if (inp != null) return inp.text;
                // 退化：直接在该行下找名为 InputField 的节点
                var it = t.Find("InputField");
                if (it != null)
                {
                    var inp2 = it.GetComponent<InputField>();
                    if (inp2 != null) return inp2.text;
                }
            }
            return null;
        }

        private bool GetToggleValue(string[] rowNames, bool def)
        {
            if (_formContentRt == null) return def;
            foreach (var row in rowNames)
            {
                var t = _formContentRt.Find(row);
                if (t == null) continue;
                var tg = t.GetComponentInChildren<Toggle>(true);
                if (tg != null) return tg.isOn;
                var tt = t.Find("Toggle");
                if (tt != null)
                {
                    var tg2 = tt.GetComponent<Toggle>();
                    if (tg2 != null) return tg2.isOn;
                }
            }
            return def;
        }

        private Button EnsureButtonRaycastable(Transform t)
        {
            if (t == null) return null;
            var btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0f); // 透明占位，保证有可射线 Graphic
            }
            img.raycastTarget = true;
            if (btn.targetGraphic == null) btn.targetGraphic = img;
            return btn;
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

        private Transform FindDeepCI(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var target = name.Trim().ToLowerInvariant();
            if (root.name.Trim().ToLowerInvariant() == target) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeepCI(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private Transform FindByExactPath(Transform start, string path)
        {
            if (start == null || string.IsNullOrEmpty(path)) return null;
            return start.Find(path);
        }

        private static string GetFullPath(Transform t)
        {
            if (t == null) return "<null>";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (t != null)
            {
                if (sb.Length == 0) sb.Insert(0, t.name); else sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }

        private void SetBgPreview(Sprite sp, string srcPath)
        {
            if (sp == null || _bgPreviewImg == null) return;
            if (_runtimeBgSprite != null)
            {
                if (_runtimeBgSprite.texture != null) Destroy(_runtimeBgSprite.texture);
                Destroy(_runtimeBgSprite);
                _runtimeBgSprite = null;
            }
            _bgPreviewImg.sprite = sp;
            _bgPreviewImg.preserveAspect = true;
            _runtimeBgSprite = sp;
            _selectedBgPath = srcPath;
        }
    }
}
