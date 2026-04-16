using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using Nautilus.Options;
using Nautilus.Options.Attributes;
using UnityEngine;
using JustEnoughItems.Config;

namespace JustEnoughItems.Options
{
    [Menu("JustEnoughItems")]
    public class JeiOptions : ModOptions
    {
        public JeiOptions() : base("JustEnoughItems") { }

        private const string OpenConfigBtnId = "open_config_dir";

        public override void BuildModOptions(uGUI_TabbedControlsPanel panel, int modsTabIndex, System.Collections.Generic.IReadOnlyCollection<OptionItem> options)
        {
            // Ensure items exist before building
            if (Options == null || Options.Count == 0)
            {
                TryPopulateItems();
            }
            base.BuildModOptions(panel, modsTabIndex, this.Options);
        }

        private void TryPopulateItems()
        {
            // 只添加“打开配置文件夹”按钮
            var nautilusAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Contains("Nautilus"));
            var buttonType = nautilusAsm?.GetType("Nautilus.Options.ModButtonOption") ?? nautilusAsm?.GetTypes().FirstOrDefault(t => t.Name == "ModButtonOption");
            if (buttonType != null)
            {
                var createBtn = buttonType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                if (createBtn != null)
                {
                    Action<object> onClick = _ =>
                    {
                        try
                        {
                            var dir = ConfigService.NewConfigJsonDirectory;
                            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                            // 直接用系统文件管理器打开
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = dir,
                                UseShellExecute = true,
                                Verb = "open"
                            });
                        }
                        catch (System.Exception ex)
                        {
                            Plugin.Log?.LogError($"Open Config Directory failed: {ex}");
                            // 兜底：尝试用 URL 打开
                            try { Application.OpenURL("file:///" + ConfigService.NewConfigJsonDirectory.Replace("\\", "/")); } catch { }
                        }
                    };
                    var btnItem = createBtn.Invoke(null, new object[] { OpenConfigBtnId, "打开配置文件夹", onClick, "打开模组 json 目录" }) as OptionItem;
                    if (btnItem != null)
                    {
                        AddItem(btnItem);
                    }
                }
            }
        }
    }
}
