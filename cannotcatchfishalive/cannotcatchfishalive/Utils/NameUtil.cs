using System;
using System.Reflection;

namespace CannotCatchFishAlive.Utils
{
    internal static class NameUtil
    {
        private static MethodInfo _miGetByTechType;
        private static MethodInfo _miGetByString;
        private static bool _inited;

        private static void EnsureInit()
        {
            if (_inited) return;
            _inited = true;
            var t = typeof(Language);
            try { _miGetByTechType = t.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(TechType) }, null); } catch { }
            try { _miGetByString = t.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null); } catch { }
        }

        public static string GetTechTypeName(TechType techType)
        {
            EnsureInit();
            var lang = Language.main;
            if (lang != null && _miGetByTechType != null)
            {
                try
                {
                    var obj = _miGetByTechType.Invoke(lang, new object[] { techType });
                    if (obj is string s && !string.IsNullOrEmpty(s)) return s;
                }
                catch { }
            }
            if (lang != null && _miGetByString != null)
            {
                try
                {
                    var key = techType.ToString();
                    var obj = _miGetByString.Invoke(lang, new object[] { key });
                    if (obj is string s && !string.IsNullOrEmpty(s)) return s;
                }
                catch { }
            }
            return techType.ToString();
        }
    }
}
