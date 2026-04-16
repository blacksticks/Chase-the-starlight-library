using UnityEngine;

namespace QuestBook.UI
{
    internal class CursorGuard : MonoBehaviour
    {
        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (!UIManager.IsOpen)
                return;

            if (!Cursor.visible)
                Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
        }
    }
}
