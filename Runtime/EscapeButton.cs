using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Registers a button to be triggered when Escape is pressed and no modal from
    /// <see cref="IModalStackProvider"/> claims it first. Multiple active instances are
    /// resolved by <see cref="Priority"/> (higher first).
    /// </summary>
    [MovedFrom(true, sourceClassName: "EscapeButton")]
    public class EscapeButton : MonoBehaviour
    {
        [Header("Event Configuration")]
        [Tooltip("Custom event triggered instead of the Button's onClick")]
        public UnityEvent UnityEvent;

        [Range(0, 1000)]
        [Tooltip("Escape button priority (higher values are triggered first)")]
        public int Priority = 1;

        public static List<EscapeButton> ScapeButtonsList { get; private set; } = new List<EscapeButton>();

        private void OnEnable() => ScapeButtonsList.Add(this);

        private void OnDisable() => ScapeButtonsList.Remove(this);

        /// <summary>Triggers the highest-priority interactable escape button. Returns true if one was triggered.</summary>
        public static bool PressedScape()
        {
            foreach (var button in ScapeButtonsList.OrderByDescending(b => b.Priority))
            {
                var btn = button.GetComponent<Button>();

                if (button.UnityEvent.GetPersistentEventCount() > 0)
                {
                    button.UnityEvent.Invoke();
                    return true;
                }

                if (btn && btn.interactable && btn.onClick.GetPersistentEventCount() > 0)
                {
                    btn.onClick.Invoke();
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("Trigger Debug Event")]
        private void DebugTriggerEvent()
        {
            UnityEvent?.Invoke();
        }
    }
}
