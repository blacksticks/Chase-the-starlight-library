using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JustEnoughItems.UI
{
    public class GlobalHotkeys : MonoBehaviour
    {
        private static System.Reflection.MethodInfo _miGetButtonDown;
        private float _nextHeartbeat;
        private static Type _gameInputType;
        private bool _handledThisFrame;
        private static int _lastKeyHandledFrame = -1;

        private void Awake()
        {
            JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys Awake (enabled=" + enabled + ")");
        }

        private void OnEnable()
        {
            JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys OnEnable");
            _nextHeartbeat = Time.time + 5f;
        }

        private void OnGUI()
        {
            // 禁用 OnGUI 中的输入处理；只保留 Update 作为唯一入口，避免同帧多入口导致首次闪退
            return;
        }

        private void Update()
        {
            _handledThisFrame = false;
            if (Time.time >= _nextHeartbeat)
            {
                JustEnoughItems.Plugin.Log?.LogDebug("GlobalHotkeys heartbeat");
                _nextHeartbeat = Time.time + 5f;
            }
            // 每0.5秒打印一次 J 是否被按住（用于诊断）
            try
            {
                if (Time.frameCount % 30 == 0) // ~0.5s 在60FPS时
                {
                    bool heldJ = Input.GetKey(KeyCode.J);
                    if (heldJ)
                    {
                        JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys: J is being held (Update)");
                    }
                }
            }
            catch { }

            // 更新 HoverContext：对当前鼠标位置进行 UI 射线检测，解析 TechType
            try { DoUIRaycastAndUpdateHover(); } catch { }

            // 优先使用 Mod Input 注册的 JEI 按钮（通常绑定到 J）；否则回退到配置键/J
            bool pressedModInput = false;
            try
            {
                if (JustEnoughItems.Plugin.JeiButtonEnum != null && JustEnoughItems.Plugin.JeiButtonEnumType != null)
                {
                    if (_miGetButtonDown == null)
                    {
                        _miGetButtonDown = typeof(GameInput).GetMethod("GetButtonDown", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new System.Type[] { JustEnoughItems.Plugin.JeiButtonEnumType }, null);
                        if (_miGetButtonDown == null)
                        {
                            JustEnoughItems.Plugin.Log?.LogWarning("GlobalHotkeys: Cannot resolve GameInput.GetButtonDown for Mod Input enum type");
                        }
                    }
                    if (_miGetButtonDown != null)
                    {
                        pressedModInput = (bool)_miGetButtonDown.Invoke(null, new object[] { JustEnoughItems.Plugin.JeiButtonEnum });
                    }
                }
            }
            catch (System.Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogWarning($"GlobalHotkeys: Mod Input check failed: {ex.Message}");
            }

            bool pressedConfig = false;
            try { if (JustEnoughItems.Plugin.OpenKey != null) pressedConfig = Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value); } catch { }
            bool pressedRawJ = Input.GetKeyDown(KeyCode.J);

            if (pressedModInput || pressedConfig || pressedRawJ)
            {
                if (Time.frameCount == _lastKeyHandledFrame) return;
                try
                {
                    if (pressedModInput) JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys: Open key pressed via Mod Input");
                    else if (pressedConfig) JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys: Open key pressed via Config Key");
                    else if (pressedRawJ) JustEnoughItems.Plugin.Log?.LogInfo("GlobalHotkeys: Open key pressed via KeyCode.J");
                    // 打印环境上下文
                    bool pdaOpen = false; bool menuSelected = false;
                    // 旧版接口：仅使用 uGUI_PDA.isOpen
                    try
                    {
                        var tPda = AccessTools.TypeByName("uGUI_PDA");
                        var inst = UnityEngine.Object.FindObjectOfType(tPda ?? typeof(UnityEngine.Component)) as Component;
                        if (inst != null && tPda != null)
                        {
                            var fi = tPda.GetField("isOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var pi = tPda.GetProperty("isOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fi != null) { pdaOpen = (bool)(fi.GetValue(inst) ?? false); }
                            else if (pi != null) { pdaOpen = (bool)(pi.GetValue(inst, null) ?? false); }
                        }
                    }
                    catch { }
                    try { menuSelected = IngameMenu.main?.selected ?? false; } catch { }
                    try { JustEnoughItems.Plugin.Log?.LogInfo($"GlobalHotkeys: Context pdaOpen={pdaOpen}, menuSelected={menuSelected}, cursorLock={Cursor.lockState}"); } catch { }
                    // 仅在 PDA 打开时响应
                    if (!pdaOpen) return;
                }
                catch { }

                // 若已可见，优先关闭
                if (JeiManager.Visible) { try { JeiManager.Hide(); } catch { } _handledThisFrame = true; _lastKeyHandledFrame = Time.frameCount; return; }
                // 1) 优先使用 HoverContext
                var hovered = HoverContext.GetHoveredTechType();
                if (!string.IsNullOrEmpty(hovered))
                {
                    JeiManager.ShowForItem(hovered);
                    _handledThisFrame = true; _lastKeyHandledFrame = Time.frameCount; return;
                }

                // 2) 兜底：尝试从 BlueprintsTab 当前选中获取
                try
                {
                    var tTab = AccessTools.TypeByName("uGUI_BlueprintsTab");
                    var tPda = AccessTools.TypeByName("uGUI_PDA");
                    var fiCurrent = AccessTools.Field(tPda, "currentTab");
                    var pda = UnityEngine.Object.FindObjectOfType(tPda) as Component;
                    var curTab = fiCurrent?.GetValue(pda) as Component;
                    if (curTab != null && curTab.GetType() == tTab)
                    {
                        // 寻找 "selected" 或当前高亮的条目，读取其中的 TechType 字段
                        var selFi = AccessTools.Field(tTab, "selected");
                        var selected = selFi?.GetValue(curTab) as Component;
                        if (selected != null)
                        {
                            var tt = ResolveTechTypeFromComponent(selected);
                            if (!string.IsNullOrEmpty(tt))
                            {
                                JeiManager.ShowForItem(tt);
                                _handledThisFrame = true;
                                return;
                            }
                        }
                    }
                }
                catch { }

                // 3) 若没有 hover/selected，直接切换 JEI 总面板（列表）
                try { JeiManager.Toggle(); } catch { }
                _handledThisFrame = true; _lastKeyHandledFrame = Time.frameCount; return;
            }

            // ESC 关闭
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Time.frameCount == _lastKeyHandledFrame) return;
                JeiManager.Hide();
                _lastKeyHandledFrame = Time.frameCount;
                return;
            }

            // Mod Input 按钮关闭（若可用）
            if (!_handledThisFrame && JustEnoughItems.Plugin.JeiButtonEnum != null)
            {
                if (_gameInputType == null)
                {
                    _gameInputType = AccessTools.TypeByName("GameInput");
                    _miGetButtonDown = _gameInputType?.GetMethod("GetButtonDown", BindingFlags.Public | BindingFlags.Static, null, new Type[] { JustEnoughItems.Plugin.JeiButtonEnumType }, null);
                }
                if (_miGetButtonDown != null)
                {
                    try
                    {
                        var pressed = (bool)_miGetButtonDown.Invoke(null, new object[] { JustEnoughItems.Plugin.JeiButtonEnum });
                        if (pressed)
                        {
                            if (Time.frameCount == _lastKeyHandledFrame) return;
                            JeiManager.Hide();
                            _handledThisFrame = true; _lastKeyHandledFrame = Time.frameCount; return;
                        }
                    }
                    catch { }
                }
            }

            // 回退：BepInEx 配置键关闭（若 JEI 已显示）；若无配置键则回退到 J
            try
            {
                bool pressedClose = false;
                if (JustEnoughItems.Plugin.OpenKey != null)
                    pressedClose = Input.GetKeyDown(JustEnoughItems.Plugin.OpenKey.Value);
                else
                    pressedClose = Input.GetKeyDown(KeyCode.J);
                if (!_handledThisFrame && JeiManager.Visible && pressedClose)
                {
                    if (Time.frameCount == _lastKeyHandledFrame) return;
                    JeiManager.Hide();
                    _lastKeyHandledFrame = Time.frameCount;
                    return;
                }
            }
            catch { }
        }

        private static void DoUIRaycastAndUpdateHover()
        {
            var es = EventSystem.current;
            if (es == null) { HoverContext.ClearHoveredTechType(); return; }

            var ped = new PointerEventData(es);
            ped.position = Input.mousePosition;
            var results = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(ped, results);
            if (results.Count == 0) { HoverContext.ClearHoveredTechType(); return; }

            foreach (var rr in results)
            {
                var go = rr.gameObject;
                if (go == null) continue;
                var comp = go.GetComponent<Component>();
                var tt = ResolveTechTypeFromHierarchy(go.transform);
                if (!string.IsNullOrEmpty(tt))
                {
                    HoverContext.SetHoveredTechType(tt);
                    return;
                }
            }
            HoverContext.ClearHoveredTechType();
        }

        private static string ResolveTechTypeFromHierarchy(Transform tr)
        {
            Transform cur = tr;
            while (cur != null)
            {
                var comp = cur.GetComponent<Component>();
                var s = ResolveTechTypeFromComponent(comp);
                if (!string.IsNullOrEmpty(s)) return s;
                cur = cur.parent;
            }
            return null;
        }

        private static string ResolveTechTypeFromComponent(Component comp)
        {
            if (comp == null) return null;
            // 查找字段/属性名中包含 "techtype" 的项
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var f in comp.GetType().GetFields(flags))
            {
                if (f.FieldType == typeof(TechType) && f.Name.ToLowerInvariant().Contains("techtype"))
                {
                    try { var v = (TechType)f.GetValue(comp); return v.ToString(); } catch { }
                }
            }
            foreach (var p in comp.GetType().GetProperties(flags))
            {
                if (p.PropertyType == typeof(TechType) && p.Name.ToLowerInvariant().Contains("techtype"))
                {
                    try { var v = (TechType)p.GetValue(comp, null); return v.ToString(); } catch { }
                }
            }
            return null;
        }
    }
}
