using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughItems.Patches
{
    [HarmonyPatch(typeof(IngameMenu), "Start")]
    internal static class IngameMenuAddJeiButton
    {
        [HarmonyPostfix]
        private static void Postfix(IngameMenu __instance)
        {
            try
            {
                if (__instance == null) return;
                var root = __instance.transform as Transform;
                if (root == null) return;

                // 找到一个可克隆的按钮（例如“选项”按钮）
                var btnTemplate = root.GetComponentInChildren<Button>(true);
                if (btnTemplate == null) return;

                var newBtnGO = UnityEngine.Object.Instantiate(btnTemplate.gameObject, btnTemplate.transform.parent);
                newBtnGO.name = "ButtonJEIConfig";
                var txt = newBtnGO.GetComponentInChildren<Text>(true);
                if (txt != null) txt.text = "JEI JSON目录";
                var btn = newBtnGO.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OpenJeiJsonDir);
                }
                newBtnGO.SetActive(true);
            }
            catch { }
        }

        private static void OpenJeiJsonDir()
        {
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
                try { JustEnoughItems.Plugin.Log?.LogError($"Open JEI json dir failed: {ex}"); } catch { }
            }
        }
    }
}
