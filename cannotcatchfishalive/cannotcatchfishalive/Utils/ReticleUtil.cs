using System;
using System.Reflection;

namespace CannotCatchFishAlive.Utils
{
    internal static class ReticleUtil
    {
        private static MethodInfo _miSetText;
        private static MethodInfo _miSetTextRaw;
        private static MethodInfo _miSetInteractText;
        private static MethodInfo _miSetIcon;
        private static object _textTypeHand;
        private static object _iconTypeHand;
        private static bool _inited;

        private static void EnsureInit()
        {
            if (_inited) return;
            _inited = true;
            var type = typeof(HandReticle);
            // 缓存 TextType.Hand 值
            var textType = type.GetNestedType("TextType", BindingFlags.Public | BindingFlags.NonPublic);
            if (textType != null)
            {
                _textTypeHand = Enum.Parse(textType, "Hand");
            }
            // 缓存 IconType.Hand 值
            var iconType = type.GetNestedType("IconType", BindingFlags.Public | BindingFlags.NonPublic);
            if (iconType != null)
            {
                try { _iconTypeHand = Enum.Parse(iconType, "Hand"); } catch { _iconTypeHand = null; }
            }
            // 可能存在的签名（不同版本差异）
            // SetText(TextType, string, bool)
            _miSetText = type.GetMethod("SetText", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            // SetTextRaw(TextType, string, bool) 或 (TextType, string)
            _miSetTextRaw = type.GetMethod("SetTextRaw", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            // SetInteractText(string, string)
            _miSetInteractText = type.GetMethod("SetInteractText", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            // SetIcon(IconType)
            _miSetIcon = type.GetMethod("SetIcon", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void TrySetHandText(string text)
        {
            var reticle = HandReticle.main;
            if (reticle == null || string.IsNullOrEmpty(text)) return;
            EnsureInit();
            try
            {
                if (_miSetTextRaw != null && _textTypeHand != null)
                {
                    var ps = _miSetTextRaw.GetParameters();
                    if (ps.Length == 3)
                    {
                        _miSetTextRaw.Invoke(reticle, new object[] { _textTypeHand, text, true });
                        return;
                    }
                    if (ps.Length == 2)
                    {
                        _miSetTextRaw.Invoke(reticle, new object[] { _textTypeHand, text });
                        return;
                    }
                }
                if (_miSetText != null && _textTypeHand != null)
                {
                    var ps = _miSetText.GetParameters();
                    if (ps.Length == 3)
                    {
                        _miSetText.Invoke(reticle, new object[] { _textTypeHand, text, true });
                        return;
                    }
                }
                if (_miSetInteractText != null)
                {
                    var ps = _miSetInteractText.GetParameters();
                    if (ps.Length == 2)
                    {
                        _miSetInteractText.Invoke(reticle, new object[] { text, string.Empty });
                        return;
                    }
                    if (ps.Length == 1)
                    {
                        _miSetInteractText.Invoke(reticle, new object[] { text });
                        return;
                    }
                }
            }
            catch
            {
                // 忽略失败，保持稳健
            }
        }

        public static void TrySetInteractText(string primary, string secondary = "")
        {
            var reticle = HandReticle.main;
            if (reticle == null) return;
            EnsureInit();
            try
            {
                if (_miSetInteractText != null)
                {
                    var ps = _miSetInteractText.GetParameters();
                    if (ps.Length == 2)
                    {
                        _miSetInteractText.Invoke(reticle, new object[] { primary ?? string.Empty, secondary ?? string.Empty });
                        return;
                    }
                    if (ps.Length == 1)
                    {
                        _miSetInteractText.Invoke(reticle, new object[] { primary ?? string.Empty });
                        return;
                    }
                }
                // 退化到手部文本
                TrySetHandText(primary);
            }
            catch { }
        }

        public static void TrySetIconHand()
        {
            var reticle = HandReticle.main;
            if (reticle == null) return;
            EnsureInit();
            try
            {
                if (_miSetIcon != null && _iconTypeHand != null)
                {
                    _miSetIcon.Invoke(reticle, new object[] { _iconTypeHand });
                }
            }
            catch { }
        }
    }
}
