using QModManager.API.ModLoading;

namespace CannotCatchFishAlive
{
    [QModCore]
    public static class ModEntry
    {
        [QModPatch]
        public static void Patch()
        {
            Bootstrap.Init();
        }
    }
}
