using BepInEx;

namespace QuestBook
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Mod.Initialize();
            Mod.Log?.LogInfo("QuestBook (BepInEx) loaded");
            UIManager.EnsureLoaded();
        }

        private void OnDestroy()
        {
            Mod.Log?.LogInfo("QuestBook (BepInEx) unloaded");
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
            {
                UIManager.Toggle();
            }
        }
    }
}
