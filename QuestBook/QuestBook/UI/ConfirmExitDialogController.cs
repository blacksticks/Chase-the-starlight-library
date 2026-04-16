using System;
using UnityEngine;
using UnityEngine.UI;
using QuestBook;

namespace QuestBook.UI
{
    internal class ConfirmExitDialogController : MonoBehaviour
    {
        private Button _yesBtn;
        private Button _noBtn;
        private Button _closeBtn;
        private Button _backdropBtn;
        private Text _titleTxt;
        private Text _messageTxt;

        private Action _onYes;
        private Action _onNo;

        internal void Initialize(string message, Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;
            TryBind();
            SetMessage(message);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void TryBind()
        {
            if (_titleTxt == null)
            {
                var t = transform.Find("Panel/Header/Title");
                if (t != null) _titleTxt = t.GetComponent<Text>();
                if (_titleTxt != null && string.IsNullOrEmpty(_titleTxt.text)) _titleTxt.text = "确认退出";
            }
            if (_messageTxt == null)
            {
                // Content/Message 或 Scroll/Viewport/Content/Message 兼容
                var t = transform.Find("Panel/Content/Message");
                if (t == null) t = transform.Find("Panel/Scroll/Viewport/Content/Message");
                if (t != null) _messageTxt = t.GetComponent<Text>();
            }
            if (_yesBtn == null)
            {
                var t = transform.Find("Panel/Footer/ConfirmButton");
                if (t != null)
                {
                    _yesBtn = EnsureButton(t);
                    _yesBtn.onClick.RemoveAllListeners();
                    _yesBtn.onClick.AddListener(OnYesClicked);
                }
            }
            if (_noBtn == null)
            {
                var t = transform.Find("Panel/Footer/CancelButton");
                if (t != null)
                {
                    _noBtn = EnsureButton(t);
                    _noBtn.onClick.RemoveAllListeners();
                    _noBtn.onClick.AddListener(OnNoClicked);
                }
            }
            if (_closeBtn == null)
            {
                var t = transform.Find("Panel/Header/CloseButton");
                if (t != null)
                {
                    _closeBtn = EnsureButton(t);
                    _closeBtn.onClick.RemoveAllListeners();
                    _closeBtn.onClick.AddListener(OnNoClicked);
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
                    // 按需求：点击空白不关闭；如需视为取消，改为 _backdropBtn.onClick.AddListener(OnNoClicked);
                }
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) OnNoClicked();
        }

        private void SetMessage(string msg)
        {
            if (_messageTxt != null)
            {
                _messageTxt.text = string.IsNullOrEmpty(msg) ? "确认退出当前页面？" : msg;
            }
        }

        private void OnYesClicked()
        {
            try { _onYes?.Invoke(); } catch { }
        }

        private void OnNoClicked()
        {
            try { _onNo?.Invoke(); } catch { }
        }

        private Button EnsureButton(Transform t)
        {
            if (t == null) return null;
            var btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0f);
            }
            img.raycastTarget = true;
            if (btn.targetGraphic == null) btn.targetGraphic = img;
            return btn;
        }
    }
}
