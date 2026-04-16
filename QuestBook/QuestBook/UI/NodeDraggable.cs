using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuestBook.UI
{
    internal class NodeDraggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rt;
        private RectTransform _nodesContainer;
        private bool _pressed;
        private bool _dragging;
        private float _pressTime;
        private Coroutine _pressCo;
        private Vector2 _offsetLocal;
        private const float LongPressThreshold = 0.25f; // 长按阈值（秒）
        private const float DragUpdateInterval = 0f;     // 0 表示每帧更新

        private string _chapterId;
        private string _nodeId;

        internal void Initialize(string chapterId, string nodeId)
        {
            _chapterId = chapterId;
            _nodeId = nodeId;
            EnsureRefs();
        }

        private void OnEnable()
        {
            EnsureRefs();
        }

        private void EnsureRefs()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_nodesContainer == null)
            {
                var t = QuestBook.UIManager.GetNodesContainerTransform();
                _nodesContainer = t as RectTransform;
                if (_nodesContainer == null && _rt != null) _nodesContainer = _rt.parent as RectTransform;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!DeveloperModeManager.IsDeveloperMode) return;
            EnsureRefs();
            _pressed = true;
            _dragging = false;
            _pressTime = Time.unscaledTime;
            if (_pressCo != null) StopCoroutine(_pressCo);
            _pressCo = StartCoroutine(LongPressChecker());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pressCo != null) { StopCoroutine(_pressCo); _pressCo = null; }
            _pressed = false;
            if (_dragging)
            {
                _dragging = false;
                // 保存最终位置
                if (_rt != null)
                {
                    QuestBook.UIManager.OnNodeDragged(_chapterId, _nodeId, _rt.anchoredPosition);
                }
            }
        }

        private IEnumerator LongPressChecker()
        {
            while (_pressed && Time.unscaledTime - _pressTime < LongPressThreshold)
                yield return null;
            if (_pressed) BeginDrag();
        }

        private void BeginDrag()
        {
            if (_dragging) return;
            if (_nodesContainer == null || _rt == null) return;
            _dragging = true;
            // 计算指针相对容器的局部坐标与节点锚点的偏移，保证拖动过程中指针位置与节点相对位置一致
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_nodesContainer, Input.mousePosition, null, out local))
            {
                _offsetLocal = _rt.anchoredPosition - local;
            }
            StartCoroutine(DragLoop());
        }

        private IEnumerator DragLoop()
        {
            var wait = DragUpdateInterval > 0f ? new WaitForSeconds(DragUpdateInterval) : null;
            while (_dragging)
            {
                if (_nodesContainer == null || _rt == null) break;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_nodesContainer, Input.mousePosition, null, out local))
                {
                    var target = local + _offsetLocal;
                    _rt.anchoredPosition = target;
                    // 实时刷新连线
                    QuestBook.UIManager.RefreshEdges();
                }
                if (wait != null) yield return wait; else yield return null;
            }
        }

        private void OnDisable()
        {
            if (_pressCo != null) { StopCoroutine(_pressCo); _pressCo = null; }
            _pressed = false; _dragging = false;
        }
    }
}
