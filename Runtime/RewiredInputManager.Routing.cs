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
                TriggerReturnConfirm();
            }

            if (AutoBridgeSubmitToPointerDown)
            {
                HandleSubmitPointerBridge();
            }
        }

        /// <summary>
        /// The same "Return was pressed" routing <see cref="HandleEscapeButtons"/> runs for a physical Return
        /// key: invokes the top modal dialog's <see cref="UI.Dialog.OkButton"/> if one is active, otherwise
        /// raises <see cref="ReturnEscapeEvent.TriggerOk"/>. Exposed so other confirm sources — the on-screen
        /// keyboard's "OK" key, in particular — can trigger the exact same dialog-confirm behavior a real
        /// Enter key press would, instead of only submitting the focused TMP_InputField.
        /// </summary>
        public void TriggerReturnConfirm()
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

        private static readonly string[] SubmitActionNames = { "UISubmit", "Submit" };

        // Action ids resolved once. Asking Rewired for a name that does not exist (Player.GetButtonDown("UISubmit"))
        // logs "The Action ... does not exist" on every call, i.e. every frame in a project without UI actions.
        private int[] _submitActionIds;

        private bool IsSubmitActionPressed()
        {
            if (Player == null || !ReInput.isReady) return false;

            if (_submitActionIds == null)
            {
                var ids = new System.Collections.Generic.List<int>();
                foreach (var name in SubmitActionNames)
                {
                    var id = ReInput.mapping.GetActionId(name);
                    if (id >= 0) ids.Add(id);
                }

                _submitActionIds = ids.ToArray();
            }

            foreach (var id in _submitActionIds)
            {
                if (Player.GetButtonDown(id)) return true;
            }

            return false;
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

            bool submitPressed = IsSubmitActionPressed();
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
