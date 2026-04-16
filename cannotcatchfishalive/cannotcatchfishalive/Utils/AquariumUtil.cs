using System;
using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class AquariumUtil
    {
        public static bool IsInAquarium(GameObject go)
        {
            if (go == null) return false;
            try
            {
                var t1 = Type.GetType("Aquarium, Assembly-CSharp");
                var t2 = Type.GetType("WaterPark, Assembly-CSharp");
                if (t1 != null && go.GetComponentInParent(t1) != null) return true;
                if (t2 != null && go.GetComponentInParent(t2) != null) return true;
            }
            catch { }
            return false;
        }

        public static bool IsPlayerInAquarium()
        {
            try
            {
                var player = Player.main;
                if (player == null) return false;
                var go = player.gameObject;
                if (go == null) return false;

                // 近邻触发体判定（兼容性方案）：玩家附近是否存在带有 WaterPark 的体积/触发器
                var tWaterPark = Type.GetType("WaterPark, Assembly-CSharp");
                if (tWaterPark == null)
                    return false;

                var pos = player.transform.position;
                var hits = Physics.OverlapSphere(pos, 4.0f);
                if (hits != null)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var c = hits[i];
                        if (c == null) continue;
                        var wp = c.GetComponentInParent(tWaterPark);
                        if (wp != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
