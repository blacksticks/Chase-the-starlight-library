using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using QuestBook.Models;

namespace QuestBook.Config
{
    internal static class UserDataRepository
    {
        internal static UserData Load()
        {
            try
            {
                var path = Paths.UserDataPath;
                if (!File.Exists(path))
                {
                    return new UserData();
                }
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length == 0) return new UserData();
                    var ser = new XmlSerializer(typeof(UserData));
                    var obj = ser.Deserialize(fs) as UserData;
                    return obj ?? new UserData();
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"UserData load failed: {e.Message}");
                return new UserData();
            }
        }

        internal static void Save(UserData data)
        {
            if (data == null) return;
            try
            {
                var dir = Paths.DataDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var fs = File.Create(Paths.UserDataPath))
                {
                    var ser = new XmlSerializer(typeof(UserData));
                    ser.Serialize(fs, data);
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogWarning($"UserData save failed: {e.Message}");
            }
        }
    }
}
