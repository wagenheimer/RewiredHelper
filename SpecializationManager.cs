using UnityEngine;
using Rewired; 

namespace I2.Loc
{
    public partial class SpecializationManager
    {
        /// <summary>
        /// Determina a especialização do dispositivo de entrada atual para adaptar a UI e mensagens.
        /// Garante uma experiência consistente entre plataformas ao detectar o tipo de entrada ativo,
        /// com regras estritas para consoles.
        /// </summary>
        /// <returns>
        /// Nome da especialização como string:
        /// • "Touch": Se a entrada por toque foi usada recentemente. (Prioridade máxima)
        /// • "Controller": Em qualquer plataforma de console, ou quando um controle está ativo no PC.
        /// • "PC": Apenas em plataformas não-console (PC/Mac/Linux) quando o mouse é o último dispositivo ativo.
        /// </returns>
        public override string GetCurrentSpecialization()
        {
            // --- Prioridade 1: Input de Toque ---
            // Se o toque foi usado recentemente, ele tem a maior prioridade em qualquer plataforma.
            if (RewiredHelper.IsUsingTouch)
            {
                return "Touch";
            }

            // --- Prioridade 2: Plataformas de Console ---
            // Se for um console, a especialização será SEMPRE "Controller",
            // garantindo que ícones de mouse/teclado nunca apareçam.
            // A verificação de Touch já foi feita.
            if (IsConsolePlatform())
            {
                return "Controller";
            }

            // --- Prioridade 3: Plataformas Desktop (PC, Mac, Linux) ---
            // Se chegamos aqui, não é um console e não há input de toque.
            // Agora, diferenciamos entre Mouse e Controle.
            if (RewiredHelper.instance != null && RewiredHelper.instance.UltimoControleAtivo != null)
            {
                return RewiredHelper.instance.UltimoControleAtivo.type switch
                {
                    ControllerType.Mouse => "PC",
                    ControllerType.Joystick => "Controller",
                    ControllerType.Custom => "Controller", // Trata tipos customizados como um controle
                    _ => "Controller"  // Fallback seguro para outros tipos
                };
            }

            // --- Fallback Final para Desktop ---
            // Se o Rewired não estiver inicializado ou nenhum controle foi detectado ainda,
            // o padrão para uma plataforma não-console é "PC".
            return "PC";
        }

        /// <summary>
        /// Verifica de forma centralizada se a plataforma de execução é um console.
        /// </summary>
        /// <returns>True se for Switch, PlayStation ou Xbox.</returns>
        private bool IsConsolePlatform()
        {
            return true;
            // Usar um switch expression é mais limpo e extensível para adicionar futuras plataformas.
            return Application.platform switch
            {
                RuntimePlatform.Switch => true,
                RuntimePlatform.PS4 => true,
                RuntimePlatform.PS5 => true,
                RuntimePlatform.XboxOne => true,
                // Adicione outras plataformas de console aqui se necessário (ex: Stadia)
                _ => false
            };
        }

        /// <summary>
        /// Força a re-localização de todos os textos na tela.
        /// Útil para ser chamado em eventos como a reconexão de um controle.
        /// </summary>
        public void ForceUpdateSpecialization()
        {
            LocalizationManager.LocalizeAll(true);
        }
    }
}