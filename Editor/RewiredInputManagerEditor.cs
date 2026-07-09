using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    [CustomEditor(typeof(RewiredInputManager))]
    public class RewiredInputManagerEditor : UnityEditor.Editor
    {
        private static bool showHelpFoldout = true;
        private static bool showBootstrapHelp = false;
        private static bool showRoutingHelp = false;
        private static bool showApisHelp = false;

        private static readonly string GlyphHelperTypeName = "Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper";

        public override void OnInspectorGUI()
        {
            var manager = (RewiredInputManager)target;

            // Desenhar campos padrões do Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(15);
            DrawSeparator();
            EditorGUILayout.Space(5);

            // ==========================================
            // SEÇÃO 1: MANUAL DE AJUDA / DOCUMENTAÇÃO
            // ==========================================
            showHelpFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showHelpFoldout, "📖 GUIA DE AJUDA RÁPIDA (HELP)");
            if (showHelpFoldout)
            {
                EditorGUI.indentLevel++;

                showBootstrapHelp = EditorGUILayout.Foldout(showBootstrapHelp, "Como Inicializar (Bootstrap)");
                if (showBootstrapHelp)
                {
                    EditorGUILayout.HelpBox(
                        "Para inicializar o gerenciador, chame o método Configure() no seu script de bootstrap (ex. no Awake):\n\n" +
                        "RewiredInputManager.Instance.Configure(\n" +
                        "    uiBlocker: seuUiBlocker,\n" +
                        "    modalStack: seuModalStack,\n" +
                        "    controllerHelpGate: seuHelpGate\n" +
                        ");\n\n" +
                        "Todos os argumentos são opcionais e assumem comportamentos padrões seguros se omitidos.",
                        MessageType.Info
                    );
                }

                showRoutingHelp = EditorGUILayout.Foldout(showRoutingHelp, "Roteamento de Escape & Return");
                if (showRoutingHelp)
                {
                    EditorGUILayout.HelpBox(
                        "• EscapeButton: Anexe a qualquer botão UI. O de maior prioridade ativo no momento responderá ao 'Escape' (ou botões Back/Menu dos controles).\n\n" +
                        "• ReturnEscapeEvent: Dispara eventos genéricos quando Escape ou Return forem pressionados e nenhum botão ou modal reivindicar a tecla primeiro.\n\n" +
                        "• IModalStackProvider: Registre seu próprio stack de modais para que o topo dele ganhe prioridade no roteamento de teclas automaticamente.",
                        MessageType.Info
                    );
                }

                showApisHelp = EditorGUILayout.Foldout(showApisHelp, "APIs Principais para seu Código");
                if (showApisHelp)
                {
                    EditorGUILayout.HelpBox(
                        "• RewiredInputManager.IsUsingTouch: true se o jogador tocou na tela recentemente.\n\n" +
                        "• manager.CurrentControllerType: Retorna se o jogador está usando Mouse/Teclado, Joystick (Controle) ou Custom (Touch).\n\n" +
                        "• RewiredInputManager.OnInputTypeChanged: Evento estático disparado ao trocar o tipo de entrada (ex: alternar ícones de ajuda).\n\n" +
                        "• manager.Vibrate(): Dispara vibração no controle ativo do jogador 0.",
                        MessageType.Info
                    );
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(15);
            DrawSeparator();
            EditorGUILayout.Space(5);

            // ==========================================
            // SEÇÃO 2: DIAGNOSTICADOR DE SETUP (CHECKER)
            // ==========================================
            GUILayout.Label("🛠️ DIAGNOSTICADOR DE SETUP (STATUS CHECKER)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 1. Verificar Rewired Input Manager na Cena
            var hasRewired = UnityEngine.Object.FindObjectOfType<Rewired.InputManager>() != null;
            DrawCheckResult("Rewired Input Manager (Nativo)", hasRewired, 
                "Instancie o prefab configurado do Rewired para gerenciar mapeamentos e botões.",
                "Criar Manager", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 2. Verificar Event System com módulo do Rewired
            var hasEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null;
            DrawCheckResult("Rewired Event System", hasEventSystem,
                "É necessário um Event System com suporte ao Rewired para navegação de UI via controle.",
                "Criar Event System", () => DefaultSetupGenerator.CreateRewiredInputManager()); // CreateRewiredInputManager já cria o Event System se faltar

            // 3. Verificar Canvas
            var hasCanvas = UnityEngine.Object.FindObjectOfType<Canvas>() != null;
            DrawCheckResult("Canvas de UI", hasCanvas,
                "A cena precisa de um Canvas para renderizar o cursor customizado e modais de diálogo.",
                "Criar Canvas", () => {
                    var go = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                    var canvas = go.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                });

            // 4. Verificação de Propriedades do Componente
            if (manager.CustomCursorEnabled && manager.CursorTexture == null)
            {
                EditorGUILayout.HelpBox("⚠️ Cursor Customizado Standalone ativado, mas nenhuma textura (Cursor Texture) foi atribuída!", MessageType.Warning);
            }

            if (manager.GameCursor == null)
            {
                EditorGUILayout.HelpBox("💡 Dica: Atribua uma imagem de UI no campo 'Game Cursor' se você utiliza um cursor customizado renderizado na tela.", MessageType.None);
            }

            // 5. Verificar Estado de Inicialização em Runtime
            if (EditorApplication.isPlaying)
            {
                if (!manager.IsConfigured)
                {
                    EditorGUILayout.HelpBox("❌ Runtime: O método Configure() ainda não foi chamado nesta execução. Inicialize-o no Awake/Start do seu jogo.", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("✅ Runtime: Inicializado e configurado com sucesso!", MessageType.Info);
                }
            }

            // 6. Verificar Addon de Glifos do Rewired
            var glyphHelperType = FindGlyphHelperType();
            if (glyphHelperType == null)
            {
                EditorGUILayout.HelpBox("ℹ️ Nota: O Addon Oficial de Glifos do Rewired não foi detectado no projeto. Se você deseja usar glifos dinâmicos nos textos de ajuda, instale-o em:\nWindow > Rewired > Extras > Glyphs > Install", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Addon de Glifos do Rewired detectado e ativo!", MessageType.Info);
            }

            EditorGUILayout.Space(10);
        }

        private void DrawCheckResult(string title, bool pass, string missingDesc, string fixBtnText, Action fixAction)
        {
            EditorGUILayout.BeginHorizontal();
            
            var statusIcon = pass ? "  ✅  " : "  ❌  ";
            var style = new GUIStyle(EditorStyles.label) { richText = true };
            
            GUILayout.Label($"<b>{statusIcon} {title}</b>", style, GUILayout.Width(250));

            if (!pass)
            {
                if (GUILayout.Button(fixBtnText, GUILayout.Width(130)))
                {
                    fixAction?.Invoke();
                }
            }
            else
            {
                GUILayout.Label("<color=green>Pronto!</color>", style);
            }

            EditorGUILayout.EndHorizontal();

            if (!pass && !string.IsNullOrEmpty(missingDesc))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(missingDesc, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        private Type FindGlyphHelperType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(GlyphHelperTypeName);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
