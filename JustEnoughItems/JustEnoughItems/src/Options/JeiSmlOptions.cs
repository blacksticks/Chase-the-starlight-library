using System;
using SMLHelper.V2.Handlers;
using SMLHelper.V2.Options;
using UnityEngine;

namespace JustEnoughItems.Options
{
    public class JeiSmlOptions : ModOptions
    {
        private const string BtnOpenJson = "open_json";

        public JeiSmlOptions() : base("Just Enough Items")
        {
            try
            {
                this.ButtonClicked += HandleButtonClicked;
            }
            catch (Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogWarning($"SML Options ctor subscribe failed: {ex.Message}");
            }
        }

        public override void BuildModOptions()
        {
            try
            {
                AddButtonOption(BtnOpenJson, "打开 JEI JSON 目录");
            }
            catch (Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogWarning($"SML Options Build failed: {ex.Message}");
            }
        }

        private void HandleButtonClicked(object sender, ButtonClickedEventArgs e)
        {
            if (e == null || e.Id != BtnOpenJson) return;
            try
            {
                var dir = JustEnoughItems.Config.JeiSupplementService.ConfigPath;
                var folder = System.IO.Path.GetDirectoryName(dir);
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
#if UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });
#else
                Application.OpenURL("file:///" + folder.Replace("\\", "/"));
#endif
            }
            catch (Exception ex)
            {
                JustEnoughItems.Plugin.Log?.LogError($"Open JEI json dir (SML Options) failed: {ex}");
            }
        }
    }
}
