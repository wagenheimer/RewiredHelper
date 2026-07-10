using System;
using UnityEngine;
using Rewired;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Reads keyboard keys and gamepad buttons to feed input into a Custom Controller tagged as "AndroidRemote".
    /// This acts as a workaround for Unity's limitation with Android remote detection.
    /// </summary>
    [AddComponentMenu("Rewired Helper/Rewired Custom Controller (Android Remote)")]
    public class RewiredCustomController_AndroidRemote : MonoBehaviour
    {
        [Tooltip("The Rewired Player ID to feed input into.")]
        public int playerId;

        private const string controllerTag = "AndroidRemote";
        private const int buttonCount = 8;
        private const int axisCount = 0;

        private CustomController controller;

        [NonSerialized] // Don't serialize this so the value is lost on editor script recompiles.
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            if (!ReInput.isReady) return;

            // Find the player
            Player player = ReInput.players.GetPlayer(playerId);
            if (player == null)
            {
                Debug.LogError($"[RewiredHelper] Player ID {playerId} not found.");
                return;
            }

            // Get the custom controller
            controller = (CustomController)player.controllers.GetControllerWithTag(ControllerType.Custom, controllerTag);
            if (controller == null)
            {
                Debug.LogWarning($"[RewiredHelper] A Custom Controller with tag \"{controllerTag}\" was not found for Player ID {playerId}.");
                return;
            }

            // Verify controller has the number of elements we expect
            if (controller.buttonCount != buttonCount || controller.axisCount != axisCount)
            {
                Debug.LogError("[RewiredHelper] AndroidRemote Custom Controller has wrong number of elements!");
                return;
            }

            // Subscribe to the input source update event
            ReInput.InputSourceUpdateEvent += OnInputSourceUpdate;
#endif
            initialized = true;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!ReInput.isReady) return;
            if (!initialized) Initialize();
        }
#endif

        private void OnDisable()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            ReInput.InputSourceUpdateEvent -= OnInputSourceUpdate;
#endif
            initialized = false;
        }

        private void OnInputSourceUpdate()
        {
            if (controller == null) return;
            GetSourceButtonValues();
        }

        private void GetSourceButtonValues()
        {
            controller.SetButtonValue(0, Input.GetKey(KeyCode.UpArrow));
            controller.SetButtonValue(1, Input.GetKey(KeyCode.DownArrow));
            controller.SetButtonValue(2, Input.GetKey(KeyCode.LeftArrow));
            controller.SetButtonValue(3, Input.GetKey(KeyCode.RightArrow));
            controller.SetButtonValue(4, Input.GetKey(KeyCode.JoystickButton0)); // Center/OK button on Android Remote
            controller.SetButtonValue(5, Input.GetKey(KeyCode.Escape)); // Back
            controller.SetButtonValue(6, Input.GetKey(KeyCode.Menu)); // Menu
            controller.SetButtonValue(7, Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Return)); // Select
        }
    }
}
