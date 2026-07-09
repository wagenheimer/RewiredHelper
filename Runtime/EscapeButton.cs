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
        [Header("Configuração de Evento")]
        [Tooltip("Evento personalizado disparado em vez do onClick do Button")]
        public UnityEvent UnityEvent;

        [Range(0, 1000)]
        [Tooltip("Prioridade do botão de escape (valores mais altos são acionados primeiro)")]
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

        [ContextMenu("Disparar Evento de Depuração")]
        private void DebugTriggerEvent()
        {
            UnityEvent?.Invoke();
        }
    }
}
