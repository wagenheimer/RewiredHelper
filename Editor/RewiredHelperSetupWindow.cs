using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    public class RewiredHelperSetupWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _ranScan;
        private bool _showHelp = true;

        // Cores no estilo do CloudSaveAudit/Editor do Unity
        private static Color ColBg => EditorGUIUtility.isProSkin
            ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        private static Color ColCard => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        private static Color ColGreen => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        private static Color ColRed => EditorGUIUtility.isProSkin
            ? new(0.85f, 0.25f, 0.20f) : new(0.70f, 0.15f, 0.10f);
        private static Color ColOrange => EditorGUIUtility.isProSkin
            ? new(1.00f, 0.60f, 0.10f) : new(0.85f, 0.50f, 0.05f);
        private static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        private static readonly Color ColDim = new(0.55f, 0.55f, 0.60f);

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Setup Checker & Help", priority = 1)]
        public static void ShowWindow()
        {
            var w = GetWindow<RewiredHelperSetupWindow>("Rewired Helper Checker");
            w.minSize = new Vector2(550, 450);
            w.Show();
        }

        private void OnEnable()
        {
            _ranScan = false;
        }

        private void OnGUI()
        {
            // Banner Superior no estilo do CloudSaveAudit
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 54), ColAccent);
            GUILayout.Space(8);
            var bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🎮  Rewired Helper Diagnostics & Setup", bannerStyle, GUILayout.ExpandWidth(true));
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.85f, 0.90f, 1f) },
                alignment = TextAnchor.MiddleCenter
            };
            
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RewiredHelperSetupWindow).Assembly);
            var versionText = pkg != null ? $"v{pkg.version}" : "v1.0.0";
            EditorGUILayout.LabelField($"{versionText}  ·  Real-time scene configuration scanner", subStyle);
            GUILayout.Space(6);

            // Fundo da janela
            EditorGUI.DrawRect(new Rect(0, 54, position.width, position.height - 54), ColBg);

            var areaStyle = new GUIStyle { padding = new RectOffset(10, 10, 8, 8) };
            EditorGUILayout.BeginVertical(areaStyle);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🔍  Scan Scene Configuration", GUILayout.Height(30)))
                {
                    _ranScan = true;
                }
                _showHelp = GUILayout.Toggle(_showHelp, "Show Quick Help Guide", GUILayout.Height(30), GUILayout.Width(170));
            }

            EditorGUILayout.Space(5);

            if (_ranScan)
            {
                // Obter diagnóstico das checagens
                var managerComp = DefaultSetupGenerator.FindInputManagerInScene();
                var hasManager = managerComp != null;
                var hasHelper = hasManager && managerComp.GetComponent<RewiredInputManager>() != null;
                var hasEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null;
                var hasCanvas = UnityEngine.Object.FindObjectOfType<Canvas>() != null;

                var isUsingTouch = RewiredInputManager.IsUsingTouch;
                
                int passed = 0;
                int total = 4;

                if (hasManager) passed++;
                if (hasHelper) passed++;
                if (hasEventSystem) passed++;
                if (hasCanvas) passed++;

                var pct = (float)passed / total;

                // Barra de progresso visual do CloudSaveAudit
                var barRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                var color = pct >= 1f ? ColGreen : pct >= 0.5f ? ColOrange : ColRed;
                if (pct > 0f)
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), color);
                
                var centerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = 11
                };
                EditorGUI.LabelField(barRect, $"{passed}/{total}  Checks OK  ({(int)(pct * 100)}%)", centerStyle);

                EditorGUILayout.Space(5);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_ranScan)
            {
                DrawScanResults();
            }
            else
            {
                var msgStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                { normal = { textColor = ColDim }, alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.Space(30);
                EditorGUILayout.LabelField("Click \"Scan Scene Configuration\" to inspect active setup", msgStyle, GUILayout.ExpandWidth(true));
            }

            if (_showHelp)
            {
                EditorGUILayout.Space(10);
                DrawHelpGuide();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(5);

            DrawFooter();

            EditorGUILayout.EndVertical();
        }

        private void DrawScanResults()
        {
            // 1. Rewired Input Manager
            var managerComp = DefaultSetupGenerator.FindInputManagerInScene();
            var hasManager = managerComp != null;
            DrawCardItem("Rewired Input Manager (Nativo)", 
                hasManager ? "✅ Encontrado na cena ativo." : "❌ Ausente! Mapeamentos e controles não funcionarão.",
                hasManager, "Criar Manager", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 2. Rewired Helper Component
            var hasHelper = hasManager && managerComp.GetComponent<RewiredInputManager>() != null;
            DrawCardItem("Rewired Helper (Component)",
                hasHelper ? "✅ Componente acoplado ao manager." : "❌ Ausente no manager! O roteamento de UI/Escape não funcionará.",
                hasHelper, "Configurar", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 3. Event System
            var hasEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null;
            DrawCardItem("Rewired Event System",
                hasEventSystem ? "✅ Event System encontrado na cena." : "❌ Ausente! Necessário para navegação física na UI com controle.",
                hasEventSystem, "Criar Event System", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 4. UI Canvas
            var hasCanvas = UnityEngine.Object.FindObjectOfType<Canvas>() != null;
            DrawCardItem("UI Canvas",
                hasCanvas ? "✅ Canvas de UI encontrado." : "⚠️ Recomendado ter um Canvas para cursores customizados e modais.",
                hasCanvas, "Criar Canvas", () => {
                    var go = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                    var canvas = go.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                }, isOptional: true);

            // 5. Glyphs Addon
            var hasGlyphs = FindGlyphHelperType() != null;
            DrawCardItem("Addon Oficial de Glifos do Rewired",
                hasGlyphs ? "✅ Addon de Glifos detectado no projeto." : "⚠️ Opcional. Instale para usar ícones de controle dinâmicos em textos de UI.",
                hasGlyphs, null, null, isOptional: true);
        }

        private void DrawCardItem(string title, string desc, bool pass, string fixBtnLabel, Action fixAction, bool isOptional = false)
        {
            var color = pass ? ColGreen : (isOptional ? ColOrange : ColRed);
            var cardR = EditorGUILayout.BeginVertical();
            
            // Desenhar Card com barra lateral colorida (estilo CloudSaveAudit)
            EditorGUI.DrawRect(new Rect(cardR.x - 2, cardR.y - 2, cardR.width + 4, cardR.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(cardR.x - 2, cardR.y - 2, 3, cardR.height + 4), color);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = color } };
            EditorGUILayout.LabelField(title, titleStyle);
            
            var descStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
            EditorGUILayout.LabelField(desc, descStyle);
            EditorGUILayout.EndVertical();

            if (!pass && !string.IsNullOrEmpty(fixBtnLabel) && fixAction != null)
            {
                if (GUILayout.Button(fixBtnLabel, GUILayout.Width(130), GUILayout.Height(22)))
                {
                    fixAction.Invoke();
                    _ranScan = true; // recarrega checagens
                }
            }
            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawHelpGuide()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = ColAccent } };
            GUILayout.Label("📖 Guia de Inicialização & APIs", headerStyle);
            EditorGUILayout.Space(3);

            var docStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
            
            EditorGUILayout.LabelField("• **Bootstrap** (Awake/Start):\n" +
                "  Configure o helper passando opcionalmente gerenciadores de cutscene/tutoriais, pilhas de modal ou permissões:\n" +
                "  `RewiredInputManager.Instance.Configure(uiBlocker, modalStack, helpGate);`", docStyle);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• **Uso de APIs**:\n" +
                "  - `RewiredInputManager.IsUsingTouch`: verifica se o input atual é touch.\n" +
                "  - `CurrentControllerType`: Mouse, Joystick (controle), ou Custom (touch).\n" +
                "  - `OnInputTypeChanged`: evento estático de alteração de controle.", docStyle);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            if (Btn("📖  Full Integration Guide", ColAccent))
            {
                Application.OpenURL("https://github.com/wagenheimer/RewiredHelper/blob/main/README.md");
            }
            if (Btn("📝  Create Controller Help Form", ColAccent))
            {
                DefaultSetupGenerator.CreateControllerHelpForm();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool Btn(string label, Color color)
        {
            var orig = GUI.backgroundColor;
            GUI.backgroundColor = color;
            var clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.backgroundColor = orig;
            return clicked;
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
