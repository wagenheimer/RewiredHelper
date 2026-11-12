using System.Linq;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Scrolls a <see cref="ScrollRect"/> with the gamepad: right stick and LT/RT.
    /// <para>
    /// The axes are read straight from the connected joystick by element name, so no Rewired actions
    /// have to be created. Works on vertical lists (stick up/down, RT scrolls down) and on horizontal
    /// lists (stick left/right, RT goes right). The mouse wheel and dragging keep working as before.
    /// </para>
    /// </summary>
    [AddComponentMenu("Wagenheimer/Rewired/Gamepad Scroll")]
    [DisallowMultipleComponent]
    public class GamepadScroll : MonoBehaviour
    {
        [Tooltip("The list to scroll. Found on this object when empty.")]
        public ScrollRect Scroll;

        [Header("Speed (normalized position per second)")]
        [Min(0f)] public float StickSpeed = 1.8f;
        [Min(0f)] public float TriggerSpeed = 1.4f;

        [Header("Input")]
        [Tooltip("Invert the right stick axis.")]
        public bool InvertStick;

        [Range(0f, 0.9f)] public float StickDeadZone = 0.2f;
        [Range(0f, 0.9f)] public float TriggerDeadZone = 0.1f;

        private Joystick joystick;
        private int rightStickX = -1;
        private int rightStickY = -1;
        private int leftTrigger = -1;
        private int rightTrigger = -1;

        private void Reset() => Scroll = GetComponent<ScrollRect>();

        private void OnEnable() => ResolveJoystick();

        private Joystick CurrentJoystick
        {
            get
            {
                if (joystick == null || !joystick.enabled) ResolveJoystick();
                return joystick;
            }
        }

        private void ResolveJoystick()
        {
            joystick = ReInput.isReady ? ReInput.controllers.Joysticks.FirstOrDefault(j => j.enabled) : null;
            rightStickX = rightStickY = leftTrigger = rightTrigger = -1;

            if (joystick == null) return;

            for (var i = 0; i < joystick.axisCount; i++)
            {
                switch (joystick.AxisElementIdentifiers[i].name)
                {
                    case "Right Stick X": rightStickX = i; break;
                    case "Right Stick Y": rightStickY = i; break;
                    case "Left Trigger": leftTrigger = i; break;
                    case "Right Trigger": rightTrigger = i; break;
                }
            }
        }

        private void Update()
        {
            if (Scroll == null)
            {
                Scroll = GetComponent<ScrollRect>();
                if (Scroll == null) return;
            }

            if (Scroll.content == null || Scroll.viewport == null) return;
            if (!Scroll.vertical && !Scroll.horizontal) return;

            var pad = CurrentJoystick;
            float stickX = 0f, stickY = 0f, lt = 0f, rt = 0f;

            if (pad != null)
            {
                if (rightStickX >= 0) stickX = pad.GetAxis(rightStickX);
                if (rightStickY >= 0) stickY = pad.GetAxis(rightStickY);
                if (leftTrigger >= 0) lt = pad.GetAxis(leftTrigger);
                if (rightTrigger >= 0) rt = pad.GetAxis(rightTrigger);
            }

            if (InvertStick) stickX = -stickX;

            var dt = Time.unscaledDeltaTime;

            if (Scroll.vertical)
            {
                // Stick up scrolls up; RT (right trigger) scrolls down.
                var delta = ApplyDeadZone(stickY, StickDeadZone) * StickSpeed;
                if (rt >= TriggerDeadZone) delta -= rt * TriggerSpeed;
                if (lt >= TriggerDeadZone) delta += lt * TriggerSpeed;

                if (!Mathf.Approximately(delta, 0f))
                    Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition + delta * dt);
            }

            if (Scroll.horizontal)
            {
                // Horizontal list: stick right and RT move forward.
                var delta = ApplyDeadZone(stickX, StickDeadZone) * StickSpeed;
                if (rt >= TriggerDeadZone) delta += rt * TriggerSpeed;
                if (lt >= TriggerDeadZone) delta -= lt * TriggerSpeed;

                if (!Mathf.Approximately(delta, 0f))
                    Scroll.horizontalNormalizedPosition = Mathf.Clamp01(Scroll.horizontalNormalizedPosition + delta * dt);
            }
        }

        private static float ApplyDeadZone(float value, float deadZone) =>
            Mathf.Abs(value) < deadZone ? 0f : value;
    }
}
