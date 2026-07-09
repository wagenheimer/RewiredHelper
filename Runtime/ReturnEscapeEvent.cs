using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Fires <see cref="ReturnEvent"/>/<see cref="EscapeEvent"/> on every active instance when
    /// <see cref="RewiredInputManager"/> sets <see cref="OkPressed"/>/<see cref="EscapePressed"/>
    /// (i.e. Return/Escape was pressed and no modal claimed it first).
    /// </summary>
    public class ReturnEscapeEvent : MonoBehaviour
    {
        public static bool EscapePressed = false;
        public static bool OkPressed = false;

        public static List<ReturnEscapeEvent> ReturnEscapeEventList { get; private set; } = new List<ReturnEscapeEvent>();

        [Header("Eventos Personalizados")]
        public UnityEvent ReturnEvent;
        public UnityEvent EscapeEvent;

        private void OnEnable() => ReturnEscapeEventList.Add(this);

        private void OnDisable() => ReturnEscapeEventList.Remove(this);

        private void Update()
        {
            if (OkPressed)
            {
                foreach (var instance in ReturnEscapeEventList)
                    instance.ReturnEvent.Invoke();

                OkPressed = false;
            }

            if (EscapePressed)
            {
                foreach (var instance in ReturnEscapeEventList)
                    instance.EscapeEvent.Invoke();

                EscapePressed = false;
            }
        }

        [ContextMenu("Disparar Evento de Retorno")]
        private void DebugTriggerReturnEvent() => ReturnEvent?.Invoke();

        [ContextMenu("Disparar Evento de Escape")]
        private void DebugTriggerEscapeEvent() => EscapeEvent?.Invoke();
    }
}
