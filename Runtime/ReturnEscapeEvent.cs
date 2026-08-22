using System.Collections.Generic;
using UnityEngine;
using System.Linq;
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

        [Range(0, 1000)]
        [Tooltip("Prioridade quando vários ReturnEscapeEvents estão ativos (maior vence). " +
                 "Apenas a instância de maior prioridade é disparada. Use um valor maior em painéis " +
                 "que devem consumir o Back antes de handlers globais (ex.: menu principal).")]
        public int Priority;

        private void OnEnable() => ReturnEscapeEventList.Add(this);

        private void OnDisable() => ReturnEscapeEventList.Remove(this);

        public static void TriggerOk()
        {
            // Copy to array to prevent InvalidOperationException if invoking an event modifies the list (e.g. via disabling/destroying a component)
            var instances = ReturnEscapeEventList.ToArray();
            foreach (var instance in HighestPriority(instances))
            {
                if (instance != null && instance.gameObject.activeInHierarchy)
                    instance.ReturnEvent?.Invoke();
            }
        }

        public static void TriggerEscape()
        {
            // Copy to array to prevent InvalidOperationException if invoking an event modifies the list
            var instances = ReturnEscapeEventList.ToArray();
            foreach (var instance in HighestPriority(instances))
            {
                if (instance != null && instance.gameObject.activeInHierarchy)
                    instance.EscapeEvent?.Invoke();
            }
        }

        /// <summary>
        /// Returns only the active instances tied for the highest Priority. When a panel closes
        /// itself in response, its OnDisable removes it from the list, so subsequent presses fall
        /// through to the next-lower-priority handler automatically.
        /// </summary>
        private static IEnumerable<ReturnEscapeEvent> HighestPriority(IEnumerable<ReturnEscapeEvent> instances)
        {
            ReturnEscapeEvent[] active = instances.Where(i => i != null && i.gameObject.activeInHierarchy).ToArray();
            if (active.Length == 0)
                yield break;

            int topPriority = active.Max(i => i.Priority);
            foreach (var instance in active.Where(i => i.Priority == topPriority))
                yield return instance;
        }

        [ContextMenu("Disparar Evento de Retorno")]
        private void DebugTriggerReturnEvent() => ReturnEvent?.Invoke();

        [ContextMenu("Disparar Evento de Escape")]
        private void DebugTriggerEscapeEvent() => EscapeEvent?.Invoke();
    }
}
