using System;
using System.Reflection;
using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class DetectionUtil
    {
        public static bool IsLiveFish(GameObject go)
        {
            if (go == null) return false;
            // 必须可拾取
            var p = go.GetComponent<Pickupable>();
            if (p == null) return false;
            // 必须为生物（Creature）
            var c = go.GetComponentInParent<Creature>();
            if (c == null) return false;
            // 必须存在 LiveMixin 且为存活
            var lm = go.GetComponentInParent<LiveMixin>();
            if (lm == null) return false;
            if (!LiveMixinUtil.IsAliveSafe(lm)) return false;
            return true;
        }

        public static string GetTechTypeName(GameObject go)
        {
            var p = go == null ? null : go.GetComponent<Pickupable>();
            if (p == null) return null;
            var t = p.GetTechType();
            return t.ToString();
        }

        public static string GetClassId(GameObject go)
        {
            if (go == null) return null;
            try
            {
                var type = Type.GetType("UWE.PrefabIdentifier, Assembly-CSharp-firstpass")
                           ?? Type.GetType("UWE.PrefabIdentifier, Assembly-CSharp")
                           ?? Type.GetType("PrefabIdentifier, Assembly-CSharp-firstpass")
                           ?? Type.GetType("PrefabIdentifier, Assembly-CSharp");
                if (type == null) return null;
                var comp = go.GetComponent(type);
                if (comp == null) return null;
                var prop = type.GetProperty("ClassId");
                if (prop == null) return null;
                return prop.GetValue(comp) as string;
            }
            catch { return null; }
        }
    }
}
