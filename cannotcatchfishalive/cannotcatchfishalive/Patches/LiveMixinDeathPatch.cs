using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
 

namespace CannotCatchFishAlive.Patches
{
    [HarmonyPatch]
    internal static class LiveMixinDeathPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(LiveMixin);
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == "Kill" || m.Name == "OnKill")
                    yield return m;
            }
        }

        [HarmonyPostfix]
        static void Postfix(LiveMixin __instance)
        {
            if (__instance == null) return;
            if (!CannotCatchFishAlive.Utils.LiveMixinUtil.IsDeadNow(__instance)) return;
            var lmGo = __instance.gameObject;
            if (lmGo == null) return;
            var creature = lmGo.GetComponentInParent<Creature>();
            if (creature == null) return;
            var root = creature.gameObject;
            if (root == null) return;

            TechType corpseTT;
            if (!CannotCatchFishAlive.Managers.DeadPickupRegistry.TryResolveCorpse(root, out corpseTT) || corpseTT == TechType.None)
                return;

            var target = root.GetComponent<CannotCatchFishAlive.Components.DeadPickupTarget>();
            if (target == null)
                target = root.AddComponent<CannotCatchFishAlive.Components.DeadPickupTarget>();
            target.Root = root;
            target.CorpseTechType = corpseTT;
        }
    }
}
