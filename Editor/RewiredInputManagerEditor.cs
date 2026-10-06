using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;

using TMPro;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// UI Toolkit inspector for <see cref="RewiredInputManager"/>: live status in Play Mode, a self-diagnosing
    /// "Setup Health" list with one-click fixes, the automatic per-platform pause policy, and collapsible settings.
    /// Shares its checks and theme with the Dashboard window.
    /// </summary>
    [CustomEditor(typeof(RewiredInputManager), editorForChildClasses: true)]
    public class RewiredInputManagerEditor : UnityEditor.Editor
    {
        private const long LiveRefreshMs = 250;
        private const long RefreshDebounceMs = 150;

        private RewiredInputManager _manager;
        private VisualElement _root;
        private Foldout _healthFoldout;
        private VisualElement _healthContent;
        private Label _headerBadge;
        private VisualElement _policyRows;
        private VisualElement _pauseChecks;
        private VisualElement _overrideBox;
        private Foldout _automaticContent;
        private Button _pauseScreenButton;
        private VisualElement _blockedScenesInfo;
        private VisualElement _liveCard;
        private VisualElement _onScreenKeyboardStatus;
        private Button _createKeyboardButton;
        private readonly Dictionary<string, Label> _liveValues = new Dictionary<string, Label>();
        private bool _refreshQueued;
        private bool _isSceneContext;

        public override VisualElement CreateInspectorGUI()
        {
            // If anything below throws, Unity would otherwise silently fall back to the plain reflection-based
            // inspector with no visible error — indistinguishable from "the custom editor was never built". Catch
            // it, log the full exception (with stack trace) so it's fixable, and show that failure instead of hiding it.
            try
            {
                return BuildInspector();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return BuildFallback(ex);
            }
        }

        private VisualElement BuildInspector()
        {
            _manager = (RewiredInputManager)target;

            // Scene checks (Event System, Canvas, Player Mouse...) only make sense for an object living in the open
            // scene: for a prefab asset or Prefab Mode they would report false problems and "fix" the wrong scene.
            _isSceneContext = !EditorUtility.IsPersistent(target) && PrefabStageUtility.GetCurrentPrefabStage() == null;

            _root = new VisualElement();
            _root.AddToClassList("rh-inspector");
            RewiredHelperUIStyle.Apply(_root);

            _root.Add(BuildHeader());
            _root.Add(BuildLiveCard());
            if (_isSceneContext)
            {
                BuildAutomaticSection();
                BuildHealthSection();
            }
            BuildBootSection();
            BuildCursorSection();
            BuildOnScreenKeyboardSection();
            BuildPauseSection();
            BuildControllerHelpSection();
            BuildRuntimeStateSection();
            BuildHelpSection();

            _root.Bind(serializedObject);
            _root.TrackSerializedObjectValue(serializedObject, _ => QueueRefresh());
            _root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            _root.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            _root.schedule.Execute(UpdateLive).Every(LiveRefreshMs);

            RefreshDynamic();
            return _root;
        }

        /// <summary>Plain IMGUI fields (always safe) plus the exception, so the manager stays usable and the bug is visible.</summary>
        private VisualElement BuildFallback(Exception ex)
        {
            var root = new VisualElement();
            var box = new HelpBox(
                "Rewired Helper's custom inspector failed to build and fell back to plain fields.\n" +
                $"{ex.GetType().Name}: {ex.Message}\nSee the Console for the full stack trace, and please report this.",
                HelpBoxMessageType.Error);
            root.Add(box);
            root.Add(new IMGUIContainer(() =>
            {
                serializedObject.Update();
                var prop = serializedObject.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    EditorGUILayout.PropertyField(prop, true);
                    enterChildren = false;
                }
                serializedObject.ApplyModifiedProperties();
            }));
            return root;
        }

        #region Refresh

        private void Subscribe()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Undo.undoRedoPerformed += QueueRefresh;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Undo.undoRedoPerformed -= QueueRefresh;
        }

        /// <summary>Spawns/destroys are constant while playing; the "Re-check" button covers that case.</summary>
        private void OnHierarchyChanged()
        {
            if (!Application.isPlaying) QueueRefresh();
        }

        /// <summary>Coalesces bursts of hierarchy/serialization changes into a single rebuild.</summary>
        private void QueueRefresh()
        {
            if (_refreshQueued || _root == null || _manager == null) return;

            _refreshQueued = true;
            _root.schedule.Execute(() =>
            {
                _refreshQueued = false;
                if (_manager != null) RefreshDynamic();
            }).ExecuteLater(RefreshDebounceMs);
        }

        /// <summary>
        /// Runs on every hierarchy/undo/serialization change and re-executes user-supplied checks. Unlike
        /// CreateInspectorGUI, an exception here would not fall back to a safe default — it would spam the
        /// Console on every keystroke — so it is caught and surfaced once via the header badge instead.
        /// </summary>
        private void RefreshDynamic()
        {
            try
            {
                if (_isSceneContext)
                {
                    RefreshAutomatic();
                    RefreshHealth();
                }
                RefreshPolicy();
                RefreshBlockedScenes();
                RefreshOnScreenKeyboard();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _headerBadge.text = "refresh error, see Console";
                _headerBadge.RemoveFromClassList("rh-badge-pass");
                _headerBadge.RemoveFromClassList("rh-badge-warn");
                _headerBadge.AddToClassList("rh-badge-fail");
            }
        }

        #endregion

        #region Header and live status

        private VisualElement BuildHeader()
        {
            var banner = new VisualElement();
            banner.AddToClassList("rh-header");

            var row = new VisualElement();
            row.AddToClassList("rh-header-row");

            var left = new VisualElement();
            left.AddToClassList("rh-header-left");
            left.Add(new Label("🎮") { style = { fontSize = 18, marginRight = 6 } });

            var title = new Label("Rewired Helper");
            title.AddToClassList("rh-header-title");
            left.Add(title);

            var version = new Label("v" + RewiredHelperDashboardWindow.GetPackageVersion());
            version.AddToClassList("rh-header-version");
            left.Add(version);

            _headerBadge = new Label("checking...");
            _headerBadge.AddToClassList("rh-badge");
            _headerBadge.AddToClassList("rh-badge-info");
            _headerBadge.style.marginLeft = 8;
            left.Add(_headerBadge);
            row.Add(left);

            var actions = new VisualElement();
            actions.AddToClassList("rh-toolbar-actions");
            actions.Add(RewiredHelperUIStyle.CreateButton("Dashboard", RewiredHelperDashboardWindow.OpenDashboard));
            row.Add(actions);

            banner.Add(row);

            var subtitle = new Label("Input-type detection, cursor, Escape routing and a mobile-safe pause policy on top of Rewired.");
            subtitle.AddToClassList("rh-header-subtitle");
            banner.Add(subtitle);

            return banner;
        }

        private VisualElement BuildLiveCard()
        {
            _liveCard = new VisualElement();
            _liveCard.AddToClassList("rh-card");
            _liveCard.Add(new Label("▶ LIVE STATUS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginBottom = 4 } });

            var chips = RewiredInspectorWidgets.CreateChipRow();
            foreach (var key in new[] { "Platform", "Policy", "Input", "Controller", "Paused", "System UI", "Configured", "Steam overlay" })
            {
                chips.Add(RewiredHelperUIStyle.CreateChip(key, "-", out var value));
                _liveValues[key] = value;
            }
            _liveCard.Add(chips);

            var buttons = RewiredInspectorWidgets.CreateRow("rh-chip-row");
            buttons.Add(RewiredHelperUIStyle.CreateButton("⏸ Test Pause", () => _manager.RequestPause()));
            buttons.Add(RewiredHelperUIStyle.CreateButton("▶ Resume", () => _manager.Resume()));
            buttons.Q<Button>().style.marginLeft = 0;
            _liveCard.Add(buttons);

            _liveCard.style.display = DisplayStyle.None;
            return _liveCard;
        }

        private bool _liveUpdateFailed;

        private void UpdateLive()
        {
            if (_manager == null || _liveUpdateFailed) return;

            try
            {
                bool isPlaying = Application.isPlaying;
                _liveCard.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isPlaying) return;

                SetLive("Platform", Application.platform + (RewiredInputManager.IsTelevisionDevice ? " (TV)" : string.Empty));
                SetLive("Policy", RewiredInputManager.IsMobilePolicyActive ? "mobile" : "desktop / console");
                SetLive("Input", _manager.CurrentControllerType + (RewiredInputManager.IsUsingTouch ? " (touch)" : string.Empty));
                SetLive("Controller", _manager.LastActiveController != null ? _manager.LastActiveController.name : "none");
                SetLive("Paused", _manager.IsFrozen ? "frozen" : _manager.IsPaused ? "silent" : "no");
                SetLive("System UI", RewiredInputManager.IsSystemUiActive ? "active" : "idle");
                SetLive("Configured", _manager.IsConfigured ? "yes" : "no");
                SetLive("Steam overlay", _manager.IsSteamOverlayActive ? "open" : "closed");
            }
            catch (Exception ex)
            {
                // This runs every 250 ms: stop retrying after the first failure instead of spamming the Console.
                _liveUpdateFailed = true;
                Debug.LogException(ex);
            }
        }

        private void SetLive(string key, string value)
        {
            if (_liveValues.TryGetValue(key, out var label) && label.text != value)
                label.text = value;
        }

        #endregion

        #region Automatic

        /// <summary>Lists everything the package decides by itself, so nothing looks like a manual chore.</summary>
        private void BuildAutomaticSection()
        {
            _automaticContent = RewiredInspectorWidgets.CreateSection(_root, "auto", "🤖 Automatic (no setup needed)", true,
                "Decided by the platform of each build and by the device at runtime.");
        }

        private void RefreshAutomatic()
        {
            _automaticContent.Query<VisualElement>(className: "rh-check").ForEach(e => e.RemoveFromHierarchy());

            var target = EditorUserBuildSettings.activeBuildTarget;
            var policy = PausePolicy.ForPlatform(RewiredHelperAudit.IsMobileTarget);
            bool hasSteam = RewiredHelperAudit.HasSteamManager();

            var rows = new[]
            {
                ("Pause policy for the build platform",
                 $"Build target {target}: app background = {policy.AppBackground}, pause on controller disconnect = {OnOff(policy.PauseOnControllerDisconnect)}, " +
                 $"tap to resume = {OnOff(policy.ResumeOnAnyInput)}. Android TV / Fire TV devices switch to the desktop policy at runtime."),
                ("Steam overlay",
                 hasSteam ? "SteamManager found: the overlay pauses the game and resumes it when it closes."
                          : "No SteamManager in the project: nothing to configure (only used on Steam builds)."),
                ("Cursor per input device",
                 "Touch: hidden. Mouse/keyboard: OS cursor (custom texture on standalone). Gamepad/remote: on-screen Game Cursor."),
                ("Controller help",
                 "Shown once, the first time a gamepad button is pressed in an allowed scene. Never on touch."),
                ("Every build",
                 "The Unity Build Pipeline, Build Settings and CLI builds log the policy in use and warn about overrides that would break mobile."),
                ("Steam Deck suspend/resume",
                 "Detected via IsSteamRunningOnSteamDeck; OnApplicationFocus also drives the app-background pause so suspend/resume behaves like the classic desktop pause (needs Game Paused assigned)."),
                ("Gamepad text input",
                 "A RewiredOnScreenKeyboard in the scene is shown/hidden automatically for any selected TMP_InputField — required for Steam Deck Verified.")
            };

            foreach (var (title, detail) in rows)
                _automaticContent.Add(RewiredInspectorWidgets.CreateAutomaticRow(title, detail));
        }

        #endregion

        #region Health

        private void BuildHealthSection()
        {
            _healthFoldout = RewiredInspectorWidgets.CreateSection(_root, "health", "🛠 Setup Health", true,
                "Checks this scene and offers a one-click fix for whatever is missing.");

            var toolbar = RewiredInspectorWidgets.CreateRow("rh-chip-row");
            var refresh = RewiredHelperUIStyle.CreateButton("↻ Re-check", RefreshDynamic);
            refresh.style.marginLeft = 0;
            toolbar.Add(refresh);
            toolbar.Add(RewiredHelperUIStyle.CreateButton("🤖 Copy AI Fix Prompt", CopyFixPrompt));
            toolbar.Add(RewiredHelperUIStyle.CreateButton("🔍 Full Audit", RewiredHelperDashboardWindow.OpenAuditTab));
            _healthFoldout.Add(toolbar);

            _healthContent = new VisualElement { style = { marginTop = 4 } };
            _healthFoldout.Add(_healthContent);
        }

        private List<AuditResult> CollectSceneChecks()
        {
            var results = new List<AuditResult>();
            RewiredSetupChecks.Run(results, _manager);
            return results;
        }

        private void RefreshHealth()
        {
            var results = CollectSceneChecks();
            RewiredInspectorWidgets.FillChecks(_healthContent, results, RefreshDynamic);

            int fails = results.Count(r => r.Severity == AuditSeverity.Fail);
            int warnings = results.Count(r => r.Severity == AuditSeverity.Warning);
            int issues = fails + warnings;

            _healthFoldout.text = issues == 0 ? "🛠 Setup Health" : $"🛠 Setup Health  ({issues} to review)";
            UpdateHeaderBadge(fails, warnings);
        }

        private void UpdateHeaderBadge(int fails, int warnings)
        {
            _headerBadge.RemoveFromClassList("rh-badge-pass");
            _headerBadge.RemoveFromClassList("rh-badge-warn");
            _headerBadge.RemoveFromClassList("rh-badge-fail");

            if (fails > 0)
            {
                _headerBadge.text = $"{fails} problem(s)";
                _headerBadge.AddToClassList("rh-badge-fail");
            }
            else if (warnings > 0)
            {
                _headerBadge.text = $"{warnings} warning(s)";
                _headerBadge.AddToClassList("rh-badge-warn");
            }
            else
            {
                _headerBadge.text = "All good";
                _headerBadge.AddToClassList("rh-badge-pass");
            }
        }

        private void CopyFixPrompt()
        {
            var results = CollectSceneChecks();
            RewiredHelperAudit.AttachPrompts(results);
            GUIUtility.systemCopyBuffer = RewiredHelperAudit.ToPromptMarkdown(results);
            _healthFoldout.text = "🛠 Setup Health  (prompt copied)";
        }

        #endregion

        #region Settings sections

        private void BuildBootSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "boot", "🚀 Initialization & Boot", false,
                "Zero-code default: the manager configures itself on Start. Turn it off to call Configure()/SetGlobalConfiguration from your bootstrap.");
            AddFields(section, "AutoConfigureOnStart", "UseDefaultModalStack", "AutoBridgeSubmitToPointerDown");
        }

        private void BuildCursorSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "cursor", "🖱 Cursor & Glyphs", false);
            AddFields(section, "GameCursor", "CustomCursorEnabled", "CursorTexture", "ForceGlyphCustomControllerLast");
        }

        private void BuildOnScreenKeyboardSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "osk", "⌨ On-Screen Keyboard", false,
                "Shown automatically when a TMP_InputField is selected with a gamepad. Touch uses the OS keyboard; mouse/keyboard needs none.");
            AddFields(section, "ShowOnScreenKeyboardOnGamepadTextInput");

            // The scene status / create button only make sense for an object in the open scene, like the Setup Health section.
            if (!_isSceneContext) return;

            _onScreenKeyboardStatus = new VisualElement { style = { marginTop = 4 } };
            section.Add(_onScreenKeyboardStatus);

            _createKeyboardButton = RewiredHelperUIStyle.CreateButton("⌨ Create On-Screen Keyboard", () =>
            {
                DefaultSetupGenerator.CreateOnScreenKeyboardAndWire();
                RefreshDynamic();
            });
            _createKeyboardButton.style.marginLeft = 0;
            _createKeyboardButton.style.marginTop = 4;
            _createKeyboardButton.style.alignSelf = Align.FlexStart;
            _createKeyboardButton.tooltip = "Builds a plain QWERTY + digits keyboard from code and registers it; shown automatically for any TMP_InputField selected with a gamepad.";
            section.Add(_createKeyboardButton);
        }

        private void RefreshOnScreenKeyboard()
        {
            if (_onScreenKeyboardStatus == null) return;

            _onScreenKeyboardStatus.Clear();

            int inputFields = RewiredHelperAudit.FindAll<TMP_InputField>().Count;
            bool hasKeyboard = DefaultSetupGenerator.FindOnScreenKeyboardInScene() != null;

            if (inputFields == 0)
            {
                _onScreenKeyboardStatus.Add(RewiredHelperUIStyle.CreateCallout(
                    "No TMP_InputField in this scene yet — the on-screen keyboard has nothing to type into.", AuditSeverity.Info));
            }
            else if (hasKeyboard)
            {
                _onScreenKeyboardStatus.Add(RewiredHelperUIStyle.CreateCallout(
                    $"On-screen keyboard found — covers all {inputFields} TMP_InputField(s) in the scene.", AuditSeverity.Pass));
            }
            else
            {
                _onScreenKeyboardStatus.Add(RewiredHelperUIStyle.CreateCallout(
                    $"{inputFields} TMP_InputField(s) in the scene but no RewiredOnScreenKeyboard — a gamepad / Steam Deck player has no way to type.", AuditSeverity.Warning));
            }

            if (_createKeyboardButton != null)
                _createKeyboardButton.style.display = hasKeyboard ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BuildControllerHelpSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "help", "❓ Controller Help", false,
                "Shown once, the first time a physical controller is used, unless the scene is blocked.");
            AddFields(section, "OnShowControllerHelp", "ControllerHelpBlockedScenes");

            _blockedScenesInfo = new VisualElement();
            section.Add(_blockedScenesInfo);

            var create = RewiredHelperUIStyle.CreateButton("📝 Create Controller Help Form", DefaultSetupGenerator.CreateControllerHelpForm);
            create.style.marginLeft = 0;
            create.style.alignSelf = Align.FlexStart;
            section.Add(create);
        }

        private void RefreshBlockedScenes()
        {
            _blockedScenesInfo.Clear();
            if (_manager.ControllerHelpBlockedScenes.Count == 0) return;

            var inBuild = new HashSet<string>(
                EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)), StringComparer.OrdinalIgnoreCase);
            var unknown = _manager.ControllerHelpBlockedScenes.Where(n => !string.IsNullOrEmpty(n) && !inBuild.Contains(n)).ToList();
            if (unknown.Count == 0) return;

            _blockedScenesInfo.Add(RewiredHelperUIStyle.CreateCallout(
                "Not in Build Settings (typo or removed scene): " + string.Join(", ", unknown), AuditSeverity.Warning));
        }

        private void BuildRuntimeStateSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "state", "⚙ Runtime State", false, "Read-only, driven by the game at runtime.");
            AddFields(section, "alreadyShowedControllerHelp", "IsSteamOverlayActive");
            section.SetEnabled(false);
        }

        private void AddFields(VisualElement parent, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                var property = serializedObject.FindProperty(name);
                if (property == null) continue;

                parent.Add(new PropertyField(property));
            }
        }

        #endregion

        #region Pause policy

        private void BuildPauseSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "pause", "⏸ Pause Policy", true,
                "Chosen automatically from the platform of each build (Unity Build Pipeline, Build Settings, CLI). Nothing to configure.");

            _policyRows = new VisualElement();
            section.Add(_policyRows);

            _pauseChecks = new VisualElement { style = { marginTop = 4 } };
            section.Add(_pauseChecks);

            var advanced = new Foldout { text = "🔧 Advanced (rarely needed)", value = SessionState.GetBool("RewiredHelper.Inspector.pauseAdvanced", false) };
            advanced.style.marginTop = 4;
            advanced.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == advanced) SessionState.SetBool("RewiredHelper.Inspector.pauseAdvanced", evt.newValue);
            });
            advanced.Add(new Label("Leave these alone unless you need behavior that differs from the automatic policy.")
            {
                style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            });
            AddFields(advanced, "OverridePlatformDefaults");

            _overrideBox = new VisualElement();
            foreach (var name in new[] { "PauseOnAppBackground", "PauseOnControllerDisconnect", "ResumeOnAnyInput" })
                _overrideBox.Add(new PropertyField(serializedObject.FindProperty(name)));
            advanced.Add(_overrideBox);

            AddFields(advanced, "PauseOnSteamOverlay", "GamePaused");
            advanced.Add(BuildPauseButtons());
            section.Add(advanced);
        }

        private static bool NeedsPauseScreenFix(RewiredInputManager manager) =>
            RewiredHelperAudit.CanFreezeOnSomePlatform(manager) && (manager.GamePaused == null || !RewiredHelperAudit.HasResumeButton(manager));

        /// <summary>
        /// Only offered while there is no pause screen with a Resume button. The automatic policy on mobile never
        /// freezes the game, so this matters for the desktop/console overlay and for manual RequestPause().
        /// </summary>
        private VisualElement BuildPauseButtons()
        {
            _pauseScreenButton = RewiredHelperUIStyle.CreateButton("🛠 Create / complete Pause Screen",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(_manager, new SerializedObject(_manager)));
            _pauseScreenButton.style.marginLeft = 0;
            _pauseScreenButton.style.marginTop = 4;
            _pauseScreenButton.style.alignSelf = Align.FlexStart;
            _pauseScreenButton.tooltip = "Creates a pause screen with a Resume button, or adds the Resume button to the one assigned to Game Paused.";
            return _pauseScreenButton;
        }

        private void RefreshPolicy()
        {
            _overrideBox.style.display = _manager.OverridePlatformDefaults ? DisplayStyle.Flex : DisplayStyle.None;
            _pauseScreenButton.style.display = NeedsPauseScreenFix(_manager) ? DisplayStyle.Flex : DisplayStyle.None;

            _policyRows.Clear();
            bool activeIsMobile = RewiredHelperAudit.IsMobileTarget;
            _policyRows.Add(CreatePolicyRow("Android / iOS", true, activeIsMobile));
            _policyRows.Add(CreatePolicyRow("Desktop / Console", false, !activeIsMobile));

            var mobileChecks = new List<AuditResult>();
            RewiredHelperAudit.AuditMobilePause(mobileChecks, _manager);
            RewiredInspectorWidgets.FillChecks(_pauseChecks, mobileChecks, RefreshDynamic);
        }

        private VisualElement CreatePolicyRow(string platform, bool isMobile, bool isActiveTarget)
        {
            var policy = PausePolicy.Resolve(_manager.OverridePlatformDefaults, _manager.PauseOnAppBackground,
                _manager.PauseOnControllerDisconnect, _manager.ResumeOnAnyInput, isMobile);

            var row = RewiredInspectorWidgets.CreateRow("rh-policy-row");
            var name = new Label(isActiveTarget ? platform + "  ●" : platform);
            name.AddToClassList("rh-policy-name");
            name.tooltip = isActiveTarget ? "Active build target" : null;
            row.Add(name);

            var chips = RewiredInspectorWidgets.CreateChipRow();
            chips.Add(RewiredHelperUIStyle.CreateChip("background", policy.AppBackground.ToString().ToLowerInvariant(), out _,
                policy.AppBackground == AppBackgroundPauseMode.Overlay ? "rh-chip-warn" : "rh-chip-on"));
            chips.Add(RewiredHelperUIStyle.CreateChip("disconnect pause", OnOff(policy.PauseOnControllerDisconnect), out _,
                policy.PauseOnControllerDisconnect ? "rh-chip-off" : "rh-chip-on"));
            chips.Add(RewiredHelperUIStyle.CreateChip("tap to resume", OnOff(policy.ResumeOnAnyInput), out _, "rh-chip-off"));
            if (_manager.OverridePlatformDefaults)
                chips.Add(RewiredHelperUIStyle.CreateChip("", "overridden", out _, "rh-chip-warn"));
            row.Add(chips);
            return row;
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        #endregion

        #region Help

        private void BuildHelpSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(_root, "quickhelp", "📖 Quick Help", false);

            section.Add(new Label("Bootstrap (all arguments optional)") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            section.Add(RewiredHelperUIStyle.CreateCodeBox(
                "RewiredInputManager.SetGlobalConfiguration(\n" +
                "    uiBlocker: myBlocker,\n" +
                "    modalStack: new DefaultModalStackProvider(),\n" +
                "    controllerHelpGate: myGate,\n" +
                "    pauseGate: myPauseGate);"));

            section.Add(new Label("Ads / IAP / share sheets") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            section.Add(RewiredHelperUIStyle.CreateCodeBox(
                "using (RewiredInputManager.BeginSystemUi()) { ShowInterstitial(); }\n" +
                "RewiredInputManager.OnPauseChanged += (paused, frozen) => Music.SetPaused(paused);"));

            var routing = RewiredHelperUIStyle.CreateCallout(
                "• EscapeButton: the active button with the highest priority answers Escape / Back / Menu.\n" +
                "• ReturnEscapeEvent: fires when Escape/Return are pressed and no button or modal claims them.\n" +
                "• IModalStackProvider: the top-most modal gains routing priority.\n" +
                "• IsUsingTouch, CurrentControllerType, OnInputTypeChanged: react to the active input source.",
                AuditSeverity.Info);
            section.Add(routing);
        }

        #endregion
    }
}
