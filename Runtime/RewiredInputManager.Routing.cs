using Rewired;

using UnityEngine;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>Escape/Return routing and the submit-to-pointer bridge.</summary>
    public partial class RewiredInputManager
    {
        /// <summary>Routes Escape/Return to the host's modal stack when present, otherwise to the generic EscapeButton/ReturnEscapeEvent components.</summary>
        private void HandleEscapeButtons()
        {
            if (_uiBlocker.IsUiBlocked) return;

            _modalStack.PruneInactiveTop();

            bool escapePressed = Input.GetKeyDown(KeyCode.Escape);
            if (Player != null)
            {
                escapePressed |= Player.GetButtonDown("MenuButton") || Player.GetButtonDown("BackButton");
            }

            if (escapePressed)
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopEscapeButton(out var escapeButton) &&
                    escapeButton != null && escapeButton.interactable && escapeButton.gameObject.activeSelf)
                {
                    escapeButton.onClick.Invoke();
                }
                else if (!EscapeButton.PressedScape())
                {
                    ReturnEscapeEvent.TriggerEscape();
                }
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopOkButton(out var okButton) &&
                    okButton != null && okButton.interactable && okButton.gameObject.activeSelf)
                {
                    okButton.onClick.Invoke();
                }
                else
                {
                    ReturnEscapeEvent.TriggerOk();
                }
            }

            if (AutoBridgeSubmitToPointerDown)
            {
                HandleSubmitPointerBridge();
            }
        }

        /// <summary>
        /// When joystick/keyboard confirmation (UISubmit / Submit / Button A / Return) is pressed on a selected UI element,
        /// automatically dispatches PointerDown/PointerUp/PointerClick so audio listeners (like EventSounds / PointerDown SFX) trigger naturally.
        /// </summary>
        private void HandleSubmitPointerBridge()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;

            var selected = es.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return;

            bool submitPressed = false;
            if (Player != null)
            {
                submitPressed = Player.GetButtonDown("UISubmit") || Player.GetButtonDown("Submit");
            }
            if (!submitPressed)
            {
                submitPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0);
            }

            if (!submitPressed) return;

            var eventData = new UnityEngine.EventSystems.PointerEventData(es)
            {
                selectedObject = selected,
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };

            // 1. Dispatch pointerDown / pointerUp / pointerClick hierarchy (upwards)
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(selected, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(selected, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(selected, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);

            // 2. Also dispatch to any child handlers (e.g. EventSounds components attached to child objects)
            var pointerDownChildren = selected.GetComponentsInChildren<UnityEngine.EventSystems.IPointerDownHandler>();
            foreach (var handler in pointerDownChildren)
            {
                if (handler is Component c && c.gameObject != selected)
                {
                    handler.OnPointerDown(eventData);
                }
            }

            var pointerClickChildren = selected.GetComponentsInChildren<UnityEngine.EventSystems.IPointerClickHandler>();
            foreach (var handler in pointerClickChildren)
            {
                if (handler is Component c && c.gameObject != selected)
                {
                    handler.OnPointerClick(eventData);
                }
            }
        }
    }
}
