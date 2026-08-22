using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Fullscreen transparent Image (raycastTarget=true) on a topmost overlay canvas.
    /// When Enabled, it swallows all pointer input so nothing underneath is clickable.
    /// </summary>
    public static class UIBlockerOverlay
    {
        private const int TopmostSortingOrder = 32767;

        private static GameObject _overlay;
        private static bool _enabled;

        public static bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (value) EnsureOverlay().SetActive(true);
                else if (_overlay != null) _overlay.SetActive(false);
            }
        }

        private static GameObject EnsureOverlay()
        {
            if (_overlay != null) return _overlay;

            var canvasGo = new GameObject("UIBlockerOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TopmostSortingOrder;
            Object.DontDestroyOnLoad(canvasGo);

            var imageGo = new GameObject("Blocker", typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // fully transparent
            image.raycastTarget = true; // swallows all clicks
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            _overlay = canvasGo;
            return _overlay;
        }
    }
}
