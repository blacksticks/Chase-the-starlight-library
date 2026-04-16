using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuestBook.UI
{
    internal class RightClickDebug : MonoBehaviour
    {
        private PointerEventData _ped;
        private List<RaycastResult> _hits = new List<RaycastResult>(16);

        private void Awake()
        {
            if (EventSystem.current == null)
            {
                Mod.Log?.LogWarning("RightClickDebug: No EventSystem found.");
            }
        }

        private void Update()
        {
            if (!UIManager.IsOpen) return;
            // 仅在开发者模式下允许弹出右键菜单；非开发者模式下若右键点击则隐藏已有菜单
            if (!DeveloperModeManager.IsDeveloperMode)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    ContextMenuController.Hide();
                    Mod.Log?.LogInfo("RightClickDebug: dev mode off, hide menu");
                }
                return;
            }
            if (!Input.GetMouseButtonDown(1)) return;
            if (EventSystem.current == null)
            {
                Mod.Log?.LogWarning("RightClickDebug: EventSystem.current is null on right click.");
                return;
            }

            if (_ped == null) _ped = new PointerEventData(EventSystem.current);
            _ped.Reset();
            _ped.position = Input.mousePosition;

            _hits.Clear();
            EventSystem.current.RaycastAll(_ped, _hits);
            Mod.Log?.LogInfo($"RightClickDebug: hits={_hits.Count} at={_ped.position}");

            // 优先命中节点菜单：若同一位置既命中节点又命中空白，优先节点
            ContextClickBinder bestNode = null;
            ContextClickBinder firstAny = null;
            for (int i = 0; i < _hits.Count; i++)
            {
                var go = _hits[i].gameObject;
                if (go == null) continue;
                var binder = go.GetComponentInParent<ContextClickBinder>();
                if (binder == null) continue;
                if (firstAny == null) firstAny = binder;
                if (binder.Kind == MenuKind.NodeItem) { bestNode = binder; break; }
            }
            if (bestNode != null)
            {
                Mod.Log?.LogInfo($"RightClickDebug: prefer NodeItem -> go={bestNode.gameObject.name}");
                ContextMenuController.ShowAt(_ped.position, bestNode.Kind, bestNode.ChapterId, bestNode.NodeId);
                return;
            }
            if (firstAny != null)
            {
                Mod.Log?.LogInfo($"RightClickDebug: fallback first binder -> kind={firstAny.Kind}");
                ContextMenuController.ShowAt(_ped.position, firstAny.Kind, firstAny.ChapterId, firstAny.NodeId);
                return;
            }

            // 范围兜底：若未命中显式 binder，则按所属区域决定
            var chapters = UIManager.GetChaptersPanelTransform();
            var nodesContainer = UIManager.GetNodesContainerTransform(); // GraphPanel/Viewport/ContentRoot/NodesContainer
            var viewport = UIManager.GetGraphViewportTransform(); // GraphPanel/Viewport
            bool inChapters = false, inNodes = false;
            for (int i = 0; i < _hits.Count; i++)
            {
                var t = _hits[i].gameObject != null ? _hits[i].gameObject.transform : null;
                if (t == null) continue;
                if (!inChapters && chapters != null && t.IsChildOf(chapters)) inChapters = true;
                if (!inNodes && nodesContainer != null && t.IsChildOf(nodesContainer)) inNodes = true;
            }
            if (inNodes)
            {
                Mod.Log?.LogInfo("RightClickDebug: scoped fallback -> GraphBlank");
                ContextMenuController.ShowAt(_ped.position, MenuKind.GraphBlank, UIManager.GetCurrentChapterId(), null);
                return;
            }
            // 其次：若点击位于 Viewport 可视区域内，也视为详情页空白
            var vpRt = viewport as RectTransform;
            if (vpRt != null && RectTransformUtility.RectangleContainsScreenPoint(vpRt, _ped.position, null))
            {
                Mod.Log?.LogInfo("RightClickDebug: viewport containment -> GraphBlank");
                ContextMenuController.ShowAt(_ped.position, MenuKind.GraphBlank, UIManager.GetCurrentChapterId(), null);
                return;
            }
            if (inChapters)
            {
                Mod.Log?.LogInfo("RightClickDebug: scoped fallback -> ChaptersBlank");
                ContextMenuController.ShowAt(_ped.position, MenuKind.ChaptersBlank, null, null);
                return;
            }

            // UI 外：隐藏
            Mod.Log?.LogInfo("RightClickDebug: no ui scope, hide menu");
            ContextMenuController.Hide();
            return;
        }
    }
}
