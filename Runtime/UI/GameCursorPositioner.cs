using UnityEngine;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Applies a raw screen-space point (as reported by Rewired's PlayerMouse
    /// "On Screen Position Changed" event) to a cursor RectTransform, converting through the
    /// owning Canvas first. A direct binding of that event to
    /// <c>RectTransform.anchoredPosition</c> only lines up when the Canvas is Screen Space -
    /// Overlay with a 1:1 Canvas Scaler (reference resolution == actual resolution); any other
    /// combination (Screen Space - Camera, World Space, or Scale With Screen Size with a
    /// different reference resolution) sends the cursor to the wrong place because
    /// anchoredPosition is expressed in the canvas's local/scaled units, not raw screen pixels.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameCursorPositioner : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform cursorRectTransform;

        private void Reset()
        {
            cursorRectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        private void Awake()
        {
            if (!cursorRectTransform)
                cursorRectTransform = GetComponent<RectTransform>();

            if (!canvas)
                canvas = GetComponentInParent<Canvas>();
        }

        public void SetScreenPosition(Vector2 screenPosition)
        {
            if (!canvas || !cursorRectTransform) return;

            var canvasRect = (RectTransform)canvas.transform;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out var localPoint))
                cursorRectTransform.localPosition = localPoint;
        }
    }
}
