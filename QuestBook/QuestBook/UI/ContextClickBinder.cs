using UnityEngine;
using UnityEngine.EventSystems;
using QuestBook.UI;

namespace QuestBook.UI
{
    internal class ContextClickBinder : MonoBehaviour, IPointerClickHandler
    {
        public MenuKind Kind;
        public string ChapterId;
        public string NodeId;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right)
                return;

            if (!DeveloperModeManager.IsDeveloperMode)
            {
                Mod.Log?.LogInfo($"ContextClick ignored (dev mode off): kind={Kind}, chapter={ChapterId}, node={NodeId}");
                ContextMenuController.Hide();
                return;
            }
            // 若中心化 RightClickDebug 正在运行，则交由中心统一分发，避免松开右键时又触发空白菜单覆盖
            var central = UnityEngine.Object.FindObjectOfType<RightClickDebug>();
            if (central != null && central.isActiveAndEnabled)
            {
                Mod.Log?.LogInfo("ContextClickBinder: central RightClickDebug active -> skip direct ShowAt");
                return;
            }
            // 兜底：中心不可用时，仍按旧逻辑直接弹出
            var pos = Input.mousePosition;
            Mod.Log?.LogInfo($"ContextClick on {Kind} at {pos}: chapter={ChapterId}, node={NodeId}");
            ContextMenuController.ShowAt(pos, Kind, ChapterId, NodeId);
        }
    }
}
