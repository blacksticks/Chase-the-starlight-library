using System.IO;
using QuestBook.Models;

namespace QuestBook.Config
{
    internal static class QuestRepository
    {
        internal static QuestBookData Current { get; private set; }

        internal static void Load()
        {
            QuestBookData data;
            if (File.Exists(Paths.ConfigPath) && QuestConfigLoader.TryLoad(Paths.ConfigPath, out data))
            {
                Current = data;
                Mod.Log?.LogInfo($"QuestBook config loaded: {Paths.ConfigPath}");
                return;
            }
            Current = new QuestBookData();
            Mod.Log?.LogWarning("QuestBook config not found at fixed path. Using empty dataset.");
        }
    }
}
