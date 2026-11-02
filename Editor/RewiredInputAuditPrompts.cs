using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Ready-to-paste instructions for an AI coding assistant (Antigravity, Claude, Copilot), one per
    /// audit finding. Kept here (instead of inline in <see cref="RewiredSetupChecks"/>) so the text can be
    /// reviewed and unit-tested on its own.
    /// </summary>
    public static class RewiredInputAuditPrompts
    {
        /// <summary>Prompt for "Legacy Input that a gamepad cannot reach".</summary>
        public static string BuildGamepadReachabilityPrompt(List<InputCodeHit> hits)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Neste projeto Unity, migre as leituras de UnityEngine.Input listadas abaixo para o Rewired " +
                          "(pacote com.wagenheimer.rewiredhelper).");
            sb.AppendLine();
            sb.AppendLine("Por que isso é um problema:");
            sb.AppendLine("UnityEngine.Input lê o hardware do mouse e do teclado diretamente. Um jogador usando controle " +
                          "não consegue acionar nada disso — e o cursor virtual do RewiredHelper NÃO move Input.mousePosition, " +
                          "então Input.GetMouseButton/Input.mousePosition continuam apontando para o ponteiro físico.");
            sb.AppendLine();
            sb.AppendLine("Leituras detectadas:");
            foreach (var hit in hits.Take(15))
                sb.AppendLine($"• {Path.GetFileName(hit.Path)} (linha {hit.Line}): {hit.Snippet}");
            if (hits.Count > 15)
                sb.AppendLine($"• ... e mais {hits.Count - 15} linha(s)");
            sb.AppendLine();
            sb.AppendLine("Como migrar:");
            sb.AppendLine("1. \"Pressione qualquer botão\" (Input.anyKey / Input.GetMouseButtonDown): use " +
                          "ReInput.controllers.GetAnyButtonDown() para pressionar e ReInput.controllers.GetAnyButton() " +
                          "para manter pressionado. Isso cobre teclado, mouse e joystick.");
            sb.AppendLine("2. Botões nomeados (Input.GetButton(\"...\")): crie/use uma Action no Rewired Input Manager e leia " +
                          "Player.GetButtonDown(\"SuaAction\") (e GetButton para manter pressionado).");
            sb.AppendLine("3. Cliques em objetos do mundo (Input.GetMouseButton* + Input.mousePosition): use " +
                          "RewiredInputManager.RewiredMousePosition como posição do ponteiro (ele segue o cursor virtual no " +
                          "joystick) mantendo o mesmo Physics.Raycast. Para UI, prefira Button/EventSystem.");
            sb.AppendLine("4. Atalhos de desenvolvimento que devem continuar no teclado: mantenha o código e adicione o " +
                          "comentário '// rewired-helper: ignore-legacy-input' no topo do arquivo para a auditoria ignorá-lo.");
            sb.AppendLine("5. Não chame Cursor.SetCursor/Cursor.visible: o RewiredInputManager é o dono do cursor " +
                          "(CustomCursorEnabled + RefreshCursor).");
            sb.AppendLine();
            sb.AppendLine("Depois de alterar, rode Tools > Wagenheimer > Rewired Helper > Verify Setup para confirmar.");
            return sb.ToString();
        }

        /// <summary>Prompt for "Custom Cursor option is not hidden on gamepad".</summary>
        public static string BuildCustomCursorOptionPrompt(List<string> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Neste projeto Unity, a opção \"Cursor Personalizado\" do Options fica sempre visível, mas ela só " +
                          "troca a textura do ponteiro do mouse. No joystick/touch o RewiredInputManager desenha o próprio " +
                          "cursor, então a opção não tem efeito e deve desaparecer automaticamente.");
            sb.AppendLine();
            if (files != null && files.Count > 0)
            {
                sb.AppendLine("Arquivos que expõem a preferência sem reagir ao dispositivo ativo:");
                foreach (string file in files)
                    sb.AppendLine($"• {Path.GetFileName(file)}");
                sb.AppendLine();
            }

            sb.AppendLine("Como corrigir (prefira a opção 1):");
            sb.AppendLine("1. Adicione InputVisibilityController ao GameObject do Toggle (o rótulo costuma ser filho dele, então " +
                          "a linha inteira some) e configure o modo para esconder no joystick/touch:");
            sb.AppendLine();
            sb.AppendLine("   var visibility = toggle.gameObject.AddComponent<InputVisibilityController>();");
            sb.AppendLine("   visibility.SetVisibilityMode(");
            sb.AppendLine("       InputVisibilityController.VisibilityMode.ShowOnMouseOrKeyboardHideOnJoystickOrTouch);");
            sb.AppendLine();
            sb.AppendLine("2. Alternativa: assine RewiredInputManager.OnInputSpecializationChanged e chame " +
                          "toggle.gameObject.SetActive(!manager.IsUsingJoystickCursor && !RewiredInputManager.IsUsingTouch) " +
                          "imediatamente ao entrar na tela.");
            sb.AppendLine();
            sb.AppendLine("Regras:");
            sb.AppendLine("• Não apague a preferência salva em hipótese alguma; apenas esconda a linha. " +
                          "Main.main.SetCustomCursor/manager.CustomCursorEnabled continuam valendo quando o jogador volta ao mouse.");
            sb.AppendLine("• Use interatividade desabilitada apenas se o design pedir a opção sempre visível; para \"sumir\", " +
                          "use ToggleGameObject (TargetAction padrão do InputVisibilityController).");
            sb.AppendLine();
            sb.AppendLine("Depois de alterar, rode Tools > Wagenheimer > Rewired Helper > Verify Setup para confirmar.");
            return sb.ToString();
        }
    }
}
