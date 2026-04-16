using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace JustEnoughItems.Patches
{
    // 在 PDA 关闭时自动关闭 JEI，防止 UI 重叠
    internal static class PdaCloseHideJei_DISABLED
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var list = new List<MethodBase>();
            var t1 = AccessTools.TypeByName("uGUI_PDA");
            if (t1 != null)
            {
                var m1 = AccessTools.Method(t1, "Close");
                if (m1 != null) list.Add(m1);
                var m2 = AccessTools.Method(t1, "OnClose");
                if (m2 != null) list.Add(m2);
            }
            var t2 = AccessTools.TypeByName("PDA");
            if (t2 != null)
            {
                var m3 = AccessTools.Method(t2, "Close");
                if (m3 != null) list.Add(m3);
            }
            return list;
        }

        static void Postfix()
        {
            try { Debug.Log("[JEI][PDA] Close detected => Hide JEI"); } catch { }
            try { JustEnoughItems.UI.JeiManager.Hide(); } catch { }
        }
    }
}
