using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using QuestBook;

namespace QuestBook.UI
{
    internal class IconPickerDialogController : MonoBehaviour
    {
        private Transform _backdropTr;
        private RectTransform _panelRt;
        private Transform _contentTr;
        private Button _confirmBtn;
        private Button _cancelBtn;

        private List<string> _files;
        private Func<string, Sprite> _spriteLoader;
        private Action<string, Sprite> _onPicked;
        private GameObject _gridItemPrefab;

        private int _selectedIndex = -1;
        private string _selectedPath;
        private readonly List<Button> _itemButtons = new List<Button>();

        public void Initialize(List<string> files, Func<string, Sprite> spriteLoader, Action<string, Sprite> onPicked, GameObject gridItemPrefab)
        {
            _files = files ?? new List<string>();
            _spriteLoader = spriteLoader;
            _onPicked = onPicked;
            _gridItemPrefab = gridItemPrefab;
            TryBind();
            BuildGrid();
        }

        private void TryBind()
        {
            if (_backdropTr == null)
            {
                _backdropTr = transform.Find("Backdrop");
                if (_backdropTr != null)
                {
                    var btn = _backdropTr.GetComponent<Button>();
                    if (btn == null) btn = _backdropTr.gameObject.AddComponent<Button>();
                    var img = _backdropTr.GetComponent<Image>();
                    if (img == null) img = _backdropTr.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.5f);
                    img.raycastTarget = true;
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => UIManager.CloseIconPicker());
                }
            }
            if (_panelRt == null)
            {
                _panelRt = transform.Find("Panel") as RectTransform;
                if (_panelRt != null) _panelRt.SetAsLastSibling();
            }
            if (_contentTr == null)
            {
                var t = transform.Find("Panel/Scroll/Viewport/Content");
                if (t == null && _panelRt != null) t = _panelRt.Find("Scroll/Viewport/Content");
                _contentTr = t;
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
            if (_cancelBtn == null)
            {
                var t = transform.Find("Panel/Footer/CancelButton");
                if (t != null)
                {
                    _cancelBtn = EnsureButtonRaycastable(t);
                    _cancelBtn.onClick.RemoveAllListeners();
                    _cancelBtn.onClick.AddListener(() => UIManager.CloseIconPicker());
                }
            }
        }

        private void BuildGrid()
        {
            if (_contentTr == null || _gridItemPrefab == null) return;
            // clear
            for (int i = _contentTr.childCount - 1; i >= 0; i--)
            {
                var c = _contentTr.GetChild(i);
                Destroy(c.gameObject);
            }
            _itemButtons.Clear();

            for (int i = 0; i < _files.Count; i++)
            {
                var path = _files[i];
                var go = Instantiate(_gridItemPrefab, _contentTr);
                go.name = $"IconGridItem_{i}";

                // 优先使用子节点 Icon 上的 Button（与你的预制体绑定一致），否则回退到根节点 Button
                var iconTr = go.transform.Find("Icon");
                Image iconImg = null;
                Button btn = null;
                if (iconTr != null)
                {
                    iconImg = iconTr.GetComponent<Image>();
                    if (iconImg == null) iconImg = iconTr.gameObject.AddComponent<Image>();
                    iconImg.preserveAspect = true;
                    btn = iconTr.GetComponent<Button>();
                    if (btn == null) btn = iconTr.gameObject.AddComponent<Button>();
                    if (btn.targetGraphic == null) btn.targetGraphic = iconImg;
                    iconImg.raycastTarget = true;
                }
                if (btn == null)
                {
                    btn = EnsureButtonRaycastable(go.transform);
                }
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnItemClicked(idx));
                _itemButtons.Add(btn);

                var sp = _spriteLoader != null ? _spriteLoader(path) : null;
                if (iconImg != null && sp != null) iconImg.sprite = sp;

                // Name (optional)
                var nameTr = go.transform.Find("Name");
                if (nameTr != null)
                {
                    var txt = nameTr.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.text = Path.GetFileName(path);
                    }
                }
            }
        }

        private void OnItemClicked(int idx)
        {
            if (idx < 0 || idx >= _files.Count) return;
            _selectedIndex = idx;
            _selectedPath = _files[idx];
            // 直接确认并关闭
            var sp = _spriteLoader != null ? _spriteLoader(_selectedPath) : null;
            try { _onPicked?.Invoke(_selectedPath, sp); } catch { }
            UIManager.CloseIconPicker();
        }

        private void UpdateSelectionVisual()
        {
            for (int i = 0; i < _itemButtons.Count; i++)
            {
                var btn = _itemButtons[i];
                if (btn == null) continue;
                var colors = btn.colors;
                // 轻微高亮以区分选择
                colors.normalColor = (i == _selectedIndex) ? new Color(0.9f, 0.95f, 1f, 1f) : Color.white;
                btn.colors = colors;
            }
        }

        private void OnConfirmClicked()
        {
            if (_files == null || _files.Count == 0)
            {
                UIManager.CloseIconPicker();
                return;
            }
            if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
                _selectedPath = _files[0];
            }
            var sp = _spriteLoader != null ? _spriteLoader(_selectedPath) : null;
            try { _onPicked?.Invoke(_selectedPath, sp); } catch { }
            UIManager.CloseIconPicker();
        }

        private Button EnsureButtonRaycastable(Transform t)
        {
            if (t == null) return null;
            var btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            // 优先用自身的 Image 作为 targetGraphic，否则使用子 Text 的 Graphic
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0f);
            }
            img.raycastTarget = true;
            btn.targetGraphic = img;
            // 如果按钮下存在 Text 作为视觉元素，仅作为子 Graphic，无需额外处理
            return btn;
        }
    }
}
