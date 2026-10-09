using System;
using System.Collections.Generic;

using UnityEngine;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// While the on-screen keyboard is open, the gamepad must navigate the keys (D-pad / stick / A) and nothing else. The joystick
    /// driven virtual cursor (<see cref="GameCursor"/> + Rewired's <c>PlayerMouse</c>) uses the same stick and the same A button, so it
    /// moved a pointer around behind the keyboard, clicked keys a second time and fought the selection. The cursor is hidden and the
    /// PlayerMouse components are switched off until the keyboard closes.
    /// </summary>
    public partial class RewiredInputManager
    {
        private const string PlayerMouseTypeName = "Rewired.Components.PlayerMouse";

        [Tooltip("Hide the joystick cursor and pause Rewired's PlayerMouse while the on-screen keyboard is open, so the gamepad only navigates the keyboard.")]
        public bool HideCursorWhileOnScreenKeyboardOpen = true;

        private static Type _playerMouseType;
        private readonly List<Behaviour> _suspendedPlayerMice = new List<Behaviour>();
        private bool _cursorSuspendedByKeyboard;

        /// <summary>Call every frame after the cursor/controller handling, so it wins over code that re-enables the cursor.</summary>
        private void SuspendCursorWhileOnScreenKeyboard()
        {
            var keyboardOpen = HideCursorWhileOnScreenKeyboardOpen && ActiveOnScreenKeyboard != null;

            if (keyboardOpen)
            {
                if (!_cursorSuspendedByKeyboard) BeginCursorSuspension();

                // Other code (HandleJoystickOrCustomController) turns the cursor back on every frame the joystick is active.
                if (GameCursor != null && GameCursor.enabled) GameCursor.enabled = false;
                Cursor.visible = false;
            }
            else if (_cursorSuspendedByKeyboard)
            {
                EndCursorSuspension();
            }
        }

        private void BeginCursorSuspension()
        {
            _cursorSuspendedByKeyboard = true;
            _suspendedPlayerMice.Clear();

            foreach (var mouse in FindPlayerMice())
            {
                if (mouse == null || !mouse.enabled) continue;

                mouse.enabled = false;
                _suspendedPlayerMice.Add(mouse);
            }

            ActiveOnScreenKeyboard?.DebugLog($"cursor suspended ({_suspendedPlayerMice.Count} PlayerMouse disabled)");
        }

        private void EndCursorSuspension()
        {
            _cursorSuspendedByKeyboard = false;

            foreach (var mouse in _suspendedPlayerMice)
            {
                if (mouse != null) mouse.enabled = true;
            }

            _suspendedPlayerMice.Clear();
            // GameCursor.enabled is restored by HandleJoystickOrCustomController as soon as the joystick is used again.
        }

        private static Behaviour[] FindPlayerMice()
        {
            _playerMouseType ??= FindTypeByName(PlayerMouseTypeName);
            if (_playerMouseType == null) return Array.Empty<Behaviour>();

#if UNITY_2023_1_OR_NEWER
            var found = FindObjectsByType(_playerMouseType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
#pragma warning disable 0618
            var found = FindObjectsOfType(_playerMouseType);
#pragma warning restore 0618
#endif
            var mice = new List<Behaviour>(found.Length);
            foreach (var item in found)
            {
                if (item is Behaviour behaviour) mice.Add(behaviour);
            }

            return mice.ToArray();
        }

        private static Type FindTypeByName(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
