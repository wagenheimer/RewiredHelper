using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

public class ReturnEscapeEvent : MonoBehaviour
{
    /// <summary>
    /// Estado de pressionamento da tecla Escape
    /// </summary>
    [Title("Estados de Entrada")]
    [ReadOnly]
    public static bool EscapePressed = false;

    /// <summary>
    /// Estado de pressionamento da tecla OK/Confirmação
    /// </summary>
    [ReadOnly]
    public static bool OkPressed = false;

    /// <summary>
    /// Lista de todas as instâncias ativas do componente ReturnEscapeEvent
    /// </summary>
    [Title("Instâncias Ativas")]
    [ReadOnly]
    [ShowInInspector]
    public static List<ReturnEscapeEvent> ReturnEscapeEventList { get; private set; } = new List<ReturnEscapeEvent>();

    /// <summary>
    /// Evento personalizado para ação de retorno
    /// </summary>
    [Title("Eventos Personalizados")]
    public UnityEvent ReturnEvent;

    /// <summary>
    /// Evento personalizado para ação de escape
    /// </summary>
    public UnityEvent EscapeEvent;

    /// <summary>
    /// Número de listeners no evento de retorno
    /// </summary>
    [Title("Diagnóstico de Eventos")]
    [ShowInInspector]
    [ReadOnly]
    public int ReturnEventListenerCount => ReturnEvent.GetPersistentEventCount();

    /// <summary>
    /// Número de listeners no evento de escape
    /// </summary>
    [ShowInInspector]
    [ReadOnly]
    public int EscapeEventListenerCount => EscapeEvent.GetPersistentEventCount();

    /// <summary>
    /// Adiciona esta instância à lista quando ativada
    /// </summary>
    private void OnEnable()
    {
        ReturnEscapeEventList.Add(this);
    }

    /// <summary>
    /// Remove esta instância da lista quando desativada
    /// </summary>
    private void OnDisable()
    {
        ReturnEscapeEventList.Remove(this);
    }

    /// <summary>
    /// Verifica a cada quadro se os botões foram pressionados
    /// </summary>
    private void Update()
    {
        // Verifica se o botão OK foi pressionado
        if (OkPressed)
        {
            // Verifica se não há diálogos visíveis e a UI não está bloqueada
            if (!Dialogs.IsThereAnyVisible && !Main.main.IsUiBlocked)
            {
                // Invoca o evento de retorno para todas as instâncias na lista
                foreach (var returnEscapeEvent in ReturnEscapeEventList)
                {
                    returnEscapeEvent.ReturnEvent.Invoke();
                }
            }
            // Reseta a variável para evitar invocações repetidas
            OkPressed = false;
        }

        // Verifica se o botão Escape foi pressionado
        if (EscapePressed)
        {
            // Verifica se não há diálogos visíveis e a UI não está bloqueada
            if (!Dialogs.IsThereAnyVisible && !Main.main.IsUiBlocked)
            {
                // Invoca o evento de escape para todas as instâncias na lista
                foreach (var returnEscapeEvent in ReturnEscapeEventList)
                {
                    returnEscapeEvent.EscapeEvent.Invoke();
                }
            }
            // Reseta a variável para evitar invocações repetidas
            EscapePressed = false;
        }
    }

    /// <summary>
    /// Botão de depuração para disparar manualmente o evento de retorno
    /// </summary>
    [Button("Disparar Evento de Retorno")]
    private void DebugTriggerReturnEvent()
    {
        ReturnEvent?.Invoke();
    }

    /// <summary>
    /// Botão de depuração para disparar manualmente o evento de escape
    /// </summary>
    [Button("Disparar Evento de Escape")]
    private void DebugTriggerEscapeEvent()
    {
        EscapeEvent?.Invoke();
    }
}