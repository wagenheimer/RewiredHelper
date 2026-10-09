using Rewired;

using UnityEngine;
using UnityEngine.EventSystems;

using TMPro;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Automatically shows/hides a registered <see cref="UI.RewiredOnScreenKeyboard"/> whenever a
    /// <c>TMP_InputField</c> is selected while the active input is a joystick (Steam Deck's default control
    /// scheme, Xbox/PlayStation pads, or any Rewired joystick). Touch already gets the OS's native on-screen
    /// keyboard for free, and mouse/keyboard obviously needs none, so only the joystick case needs this.
    /// No per-field setup: place one <see cref="UI.RewiredOnScreenKeyboard"/> in the scene and it just works.
    /// </summary>
    public partial class RewiredInputManager
    {
        [Tooltip("Automatically show the on-screen keyboard when a TMP_InputField is selected with a gamepad (required for Steam Deck's on-screen-keyboard certification requirement). Off has no effect unless a RewiredOnScreenKeyboard exists in the scene.")]
        public bool ShowOnScreenKeyboardOnGamepadTextInput = true;

        [Tooltip("Only show the on-screen keyboard on Steam Deck. Other gamepads (Xbox/PlayStation pads on a PC or console) usually have a system keyboard or a physical one. The Unity Editor always shows it with a gamepad (or the Play Mode device simulation), so it can be tested.")]
        public bool OnScreenKeyboardOnlyOnSteamDeck = true;

        private static readonly System.Collections.Generic.HashSet<UI.RewiredOnScreenKeyboard> _onScreenKeyboards = new();

        /// <summary>The on-screen keyboard that is showing right now, or null. Used to lift forms above it (see <see cref="UI.RewiredKeyboardAvoider"/>).</summary>
        public static UI.RewiredOnScreenKeyboard ActiveOnScreenKeyboard
        {
            get
            {
                foreach (var keyboard in _onScreenKeyboards)
                {
                    if (keyboard != null && keyboard.isActive) return keyboard;
                }
                return null;
            }
        }

        /// <summary>
        /// True only in the Unity Editor, where the keyboard is always allowed so it can be tested with a gamepad. (The Play Mode device
        /// simulation, <c>DebugForceControllerType</c>, is itself Editor-only, so it must not be referenced from code that ships in builds.)
        /// </summary>
        private static bool IsOnScreenKeyboardTestingInEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Pure rule for whether the keyboard may open on this device. The Editor and a simulated device (testing) always count.</summary>
        public static bool IsOnScreenKeyboardAllowed(bool onlyOnSteamDeck, bool isSteamDeck, bool testingInEditor) =>
            !onlyOnSteamDeck || isSteamDeck || testingInEditor;

        private GameObject _lastKeyboardSelection;

        public static void RegisterOnScreenKeyboard(UI.RewiredOnScreenKeyboard keyboard)
        {
            if (keyboard != null) _onScreenKeyboards.Add(keyboard);
        }

        public static void UnregisterOnScreenKeyboard(UI.RewiredOnScreenKeyboard keyboard)
        {
            if (keyboard != null) _onScreenKeyboards.Remove(keyboard);
        }

        /// <summary>
        /// Registers every <see cref="UI.RewiredOnScreenKeyboard"/> already in the scene, including inactive
        /// ones. The generated keyboard starts inactive (so it never runs <c>OnEnable</c> to register itself),
        /// which is why the manager must scan for it rather than rely on self-registration alone. Safe to call
        /// repeatedly: it drops entries destroyed with a previous scene and uses a set, so re-adding is a no-op.
        /// </summary>
        internal void DiscoverOnScreenKeyboards()
        {
            _onScreenKeyboards.RemoveWhere(keyboard => keyboard == null);

#pragma warning disable 0618 // FindObjectsOfType(Type, bool) is the only active+inactive overload on Unity 2021.3
            foreach (var keyboard in UnityEngine.Object.FindObjectsOfType<UI.RewiredOnScreenKeyboard>(true))
                _onScreenKeyboards.Add(keyboard);
#pragma warning restore 0618
        }

        /// <summary>True while any registered on-screen keyboard is showing.</summary>
        public static bool IsOnScreenKeyboardActive
        {
            get
            {
                foreach (var keyboard in _onScreenKeyboards)
                {
                    if (keyboard != null && keyboard.isActive) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// After CLOSE / B the field stays selected, so the selection does not change and nothing would reopen the keyboard.
        /// Pressing A (submit) on that field brings it back.
        /// </summary>
        private void TryReopenDismissedKeyboard(GameObject selected)
        {
            if (selected == null || CurrentControllerType != ControllerType.Joystick) return;

            var inputField = selected.GetComponent<TMP_InputField>();
            if (inputField == null) return;
            if (!IsOnScreenKeyboardAllowed(OnScreenKeyboardOnlyOnSteamDeck, IsSteamDeck, IsOnScreenKeyboardTestingInEditor)) return;
            if (!IsSubmitActionPressed() && !Input.GetKeyDown(KeyCode.JoystickButton0)) return;

            foreach (var keyboard in _onScreenKeyboards)
            {
                if (keyboard != null && !keyboard.isActive && keyboard.DismissedFor == inputField)
                    keyboard.ReopenFor(inputField);
            }
        }

        /// <summary>
        /// Where the keyboard is not allowed (everything except Steam Deck, unless turned off) it must never be visible, even when the scene
        /// was saved with it enabled: a phone or tablet already has its own keyboard and has no way to close this one.
        /// </summary>
        private void HideKeyboardsWhereNotAllowed()
        {
            if (_onScreenKeyboards.Count == 0) return;
            if (IsOnScreenKeyboardAllowed(OnScreenKeyboardOnlyOnSteamDeck, IsSteamDeck, IsOnScreenKeyboardTestingInEditor)) return;

            foreach (var keyboard in _onScreenKeyboards)
            {
                if (keyboard != null && keyboard.gameObject.activeSelf)
                    keyboard.SetActive(false);
            }
        }

        private void HandleOnScreenKeyboard()
        {
            HideKeyboardsWhereNotAllowed();
            if (!ShowOnScreenKeyboardOnGamepadTextInput || _onScreenKeyboards.Count == 0) return;

            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == _lastKeyboardSelection)
            {
                TryReopenDismissedKeyboard(selected);
                return;
            }

            _lastKeyboardSelection = selected;

            var inputField = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            bool wantsKeyboard = inputField != null && CurrentControllerType == ControllerType.Joystick &&
                                 IsOnScreenKeyboardAllowed(OnScreenKeyboardOnlyOnSteamDeck, IsSteamDeck, IsOnScreenKeyboardTestingInEditor);

            foreach (var keyboard in _onScreenKeyboards)
            {
                if (keyboard == null) continue;

                // The player closed the keyboard on this field: stay closed until the selection moves elsewhere.
                keyboard.ClearDismissed(inputField);
                bool dismissed = inputField != null && keyboard.DismissedFor == inputField;

                if (wantsKeyboard && !dismissed)
                {
                    if (!keyboard.isActive || keyboard.focus != inputField)
                        keyboard.SetActiveFocus(inputField);
                    else
                        keyboard.RestoreKeySelection();
                }
                else if (keyboard.isActive && !keyboard.ContainsSelection(selected))
                {
                    // Selecting one of the keyboard's own keys (D-pad / stick) must NOT count as leaving the field,
                    // otherwise the keyboard would close the moment the player tried to type.
                    keyboard.SetActive(false);
                }
            }
        }
    }
}
