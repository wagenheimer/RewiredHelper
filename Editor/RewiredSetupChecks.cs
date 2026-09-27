using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Scene-setup checks shared by the Dashboard audit and the manager's Inspector, so both always agree.
    /// Every failing check carries a one-click fix when one exists.
    /// </summary>
    internal static class RewiredSetupChecks
    {
        private const string Category = "Scene Setup";
        private const string GlyphHelperTypeName = "Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper";
        private const string I2SpecializationPath = "Assets/I2/Localization/Scripts/Configurables/SpecializationManager.cs";

        public static void Run(List<AuditResult> results, RewiredInputManager manager)
        {
            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            RewiredHelperAudit.Add(results, Category, "Rewired Input Manager in the scene", inputManager != null,
                "Found in the open scene.", "Missing: no Rewired input will be processed.",
                "Creates Rewired's native Input Manager GameObject with a default configuration.",
                "Create Manager", DefaultSetupGenerator.CreateRewiredInputManager);

            RewiredHelperAudit.Add(results, Category, "RewiredInputManager component", manager != null,
                "Component present.", "Missing: input-type tracking, cursor and Escape routing will not work.",
                "Add RewiredInputManager next to the Rewired Input Manager.", "Configure", DefaultSetupGenerator.CreateRewiredInputManager);

            RewiredHelperAudit.Add(results, Category, "Event System uses Rewired's input module", DefaultSetupGenerator.HasRewiredEventSystemInScene(),
                "RewiredStandaloneInputModule active.",
                "Missing: controller UI navigation and Player Mouse will not work (Unity's default module ignores Rewired).",
                "Creates an Event System using Rewired's RewiredStandaloneInputModule.", "Create Event System",
                DefaultSetupGenerator.EnsureRewiredEventSystem);

            CheckDuplicateEventSystems(results);
            CheckCanvas(results);

            if (manager != null)
            {
                CheckCursor(results, manager);
                CheckPlayerMouse(results, manager);
                CheckRuntimeConfiguration(results, manager);
            }

            CheckGlyphs(results, manager);
            CheckI2(results);
        }

        #region Core

        private static void CheckDuplicateEventSystems(List<AuditResult> results)
        {
            int count = RewiredHelperAudit.FindAll<EventSystem>().Count;
            RewiredHelperAudit.Add(results, Category, "Single Event System", count <= 1,
                "No duplicate Event Systems in the open scene.",
                $"{count} Event Systems in the open scene: Unity logs 'There can be only one active Event System'.",
                "Keeps only the Event System running Rewired's input module, in every scene of the project.",
                "Remove Duplicates (All Scenes)", DefaultSetupGenerator.RemoveDuplicateEventSystemsInAllScenes, AuditSeverity.Warning);
        }

        private static void CheckCanvas(List<AuditResult> results)
        {
            RewiredHelperAudit.Add(results, Category, "UI Canvas", RewiredHelperAudit.FindAll<Canvas>().Count > 0,
                "Canvas found.", "No Canvas in the open scene (needed for the game cursor and modal dialogs).",
                "Creates a Screen Space - Overlay Canvas with a GraphicRaycaster.", "Create Canvas", CreateCanvas, AuditSeverity.Warning);
        }

        private static void CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        }

        private static void CheckCursor(List<AuditResult> results, RewiredInputManager manager)
        {
            // Touch-only titles never draw a game cursor, so a missing one is only a hint on mobile.
            var missing = RewiredHelperAudit.IsMobileTarget ? AuditSeverity.Info : AuditSeverity.Warning;
            RewiredHelperAudit.Add(results, Category, "Game Cursor (controller/remote cursor image)", manager.GameCursor != null,
                "Game Cursor image assigned.", "Not assigned: gamepad/remote users get no on-screen cursor.",
                "Creates a Game Cursor Image under the Canvas (hidden until joystick input), adds GameCursorPositioner and links it here.",
                "Create Game Cursor", () => DefaultSetupGenerator.CreateGameCursorAndWire(manager, new SerializedObject(manager)), missing);

            if (manager.CustomCursorEnabled)
            {
                RewiredHelperAudit.Add(results, Category, "Custom cursor texture assigned", manager.CursorTexture != null,
                    "Cursor Texture assigned.", "Custom Cursor Enabled is on, but no Cursor Texture is assigned.",
                    "Assign a Read/Write, uncompressed, no-mipmap texture to Cursor Texture.", failSeverity: AuditSeverity.Warning);
            }
        }

        private static void CheckRuntimeConfiguration(List<AuditResult> results, RewiredInputManager manager)
        {
            if (!EditorApplication.isPlaying) return;

            RewiredHelperAudit.Add(results, Category, "Runtime: Configure() called", manager.IsConfigured,
                "Initialized and configured.", "Configure() has not run in this session.",
                "Enable Auto Configure On Start, or call RewiredInputManager.SetGlobalConfiguration(...) from your bootstrap.",
                failSeverity: AuditSeverity.Fail);
        }

        #endregion

        #region Player Mouse

        /// <summary>Only offers the action that is actually missing instead of re-offering to recreate what exists.</summary>
        private static void CheckPlayerMouse(List<AuditResult> results, RewiredInputManager manager)
        {
            var severity = RewiredHelperAudit.IsMobileTarget ? AuditSeverity.Info : AuditSeverity.Warning;
            var playerMouseType = DefaultSetupGenerator.FindPlayerMouseType();
            var playerMouse = DefaultSetupGenerator.FindPlayerMouseInScene(playerMouseType);

            const string title = "Player Mouse (joystick cursor movement)";
            if (playerMouse == null)
            {
                RewiredHelperAudit.Add(results, Category, title, false, null,
                    "Rewired's Player Mouse drives the pointer from controller input. Without it the cursor shows for a joystick but never moves.",
                    "Creates Rewired's PlayerMouse and wires it to move the Game Cursor from joystick input.",
                    "Create Player Mouse", () => DefaultSetupGenerator.CreatePlayerMouseAndWire(manager), severity);
                return;
            }

            using var serialized = new SerializedObject(playerMouse);
            int configured = DefaultSetupGenerator.CountConfiguredMouseElements(serialized);
            if (configured < 0)
            {
                // Different Rewired version: don't claim a false failure.
                results.Add(new AuditResult { Category = Category, Title = title, Severity = AuditSeverity.Info,
                    Detail = "Detected, but its Elements list could not be inspected: verify a horizontal/vertical axis is bound." });
                return;
            }

            RewiredHelperAudit.Add(results, Category, title, configured > 0,
                "Player Mouse has movement elements.", "Player Mouse exists but has no axis bound, so the cursor stays fixed.",
                "Binds the horizontal/vertical movement axes to Player Mouse's Elements list.",
                "Auto-Configure Elements", () => DefaultSetupGenerator.ConfigureMouseMovementElements(playerMouse), severity);

            if (configured > 0 && manager.GameCursor != null)
                CheckCursorWiring(results, manager, playerMouse, serialized, severity);
        }

        private static void CheckCursorWiring(List<AuditResult> results, RewiredInputManager manager, Component playerMouse,
            SerializedObject playerMouseSerialized, AuditSeverity severity)
        {
            var positioner = manager.GameCursor.GetComponent<UI.GameCursorPositioner>();
            RewiredHelperAudit.Add(results, Category, "Game Cursor Positioner", positioner != null,
                "GameCursorPositioner present.",
                "Without it the cursor only lines up on a Screen Space - Overlay Canvas with a 1:1 Canvas Scaler.",
                "Adds GameCursorPositioner to the Game Cursor.", "Add Positioner",
                () => Undo.AddComponent<UI.GameCursorPositioner>(manager.GameCursor.gameObject), severity);

            bool positionWired = IsEventWiredTo(playerMouseSerialized.FindProperty("_onScreenPositionChanged"), positioner, "SetScreenPosition");
            bool enabledWired = IsEventWired(playerMouseSerialized.FindProperty("_onEnabledStateChanged"));
            RewiredHelperAudit.Add(results, Category, "Player Mouse events wired to the cursor", positionWired && enabledWired,
                "Position and visibility events are wired.",
                positionWired
                    ? "On Enabled State Changed is not wired to control the Game Cursor's visibility."
                    : "On Screen Position Changed is not wired to GameCursorPositioner.SetScreenPosition (the cursor may drift off-screen).",
                "Rewires both events to the Game Cursor.", "Fix Wiring",
                () => DefaultSetupGenerator.WirePlayerMouseEvents(manager, playerMouse), severity);
        }

        private static bool IsEventWiredTo(SerializedProperty eventProp, UnityEngine.Object target, string methodName)
        {
            var calls = eventProp?.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null || target == null) return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target").objectReferenceValue == target &&
                    call.FindPropertyRelative("m_MethodName").stringValue == methodName)
                    return true;
            }
            return false;
        }

        private static bool IsEventWired(SerializedProperty eventProp)
        {
            var calls = eventProp?.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null) return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target").objectReferenceValue != null &&
                    !string.IsNullOrEmpty(call.FindPropertyRelative("m_MethodName").stringValue))
                    return true;
            }
            return false;
        }

        #endregion

        #region Glyphs and I2

        private static void CheckGlyphs(List<AuditResult> results, RewiredInputManager manager)
        {
            bool hasGlyphs = FindType(GlyphHelperTypeName) != null;
            RewiredHelperAudit.Add(results, Category, "Rewired Glyphs add-on", hasGlyphs,
                "Detected in the project.", "Not installed: UI labels cannot show dynamic controller icons.",
                "Extract Assets/Rewired/Internal/Assets/Extras/GlyphsUnityUITMProAddonV2.zip (optional).", failSeverity: AuditSeverity.Info);
            if (!hasGlyphs) return;

            var providerType = DefaultSetupGenerator.FindGlyphProviderType();
            if (manager != null)
            {
                bool hasProvider = providerType != null && manager.GetComponent(providerType) != null;
                RewiredHelperAudit.Add(results, Category, "Glyph Provider", hasProvider,
                    "Glyph Provider present.",
                    "Without a Glyph Provider every <rewiredElement> glyph tag falls back to plain text instead of an icon.",
                    "Adds Rewired's GlyphProvider component to the Input Manager.", "Add Glyph Provider",
                    () => DefaultSetupGenerator.EnsureGlyphProvider(manager.gameObject), AuditSeverity.Warning);
            }

            CheckAndroidRemoteGlyphs(results);
        }

        private static void CheckAndroidRemoteGlyphs(List<AuditResult> results)
        {
            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            if (inputManager == null || !AndroidRemoteGlyphSetup.HasAndroidCustomController(inputManager)) return;

            RewiredHelperAudit.Add(results, Category, "Android Remote glyphs ready", AndroidRemoteGlyphSetup.IsAndroidRemoteGlyphSetReady(inputManager),
                "Glyph set generated.",
                "AndroidController exists but has no registered glyph set: TV remote users see raw names ('Left', 'Escape').",
                "Fills element keys, generates the SpriteGlyphSet with Rewired's generator, copies matching sprites, registers it and reloads the provider.",
                "Ensure Android Remote Glyphs", () => AndroidRemoteGlyphSetup.EnsureAndroidRemoteGlyphs(), AuditSeverity.Warning);
        }

        private static void CheckI2(List<AuditResult> results)
        {
            if (FindType("I2.Loc.LocalizationManager") == null) return;

            if (File.Exists(I2SpecializationPath) && !File.ReadAllText(I2SpecializationPath).Contains("partial class SpecializationManager"))
            {
                results.Add(new AuditResult
                {
                    Category = Category, Title = "I2 SpecializationManager is partial", Severity = AuditSeverity.Fail,
                    Detail = $"'{I2SpecializationPath}' must declare 'public partial class SpecializationManager' so the Rewired Helper integration compiles.",
                    FixHint = "Add the 'partial' keyword to the class declaration."
                });
                return;
            }

            bool hasIntegration = FindType("Wagenheimer.RewiredHelper.Integration.I2SpecializationImportedMarker") != null;
            RewiredHelperAudit.Add(results, Category, "I2 Localization integration imported", hasIntegration,
                "Integration sample is imported.", "I2 Localization detected, but the specialization helper sample is not imported.",
                "Copies the SpecializationManager.cs sample into Assets/Samples so translated texts render controller glyphs.",
                "Import Integration", I2IntegrationImporter.Import, AuditSeverity.Warning);

            bool termsOk = DefaultSetupGenerator.AllI2TermsExist(out int missingCount, out var missingTerms);
            RewiredHelperAudit.Add(results, Category, "I2 Localization terms", termsOk,
                "All Rewired Helper terms exist.", DescribeMissingTerms(missingCount, missingTerms),
                "Checks every term Rewired Helper needs and adds the missing ones with an English translation.",
                "Verify/Add Terms", DefaultSetupGenerator.EnsureI2Terms, AuditSeverity.Warning);
        }

        private static string DescribeMissingTerms(int missingCount, List<string> missingTerms)
        {
            var sb = new StringBuilder(missingCount == 1
                ? "1 term is missing an English translation in the I2 Language Source:"
                : $"{missingCount} terms are missing or have no English translation in the I2 Language Source:");
            foreach (var term in DefaultSetupGenerator.GetRequiredI2Terms().Keys)
                sb.Append('\n').Append(missingTerms != null && missingTerms.Contains(term) ? "❌ " : "✅ ").Append(term);
            return sb.ToString();
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); }
                catch { continue; }
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
