using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Sirenix.OdinInspector;

/// <summary>
/// Gerencia botões de escape, garantindo prioridade sobre botões de retorno.
/// </summary>
public class EscapeButton : MonoBehaviour
{
    [Title("Configuração de Evento")]
    [Tooltip("Evento personalizado do Unity a ser disparado")]
    public UnityEvent UnityEvent;

    [Range(0, 1000)]
    [Tooltip("Prioridade do botão de escape (valores mais altos são acionados primeiro)")]
    public int Priority = 1;

    [Title("Instâncias Ativas")]
    [ReadOnly]
    [ShowInInspector]
    public static List<EscapeButton> ScapeButtonsList { get; private set; } = new List<EscapeButton>();

    [Title("Diagnóstico de Eventos")]
    [ShowInInspector]
    [ReadOnly]
    public int CustomEventListenerCount => UnityEvent.GetPersistentEventCount();

    [ShowInInspector]
    [ReadOnly]
    public bool IsButtonInteractable => GetComponent<Button>()?.interactable ?? false;

    private void OnEnable()
    {
        ScapeButtonsList.Add(this);
    }

    private void OnDisable()
    {
        ScapeButtonsList.Remove(this);
    }

    /// <summary>
    /// Verifica e aciona o primeiro botão de escape interativo encontrado.
    /// </summary>
    /// <returns>Verdadeiro se um botão de escape foi pressionado, falso caso contrário.</returns>
    public static bool PressedScape()
    {
        // Itera pelos botões de escape ordenados por prioridade
        foreach (var button in ScapeButtonsList.OrderByDescending(b => b.Priority))
        {
            var btn = button.GetComponent<Button>();

            // Verifica o Evento personalizado primeiro
            if (button.UnityEvent.GetPersistentEventCount() > 0)
            {
                button.UnityEvent.Invoke();
                return true;
            }

            // Verifica eventos de clique do botão, se disponíveis
            if (btn && btn.interactable && btn.onClick.GetPersistentEventCount() > 0)
            {
                btn.onClick.Invoke();
                return true;
            }
        }

        return false;
    }

    [Button("Disparar Evento de Depuração")]
    private void DebugTriggerEvent()
    {
        UnityEvent?.Invoke();
    }
}