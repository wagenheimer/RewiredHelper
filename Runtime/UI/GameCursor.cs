using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Software ("hand") cursor drawn as a UI <see cref="Image"/> that follows the mouse on desktop builds.
    /// <list type="bullet">
    /// <item>Hidden until the mouse actually moves, and hidden again after every scene change.</item>
    /// <item>While a gamepad drives the pointer (<see cref="RewiredInputManager.IsUsingJoystickCursor"/>)
    /// it stays visible and <see cref="GameCursorPositioner"/> moves it instead.</item>
    /// <item>Deactivates itself on mobile, where there is no pointer.</item>
    /// </list>
    /// Put it under the game's main Canvas; the Canvas is found on the parents when not assigned.
    /// </summary>
    [AddComponentMenu("Wagenheimer/Rewired/Game Cursor (Software Cursor)")]
    [DisallowMultipleComponent]
    public class GameCursor : MonoBehaviour
    {
        [Tooltip("The hand / arrow graphic. Found on this object or its children when empty.")]
        [FormerlySerializedAs("Image")]
        public Image image;

        [Tooltip("Canvas used to convert the mouse position. Found on the parents when empty " +
                 "(the cursor's own nested sorting Canvas is skipped).")]
        public Canvas canvas;

        [Tooltip("Z offset applied after conversion so the cursor draws in front of the UI.")]
        public float depth = -200f;

#if UNITY_STANDALONE
        private Vector2 lastMousePosition;
        private bool hasLastMousePosition;

        private void Awake()
        {
            if (image == null) image = GetComponentInChildren<Image>(true);
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;

        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        private void Start()
        {
            // The pointer appears with the first mouse movement.
            if (image != null) image.enabled = false;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (image != null) image.enabled = false;
        }

        private void Update()
        {
            if (image == null) return;

            // With a gamepad the Rewired Helper drives this hand through GameCursorPositioner. The OS cursor
            // is hidden in that mode, so hiding the hand would leave no visible pointer at all.
            var manager = RewiredInputManager.Instance;
            if (manager != null && manager.IsUsingJoystickCursor)
            {
                if (!image.enabled) image.enabled = true;
                return;
            }

            var target = ResolveCanvas();
            if (target == null) return;

            var mouse = (Vector2)Input.mousePosition;
            var moved = hasLastMousePosition && (mouse - lastMousePosition).sqrMagnitude > 0.01f;
            lastMousePosition = mouse;
            hasLastMousePosition = true;

            if (moved && !image.enabled)
            {
                image.rectTransform.SetAsLastSibling();
                image.enabled = true;
            }

            var canvasRect = (RectTransform)target.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, target.worldCamera, out var local);

            var world = canvasRect.TransformPoint(local);
            world.z += depth;
            transform.position = world;
        }

        private Canvas ResolveCanvas()
        {
            if (canvas != null) return canvas;

            var parent = transform.parent;
            if (parent != null) canvas = parent.GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            return canvas;
        }
#else
        // No pointer on touch platforms.
        private void Awake() => gameObject.SetActive(false);
#endif
    }
}
