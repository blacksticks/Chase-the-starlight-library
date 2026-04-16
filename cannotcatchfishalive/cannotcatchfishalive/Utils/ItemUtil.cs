using System;
using System.Reflection;
using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class ItemUtil
    {
        public static bool TryGiveToPlayer(TechType techType)
        {
            try
            {
                if (techType == TechType.None) return false;
                var player = Player.main;
                if (player == null) return false;
                var inv = Inventory.main;
                if (inv == null) return false;

                // 优先尝试 CraftData.AddToInventory(TechType, int, bool, bool)
                var craftDataType = typeof(CraftData);
                var addToInv = craftDataType.GetMethod("AddToInventory", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (addToInv != null)
                {
                    var ps = addToInv.GetParameters();
                    try
                    {
                        if (ps.Length == 4)
                        {
                            var ok = addToInv.Invoke(null, new object[] { techType, 1, true, true });
                            if (ok is bool b1 && b1) return true;
                        }
                        else if (ps.Length == 2)
                        {
                            var ok = addToInv.Invoke(null, new object[] { techType, 1 });
                            if (ok is bool b2 && b2) return true;
                        }
                    }
                    catch { }
                }

                // 回退：实例化预制体并调用 Inventory.main.Pickup/ForcePickup
                GameObject prefab = null;
                try { prefab = CraftData.GetPrefabForTechType(techType); } catch { }
                GameObject go = null;
                if (prefab != null)
                    go = UnityEngine.Object.Instantiate(prefab);
                if (go == null)
                {
                    try { go = CraftData.InstantiateFromPrefab(techType); } catch { }
                }
                if (go == null) return false;

                var pu = go.GetComponent<Pickupable>();
                if (pu == null) pu = go.AddComponent<Pickupable>();
                pu.isPickupable = true;
                var tag = go.GetComponent<TechTag>();
                if (tag == null) tag = go.AddComponent<TechTag>();
                tag.type = techType;

                // 尝试调用 Pickup/ForcePickup
                var miPickup = inv.GetType().GetMethod("Pickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (miPickup != null)
                {
                    try
                    {
                        var ps = miPickup.GetParameters();
                        if (ps.Length >= 1)
                        {
                            miPickup.Invoke(inv, new object[] { pu });
                            return true;
                        }
                    }
                    catch { }
                }
                var miForcePickup = inv.GetType().GetMethod("ForcePickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (miForcePickup != null)
                {
                    try
                    {
                        var ps = miForcePickup.GetParameters();
                        if (ps.Length >= 1)
                        {
                            miForcePickup.Invoke(inv, new object[] { pu });
                            return true;
                        }
                    }
                    catch { }
                }

                // 失败则销毁实例以免泄漏
                UnityEngine.Object.Destroy(go);
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGiveDeadToPlayer(TechType techType)
        {
            try
            {
                if (techType == TechType.None) return false;
                var inv = Inventory.main;
                if (inv == null) return false;

                GameObject prefab = null;
                try { prefab = CraftData.GetPrefabForTechType(techType); } catch { }
                GameObject go = null;
                if (prefab != null)
                    go = UnityEngine.Object.Instantiate(prefab);
                if (go == null)
                {
                    try { go = CraftData.InstantiateFromPrefab(techType); } catch { }
                }
                if (go == null) return false;

                // 标记为死亡态
                MarkDeadForInventory(go);

                var pu = go.GetComponent<Pickupable>() ?? go.AddComponent<Pickupable>();
                pu.isPickupable = true;
                var miPickup = inv.GetType().GetMethod("Pickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (miPickup != null)
                {
                    try { miPickup.Invoke(inv, new object[] { pu }); return true; } catch { }
                }
                var miForcePickup = inv.GetType().GetMethod("ForcePickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (miForcePickup != null)
                {
                    try { miForcePickup.Invoke(inv, new object[] { pu }); return true; } catch { }
                }

                UnityEngine.Object.Destroy(go);
                return false;
            }
            catch { return false; }
        }

        private static void MarkDeadForInventory(GameObject go)
        {
            if (go == null) return;
            try
            {
                // 1) 直接 Kill LiveMixin
                var lm = go.GetComponentInChildren<LiveMixin>(true);
                if (lm != null)
                {
                    try { lm.Kill(); return; } catch { }
                    try
                    {
                        var fi = typeof(LiveMixin).GetField("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                 ?? typeof(LiveMixin).GetField("Health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi != null) fi.SetValue(lm, 0f);
                    }
                    catch { }
                }

                // 2) 尝试通过 Creature 的 liveMixin Kill
                var creature = go.GetComponentInChildren<Creature>(true);
                if (creature != null && creature.liveMixin != null)
                {
                    try { creature.liveMixin.Kill(); return; } catch { }
                }
            }
            catch { }
        }
    }
}
