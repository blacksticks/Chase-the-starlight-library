using System;
using UnityEngine;

namespace CannotCatchFishAlive.Utils
{
    internal static class PrefabUtil
    {
        // 通过反射方式确保添加 PrefabIdentifier（避免直接引用类型导致编译器缺少引用报错）
        public static void TryEnsurePrefabIdentifier(GameObject go, string classId)
        {
            if (go == null) return;
            try
            {
                // 尝试在两个常见位置解析类型
                var type = Type.GetType("UWE.PrefabIdentifier, Assembly-CSharp-firstpass")
                           ?? Type.GetType("UWE.PrefabIdentifier, Assembly-CSharp")
                           ?? Type.GetType("PrefabIdentifier, Assembly-CSharp-firstpass")
                           ?? Type.GetType("PrefabIdentifier, Assembly-CSharp");

                if (type == null)
                    return; // 无法解析则跳过（非致命）

                var existing = go.GetComponent(type);
                if (existing == null)
                {
                    existing = go.AddComponent(type);
                }

                // 设置 ClassId 属性（若存在）
                var prop = type.GetProperty("ClassId");
                if (prop != null && prop.CanWrite)
                {
                    var current = prop.GetValue(existing) as string;
                    if (string.IsNullOrEmpty(current))
                        prop.SetValue(existing, classId);
                }
            }
            catch
            {
                // 忽略异常，保持稳健
            }
        }

        // 通过反射读取 PrefabIdentifier.ClassId
        public static string GetPrefabIdentifierClassId(GameObject go)
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
            catch
            {
                return null;
            }
        }
    }
}
