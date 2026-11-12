using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Detects and migrates legacy manual Escape/Return input handling typically found in
    /// <c>MainBase.cs</c> or <c>Main.cs</c>.
    /// <para>
    /// <see cref="RewiredInputManager"/> already handles Escape/Return routing to modal dialogs
    /// (<c>Dialogs.Modals</c>), <see cref="EscapeButton"/>, and <see cref="ReturnEscapeEvent"/>
    /// for both keyboard and gamepad. Leaving the legacy block active causes duplicate events and
    /// double-clicking dialog buttons.
    /// </para>
    /// </summary>
    public static class RewiredLegacyInputMigrator
    {
        private const string MenuPath = RewiredHelperMenu.MigrateLegacyMainInput;

        // Matches the standard MainBase.cs block containing !Main.main.IsUiBlocked and both Escape and Return blocks.
        private static readonly Regex LegacyMainBaseBlock = new Regex(
            @"(?<indent>[ \t]*)if\s*\(\s*!Main\.main\.IsUiBlocked\s*\)\s*\{[ \t]*\r?\n" +
            @"(?:[ \t]*\r?\n)*" +
            @"[ \t]*if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Escape\s*\)[\s\S]*?ReturnEscapeEvent\.EscapePressed\s*=\s*true;\s*\}[ \t]*\r?\n" +
            @"(?:[ \t]*\r?\n)*" +
            @"[ \t]*if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Return\s*\)[\s\S]*?ReturnEscapeEvent\.OkPressed\s*=\s*true;\s*\}[ \t]*\r?\n" +
            @"[ \t]*\}",
            RegexOptions.Compiled);

        // Matches standalone Escape & Return pairs with ReturnEscapeEvent flags even if not wrapped in !IsUiBlocked.
        private static readonly Regex LegacyEscapeReturnPair = new Regex(
            @"(?<indent>[ \t]*)if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Escape\s*\)[\s\S]*?ReturnEscapeEvent\.EscapePressed\s*=\s*true;\s*\}[ \t]*\r?\n" +
            @"(?:[ \t]*\r?\n)*" +
            @"[ \t]*if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Return\s*\)[\s\S]*?ReturnEscapeEvent\.OkPressed\s*=\s*true;\s*\}",
            RegexOptions.Compiled);

        // Matches individual legacy Escape block with Dialogs.Modals or ReturnEscapeEvent.
        private static readonly Regex LegacyEscapeBlock = new Regex(
            @"(?<indent>[ \t]*)if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Escape\s*\)[\s\S]*?(?:ReturnEscapeEvent\.EscapePressed\s*=\s*true|Dialogs\.Modals[\s\S]*?Invoke\(\))\s*;?\s*\}",
            RegexOptions.Compiled);

        // Matches individual legacy Return block with Dialogs.Modals or ReturnEscapeEvent.
        private static readonly Regex LegacyReturnBlock = new Regex(
            @"(?<indent>[ \t]*)if\s*\(\s*Input\.GetKeyDown\s*\(\s*KeyCode\.Return\s*\)[\s\S]*?(?:ReturnEscapeEvent\.OkPressed\s*=\s*true|Dialogs\.Modals[\s\S]*?Invoke\(\))\s*;?\s*\}",
            RegexOptions.Compiled);

        [MenuItem(MenuPath, priority = RewiredHelperMenu.MigrateLegacyMainInputPriority)]
        public static void MenuMigrateProject()
        {
            var hits = ProjectInputScanner.FindRawInput();
            if (hits.Count == 0)
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "Nenhum script com leitura manual de Escape/Return encontrado em Assets/.\n" +
                    "O projeto já está limpo e o RewiredInputManager gerencia as entradas.", "OK");
                return;
            }

            var filePaths = hits.Select(h => h.Path).Distinct().ToList();
            if (!EditorUtility.DisplayDialog("Migrar Input Legado (MainBase / Main)",
                    $"Foram detectadas leituras manuais de Escape/Return em {filePaths.Count} arquivo(s):\n\n" +
                    string.Join("\n", filePaths.Take(5).Select(p => "• " + Path.GetFileName(p))) +
                    (filePaths.Count > 5 ? $"\n... e mais {filePaths.Count - 5} arquivo(s)" : "") + "\n\n" +
                    "O RewiredHelper já gerencia o fechamento de modais e botões de escape/return automaticamente.\n" +
                    "Deseja comentar os blocos manuais redundantes nestes arquivos? (Um backup .bak será criado para cada um).",
                    "Migrar", "Cancelar"))
                return;

            MigrateHits(hits);
        }

        /// <summary>
        /// Pure helper: analyzes <paramref name="sourceText"/> and produces <paramref name="migratedText"/>
        /// with the legacy Escape/Return blocks commented out. Returns true if changes were made.
        /// </summary>
        public static bool TryMigrateScriptText(string sourceText, out string migratedText)
        {
            migratedText = sourceText;
            if (string.IsNullOrEmpty(sourceText)) return false;

            bool changed = false;

            // 1. Try full MainBase block (!IsUiBlocked + Escape + Return)
            if (LegacyMainBaseBlock.IsMatch(migratedText))
            {
                migratedText = LegacyMainBaseBlock.Replace(migratedText, m =>
                {
                    string ind = m.Groups["indent"].Value;
                    return $"{ind}// [RewiredHelper Migration] O tratamento manual de Escape e Return foi desativado aqui.\n" +
                           $"{ind}// O RewiredInputManager.Routing já gerencia automaticamente fechamento de modais (Dialogs.Modals),\n" +
                           $"{ind}// botões EscapeButton e eventos ReturnEscapeEvent tanto para teclado quanto para gamepads.\n" +
                           $"{ind}/*\n{m.Value}\n{ind}*/";
                });
                changed = true;
            }
            // 2. Try Escape + Return pair
            else if (LegacyEscapeReturnPair.IsMatch(migratedText))
            {
                migratedText = LegacyEscapeReturnPair.Replace(migratedText, m =>
                {
                    string ind = m.Groups["indent"].Value;
                    return $"{ind}// [RewiredHelper Migration] O tratamento manual de Escape e Return foi desativado aqui.\n" +
                           $"{ind}// O RewiredInputManager.Routing já gerencia automaticamente fechamento de modais (Dialogs.Modals),\n" +
                           $"{ind}// botões EscapeButton e eventos ReturnEscapeEvent tanto para teclado quanto para gamepads.\n" +
                           $"{ind}/*\n{m.Value}\n{ind}*/";
                });
                changed = true;
            }
            else
            {
                // 3. Try individual Escape / Return blocks
                if (LegacyEscapeBlock.IsMatch(migratedText))
                {
                    migratedText = LegacyEscapeBlock.Replace(migratedText, m =>
                    {
                        string ind = m.Groups["indent"].Value;
                        return $"{ind}// [RewiredHelper Migration] Escape manual desativado (gerenciado pelo RewiredInputManager):\n" +
                               $"{ind}/*\n{m.Value}\n{ind}*/";
                    });
                    changed = true;
                }

                if (LegacyReturnBlock.IsMatch(migratedText))
                {
                    migratedText = LegacyReturnBlock.Replace(migratedText, m =>
                    {
                        string ind = m.Groups["indent"].Value;
                        return $"{ind}// [RewiredHelper Migration] Return manual desativado (gerenciado pelo RewiredInputManager):\n" +
                               $"{ind}/*\n{m.Value}\n{ind}*/";
                    });
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Migrates all files identified by <paramref name="hits"/>.
        /// Creates a <c>.bak</c> copy and refreshes the AssetDatabase.
        /// </summary>
        public static void MigrateHits(List<InputCodeHit> hits)
        {
            if (hits == null || hits.Count == 0) return;

            var paths = hits.Select(h => h.Path).Distinct().ToList();
            var migratedFiles = new List<string>();

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    string text = File.ReadAllText(path);
                    if (TryMigrateScriptText(text, out string newText))
                    {
                        // Backup
                        File.Copy(path, path + ".bak", true);
                        File.WriteAllText(path, newText);
                        migratedFiles.Add(Path.GetFileName(path));
                    }
                    else
                    {
                        // Fallback: If it couldn't find a known multi-line block, add the ignore-input marker
                        // so audit passes if desired, or skip.
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RewiredHelper] Falha ao migrar arquivo '{path}': {ex.Message}");
                }
            }

            if (migratedFiles.Count > 0)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Migração Concluída",
                    $"Input manual de Escape/Return comentado com sucesso em:\n• " +
                    string.Join("\n• ", migratedFiles) +
                    "\n\nArquivos de backup (.bak) foram criados.\n" +
                    "O RewiredInputManager agora é a única fonte que processa Escape e Return.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "Nenhum bloco de código padrão do MainBase foi substituído automaticamente.\n" +
                    "Use o prompt para IA (ícone 🤖 no Dashboard) para ajustar scripts com lógica customizada.", "OK");
            }
        }

        /// <summary>
        /// Generates a ready-to-paste instruction for an AI coding assistant (Antigravity, Claude, Copilot).
        /// </summary>
        public static string BuildAiPrompt(List<InputCodeHit> hits)
        {
            string filesList = hits != null && hits.Count > 0
                ? string.Join("\n", hits.Select(h => $"• {h.ScriptName} (linha {h.Line}): `{h.Snippet}`"))
                : "• MainBase.cs / Main.cs";

            return
                "Neste projeto Unity, remova ou comente o tratamento manual de 'KeyCode.Escape' e 'KeyCode.Return' " +
                "presente no(s) script(s) abaixo, pois o pacote 'com.wagenheimer.rewiredhelper' já gerencia nativamente " +
                "o fechamento de modais (Dialogs.Modals), os botões EscapeButton e os eventos ReturnEscapeEvent tanto para teclado quanto para gamepads.\n\n" +
                "Problema resolvido:\n" +
                "Quando MainBase.Update() e RewiredInputManager processam Escape/Return simultaneamente, os botões recebem duplo clique no mesmo frame " +
                "(fechando dois diálogos de uma vez ou confirmando e fechando a tela de trás).\n\n" +
                "Arquivos detectados com leitura direta:\n" +
                filesList + "\n\n" +
                "Instruções para a IA:\n" +
                "1. Abra o arquivo indicado e localize no Update() o bloco similar a:\n" +
                "   if (!Main.main.IsUiBlocked)\n" +
                "   {\n" +
                "       if (Input.GetKeyDown(KeyCode.Escape) || ...)\n" +
                "       {\n" +
                "           if (Dialogs.Modals.Count > 0 ...) Dialogs.Modals[...].EscapeButton.onClick.Invoke();\n" +
                "           else ReturnEscapeEvent.EscapePressed = true;\n" +
                "       }\n" +
                "       if (Input.GetKeyDown(KeyCode.Return))\n" +
                "       {\n" +
                "           if (Dialogs.Modals.Count > 0 ...) Dialogs.Modals[...].OkButton.onClick.Invoke();\n" +
                "           else ReturnEscapeEvent.OkPressed = true;\n" +
                "       }\n" +
                "   }\n" +
                "2. Remova ou comente esse trecho manual.\n" +
                "3. Para manter a verificação de UI bloqueada, certifique-se de que o RewiredInputManager foi configurado no Awake/Start com:\n" +
                "   RewiredInputManager.Configure(uiBlocker: () => Main.main != null && Main.main.IsUiBlocked);\n" +
                "4. Salve e valide executando 'Tools > Wagenheimer > Rewired Helper > Verify Setup'.";
        }
    }
}

