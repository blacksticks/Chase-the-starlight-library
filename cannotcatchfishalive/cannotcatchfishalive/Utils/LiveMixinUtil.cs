using System;
using System.Reflection;

namespace CannotCatchFishAlive.Utils
{
    internal static class LiveMixinUtil
    {
        private static MethodInfo _miIsAlive;
        private static PropertyInfo _piIsAlive;
        private static FieldInfo _fiIsAlive;
        private static PropertyInfo _piHealth;
        private static FieldInfo _fiHealth;
        private static bool _inited;

        private static void EnsureInit()
        {
            if (_inited) return;
            _inited = true;
            var t = typeof(LiveMixin);
            _miIsAlive = t.GetMethod("IsAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            _piIsAlive = t.GetProperty("IsAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? t.GetProperty("isAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _fiIsAlive = t.GetField("IsAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? t.GetField("isAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _piHealth = t.GetProperty("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? t.GetProperty("Health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _fiHealth = t.GetField("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? t.GetField("Health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static bool IsAliveSafe(LiveMixin lm)
        {
            if (lm == null) return false;
            EnsureInit();
            try
            {
                if (_miIsAlive != null)
                {
                    var obj = _miIsAlive.Invoke(lm, null);
                    if (obj is bool b1) return b1;
                }
                if (_piIsAlive != null)
                {
                    var obj = _piIsAlive.GetValue(lm);
                    if (obj is bool b2) return b2;
                }
                if (_fiIsAlive != null)
                {
                    var obj = _fiIsAlive.GetValue(lm);
                    if (obj is bool b3) return b3;
                }
            }
            catch { }
            // 若无法确定，依据 health > 0 近似判断
            float h;
            if (TryGetHealth(lm, out h))
                return h > 0f;
            // 无法确定，返回 false（不要阻止后续判定）
            return false;
        }

        public static bool IsDeadNow(LiveMixin lm)
        {
            if (lm == null) return false;
            EnsureInit();
            try
            {
                if (_miIsAlive != null)
                {
                    var obj = _miIsAlive.Invoke(lm, null);
                    if (obj is bool b1) return !b1;
                }
                if (_piIsAlive != null)
                {
                    var obj = _piIsAlive.GetValue(lm);
                    if (obj is bool b2) return !b2;
                }
                if (_fiIsAlive != null)
                {
                    var obj = _fiIsAlive.GetValue(lm);
                    if (obj is bool b3) return !b3;
                }
            }
            catch { }
            float h;
            if (TryGetHealth(lm, out h))
                return h <= 0f;
            return false;
        }

        private static bool TryGetHealth(LiveMixin lm, out float health)
        {
            health = 0f;
            try
            {
                if (_piHealth != null)
                {
                    var obj = _piHealth.GetValue(lm);
                    if (obj is float f1) { health = f1; return true; }
                }
                if (_fiHealth != null)
                {
                    var obj = _fiHealth.GetValue(lm);
                    if (obj is float f2) { health = f2; return true; }
                }
            }
            catch { }
            return false;
        }
    }
}
