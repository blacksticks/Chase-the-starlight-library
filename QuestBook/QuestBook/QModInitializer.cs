using QModManager.API.ModLoading;

namespace QuestBook
{
    [QModCore]
    public static class QModInitializer
    {
        [QModPatch]
        public static void Patch()
        {
            Mod.Initialize();
            Mod.Log?.LogInfo("QuestBook (QMod) patched");
        }
    }
}
