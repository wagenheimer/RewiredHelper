using UnityEngine;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Applies a raw screen-space point (as reported by Rewired's PlayerMouse
    /// "On Screen Position Changed" event) to a cursor RectTransform.
    ///
    /// The point is converted into the local space of the cursor's <b>parent</b>, which is what
    /// <c>RectTransform.localPosition</c> expects. Converting against the cursor's own rect (or
    /// against its own nested sorting Canvas) sends the cursor to the wrong place as soon as the
    /// canvas is scaled — a Scale With Screen Size canvas is scaled, so raw pixels are never the
    /// same unit as local positions there.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameCursorPositioner : MonoBehaviour
    {
        [Tooltip("Canvas used to convert screen points. Found automatically on the parents; the " +
                 "cursor's own nested Canvas (sorting) is never used.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("RectTransform that is moved. Defaults to this object.")]
        [SerializeField] private RectTransform cursorRectTransform;

        [Tooltip("Keeps the cursor image centred on the pointer instead of using its pivot as hot spot.")]
        [SerializeField] private bool centerOnPointer = true;

        [Tooltip("Keeps the cursor inside the canvas so it never disappears at the screen edges.")]
        [SerializeField] private bool clampToParent = true;

        private RectTransform _targetRect;

        private void Reset()
        {
            cursorRectTransform = GetComponent<RectTransform>();
            canvas = FindParentCanvas();
        }

        private void Awake()
        {
            if (cursorRectTransform == null)
                cursorRectTransform = GetComponent<RectTransform>();

            EnsureSortingCanvas();

            canvas = FindParentCanvas();
            _targetRect = cursorRectTransform != null ? cursorRectTransform.parent as RectTransform : null;
        }

        private void OnEnable()
        {
            // Safe areas, dialogs and canvas rebuilds can reparent the cursor at runtime.
            canvas = FindParentCanvas();
            _targetRect = cursorRectTransform != null
                ? cursorRectTransform.parent as RectTransform
                : null;
        }

        /// <summary>
        /// Walks up from the parent, so the cursor's own sorting Canvas is never picked.
        /// </summary>
        private Canvas FindParentCanvas()
        {
            var current = transform.parent;

            while (current != null)
            {
                var found = current.GetComponent<Canvas>();
                if (found != null) return found;
                current = current.parent;
            }

            return null;
        }

        private void EnsureSortingCanvas()
        {
            var sortingCanvas = GetComponent<Canvas>();
            if (sortingCanvas == null)
                sortingCanvas = gameObject.AddComponent<Canvas>();

            sortingCanvas.overrideSorting = true;

            if (sortingCanvas.sortingOrder < 1000)
                sortingCanvas.sortingOrder = 1000;
        }

        private Canvas RootCanvas =>
            canvas != null ? canvas.rootCanvas : null;

        /// <summary>
        /// Screen-space point (pixels, origin at bottom-left) to local position of the cursor.
        /// Safe to call from the editor or from a UnityEvent.
        /// </summary>
        public void SetScreenPosition(Vector2 screenPosition)
        {
            if (cursorRectTransform == null)
                cursorRectTransform = GetComponent<RectTransform>();
            if (cursorRectTransform == null) return;

            if (_targetRect == null)
                _targetRect = cursorRectTransform.parent as RectTransform;
            if (_targetRect == null) return;

            if (canvas == null)
                canvas = FindParentCanvas();

            var root = RootCanvas;
            var camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                ? root.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _targetRect, screenPosition, camera, out var localPoint))
                return;

            if (centerOnPointer)
            {
                // localPosition places the pivot, so shift by the distance between the pivot and
                // the centre of the image (top-left pivots would otherwise sit one half-image off).
                var rect = cursorRectTransform.rect;
                var scale = cursorRectTransform.localScale;

                localPoint.x += (0.5f - cursorRectTransform.pivot.x) * rect.width * scale.x;
                localPoint.y += (0.5f - cursorRectTransform.pivot.y) * rect.height * scale.y;
            }

            if (clampToParent)
            {
                var bounds = _targetRect.rect;
                localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin, bounds.xMax);
                localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin, bounds.yMax);
            }

            cursorRectTransform.localPosition = localPoint;
        }

        /// <summary>
        /// Projects the cursor back to screen pixels (used by RewiredInputManager).
        /// </summary>
        public Vector2 GetScreenPosition()
        {
            if (cursorRectTransform == null) return Vector2.zero;

            var root = RootCanvas;
            var camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                ? root.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(camera, cursorRectTransform.position);
        }
    }
}
