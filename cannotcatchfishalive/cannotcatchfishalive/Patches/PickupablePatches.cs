using HarmonyLib;
using UnityEngine;

namespace CannotCatchFishAlive.Patches
{
    [HarmonyPatch(typeof(Pickupable))]
    internal static class PickupablePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnHandHover")]
        private static void OnHandHover_Postfix(Pickupable __instance)
        {
            var go = __instance ? __instance.gameObject : null;
            if (go == null) return;
            if (!Utils.DetectionUtil.IsLiveFish(go)) return;
            if (Managers.WhitelistManager.IsWhitelisted(go)) return;
            if (Utils.AquariumUtil.IsInAquarium(go)) return;
            if (Utils.AquariumUtil.IsPlayerInAquarium()) return;
            Utils.ReticleUtil.TrySetHandText("此鱼无法被活着抓取");
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnHandClick")]
        private static bool OnHandClick_Prefix(Pickupable __instance)
        {
            var go = __instance ? __instance.gameObject : null;
            if (go == null) return true;
            if (!Utils.DetectionUtil.IsLiveFish(go)) return true;
            if (Managers.WhitelistManager.IsWhitelisted(go)) return true;
            if (Utils.AquariumUtil.IsInAquarium(go)) return true;
            if (Utils.AquariumUtil.IsPlayerInAquarium()) return true;
            if (Utils.Cooldown.InCooldown())
            {
                ErrorMessage.AddMessage("<color=#FFFF00>说不让你活着抓鱼尼尔多隆吗</color>");
            }
            else
            {
                ErrorMessage.AddMessage("此鱼无法被活着抓取");
                Utils.Cooldown.Mark();
            }
            return false;
        }
    }
}
