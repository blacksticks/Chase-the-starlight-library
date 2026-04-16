using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughItems.UI
{
    public class JeiNotFoundPage : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button closeButton;
        private int _shownFrame = -1;

        private void Awake()
        {
            try
            {
                if (titleText == null) titleText = transform.Find("Header/TitleText")?.GetComponent<Text>();
                if (messageText == null) messageText = transform.Find("Body/MessageText")?.GetComponent<Text>();
                if (closeButton == null) closeButton = transform.Find("Header/CloseButton")?.GetComponent<Button>();

                // 不修改标题文本，按你的需求保持原 UI 文字
                if (closeButton != null)
                {
                    closeButton.onClick.RemoveAllListeners();
                    closeButton.onClick.AddListener(() => { try { JustEnoughItems.Plugin.Log?.LogInfo("JEI: CloseButton clicked (NotFoundPage)"); } catch { } try { JeiManager.Hide(); } catch { } });
                }
            }
            catch { }
        }

        public void Init(string id)
        {
            try
            {
                try { _shownFrame = Time.frameCount; } catch { _shownFrame = -1; }
                if (messageText != null)
                {
                    // 仅向 Body/MessageText 注入物品ID（支持 {fallbackId} 占位符）
                    string NormalizeId(string s)
                    {
                        if (string.IsNullOrEmpty(s)) return string.Empty;
                        var t = s.Trim();
                        if (t.StartsWith("TechType.", System.StringComparison.OrdinalIgnoreCase)) t = t.Substring("TechType.".Length);
                        return t.Trim();
                    }
                    var fid = NormalizeId(id);
                    var current = messageText.text ?? string.Empty;
                    if (current.Contains("{fallbackId}"))
                    {
                        messageText.text = current.Replace("{fallbackId}", fid);
                    }
                    else
                    {
                        messageText.text = string.IsNullOrEmpty(fid) ? "未配置物品ID：" : ("未配置物品ID：" + fid);
                    }
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
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"JEI: Page Update close via key (NotFoundPage), viaConfig={viaConfig}, viaJ={viaJ}"); } catch { }
                    JeiManager.Hide();
                }
            }
            catch { }
        }
    }
}
