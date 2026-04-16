using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using JustEnoughItems.Config;

namespace JustEnoughItems.UI
{
    public class JeiInternalTracker : MonoBehaviour
    {
        private static JeiInternalTracker _instance;
        private readonly HashSet<TechType> _tracked = new HashSet<TechType>();
        private RectTransform _root;
        private VerticalLayoutGroup _layout;

        public static bool EnsureInstance()
        {
            try
            {
                if (_instance != null) return true;
                var parent = ResolveHudTransform();
                if (parent == null) return false;
                var go = new GameObject("JEI_TrackerPanel", typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                _instance = go.AddComponent<JeiInternalTracker>();
                _instance.InitRoot();
                return true;
            }
            catch { return false; }
        }

        public static bool ToggleTracking(TechType techType)
        {
            if (!EnsureInstance()) return false;
            if (_instance.IsTracked(techType)) return _instance.StopTracking(techType);
            return _instance.StartTracking(techType);
        }

        public static bool IsTrackedStatic(TechType techType)
        {
            return _instance != null && _instance.IsTracked(techType);
        }

        private void InitRoot()
        {
            _root = (RectTransform)transform;
            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-20f, -20f);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 520f);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1080f);
            _layout = gameObject.AddComponent<VerticalLayoutGroup>();
            _layout.spacing = 8f;
            _layout.childAlignment = TextAnchor.UpperRight;
            _layout.childControlWidth = true;
            _layout.childControlHeight = false;
            _layout.childForceExpandHeight = false;
            _layout.childForceExpandWidth = true;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static Transform ResolveHudTransform()
        {
            try
            {
                Type uGuiType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType("uGUI");
                        if (t != null) { uGuiType = t; break; }
                    }
                    catch { }
                }
                if (uGuiType == null) return null;
                var mainProp = uGuiType.GetProperty("main", BindingFlags.Public | BindingFlags.Static);
                if (mainProp == null) return null;
                var main = mainProp.GetValue(null, null);
                if (main == null) return null;
                object hud = null;
                var hudProp = uGuiType.GetProperty("hud", BindingFlags.Public | BindingFlags.Instance);
                if (hudProp != null) hud = hudProp.GetValue(main, null);
                if (hud == null)
                {
                    var hudField = uGuiType.GetField("hud", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (hudField != null) hud = hudField.GetValue(main);
                }
                if (hud == null) return null;
                var hudType = hud.GetType();
                var trProp = hudType.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                return trProp != null ? (Transform)trProp.GetValue(hud, null) : null;
            }
            catch { return null; }
        }

        private bool IsTracked(TechType techType)
        {
            return _tracked.Contains(techType);
        }

        private bool StartTracking(TechType techType)
        {
            if (!CanTrack(techType)) return false;
            _tracked.Add(techType);
            CreateEntry(techType);
            return true;
        }

        private bool StopTracking(TechType techType)
        {
            if (!_tracked.Remove(techType)) return false;
            RemoveEntry(techType);
            return true;
        }

        private bool CanTrack(TechType techType)
        {
            try
            {
                if (!CrafterLogic.IsCraftRecipeUnlocked(techType)) return false;
            }
            catch { }
            return true;
        }

        private void CreateEntry(TechType techType)
        {
            var entry = new GameObject("JEI_Tracked_" + techType, typeof(RectTransform));
            entry.transform.SetParent(transform, false);
            var rt = (RectTransform)entry.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            var h = entry.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(entry.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 48f);
            iconRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48f);
            var img = iconGo.GetComponent<Image>();
            img.sprite = GetIconFor(techType);
            img.color = Color.white;

            var textGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(entry.transform, false);
            var txt = textGo.GetComponent<Text>();
            txt.text = GetDisplayName(techType);
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 18;
            txt.alignment = TextAnchor.MiddleRight;
            txt.color = Color.white;
        }

        private void RemoveEntry(TechType techType)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var ch = transform.GetChild(i);
                if (ch != null && ch.name == "JEI_Tracked_" + techType)
                {
                    UnityEngine.Object.Destroy(ch.gameObject);
                    break;
                }
            }
        }

        private Sprite GetIconFor(TechType tt)
        {
            try
            {
                var token = tt.ToString();
                var spr = JustEnoughItems.IconCache.GetById(token);
                if (spr != null) return spr;
            }
            catch { }
            return null;
        }

        private string GetDisplayName(TechType tt)
        {
            try
            {
                var token = tt.ToString();
                var dict = ConfigService.ChineseNames;
                if (dict != null && dict.TryGetValue(token, out var name) && !string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return tt.ToString();
        }
    }
}
