using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Fires <see cref="ReturnEvent"/>/<see cref="EscapeEvent"/> on active instances when
    /// triggered by <see cref="RewiredInputManager"/> via static Trigger methods.
    /// </summary>
    [MovedFrom(true, sourceClassName: "ReturnEscapeEvent")]
    public class ReturnEscapeEvent : MonoBehaviour
    {
        public static List<ReturnEscapeEvent> ReturnEscapeEventList { get; private set; } = new List<ReturnEscapeEvent>();

        [Header("Eventos Personalizados")]
        public UnityEvent ReturnEvent;
        public UnityEvent EscapeEvent;

        private void OnEnable() => ReturnEscapeEventList.Add(this);

        private void OnDisable() => ReturnEscapeEventList.Remove(this);

        public static void TriggerOk()
        {
            // Copy to array to prevent InvalidOperationException if invoking an event modifies the list (e.g. via disabling/destroying a component)
            var instances = ReturnEscapeEventList.ToArray();
            foreach (var instance in instances)
            {
                if (instance != null && instance.gameObject.activeInHierarchy)
                    instance.ReturnEvent?.Invoke();
            }
        }

        public static void TriggerEscape()
        {
            // Copy to array to prevent InvalidOperationException if invoking an event modifies the list
            var instances = ReturnEscapeEventList.ToArray();
            foreach (var instance in instances)
            {
                if (instance != null && instance.gameObject.activeInHierarchy)
                    instance.EscapeEvent?.Invoke();
            }
        }

        [ContextMenu("Disparar Evento de Retorno")]
        private void DebugTriggerReturnEvent() => ReturnEvent?.Invoke();

        [ContextMenu("Disparar Evento de Escape")]
        private void DebugTriggerEscapeEvent() => EscapeEvent?.Invoke();
    }
}
