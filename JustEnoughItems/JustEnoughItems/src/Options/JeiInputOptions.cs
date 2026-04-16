using System;
using Nautilus.Options;
using UnityEngine;

namespace JustEnoughItems.Options
{
    public class JeiInputOptions : ModOptions
    {
        private const string OpenKeyId = "jei_open_key";
        private readonly Options.JeiInputConfig _config;

        public JeiInputOptions(Options.JeiInputConfig config, KeyCode initial)
            : base("Mod Input")
        {
            _config = config;
            // Try to create a ModKeybindOption via reflection; if unavailable, fall back to a button that starts rebind
            var nautilusAsm = AppDomain.CurrentDomain.GetAssemblies();
            Type optItemType = null;
            foreach (var asm in nautilusAsm)
            {
                if (asm.GetName().Name.Contains("Nautilus"))
                {
                    optItemType = asm.GetType("Nautilus.Options.OptionItem");
                    break;
                }
            }

            object toAdd = null;
            foreach (var asm in nautilusAsm)
            {
                if (!asm.GetName().Name.Contains("Nautilus")) continue;
                var mkType = asm.GetType("Nautilus.Options.ModKeybindOption");
                if (mkType != null)
                {
                    var create = mkType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (create != null)
                    {
                        try
                        {
                            toAdd = create.Invoke(null, new object[] { OpenKeyId, "Open JEI", GameInput.Device.Keyboard, initial, "打开/关闭 JEI UI" });
                        }
                        catch { toAdd = null; }
                    }
                    break;
                }
            }

            if (toAdd == null)
            {
                // Fallback to a button to start interactive rebind
                foreach (var asm in nautilusAsm)
                {
                    if (!asm.GetName().Name.Contains("Nautilus")) continue;
                    var btnType = asm.GetType("Nautilus.Options.ModButtonOption");
                    if (btnType != null)
                    {
                        var createBtn = btnType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (createBtn != null)
                        {
                            Action<object> onClick = _ => { Plugin.BeginRebind(); Plugin.Log?.LogInfo("Mod Input: Start rebind Open JEI"); };
                            toAdd = createBtn.Invoke(null, new object[] { OpenKeyId, $"Rebind Open JEI (Current: {initial})", onClick, "按下后在游戏中按任意键设为新热键" });
                        }
                        break;
                    }
                }
            }

            if (toAdd != null && optItemType != null && toAdd is OptionItem oi)
            {
                AddItem(oi);
            }
            OnChanged += HandleChanged;
        }

        private void HandleChanged(object sender, OptionEventArgs e)
        {
            if (e.Id == OpenKeyId)
            {
                try
                {
                    // Read new value via reflection to avoid compile-time dependency on KeybindChangedEventArgs
                    var prop = e.GetType().GetProperty("Value");
                    var val = prop != null ? prop.GetValue(e) : null;
                    var kc = val is KeyCode k ? k : default;
                    JustEnoughItems.Plugin.OpenKey.Value = kc;
                    JustEnoughItems.Plugin.Log?.LogInfo($"Mod Input changed: OpenJEI = {kc}");
                    if (_config != null)
                    {
                        _config.OpenJei = kc;
                        _config.Save();
                    }
                    try { JustEnoughItems.Plugin.OpenKey.ConfigFile.Save(); } catch { }
                }
                catch (Exception ex)
                {
                    JustEnoughItems.Plugin.Log?.LogWarning($"Failed to apply Mod Input change: {ex.Message}");
                }
            }
        }
    }
}
