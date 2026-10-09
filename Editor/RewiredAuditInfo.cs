using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.EventSystems;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>What a finding is, the code it relates to and the object to ping, so each row explains itself.</summary>
    internal readonly struct AuditInfo
    {
        public readonly string About;
        public readonly string Code;
        public readonly Func<UnityEngine.Object> Ping;

        public AuditInfo(string about, string code = null, Func<UnityEngine.Object> ping = null)
        {
            About = about;
            Code = code;
            Ping = ping;
        }
    }

    /// <summary>
    /// One place that documents every Setup Audit finding (matched by its title): a plain explanation, the code or Inspector shape that
    /// satisfies it, and which scene object / asset to ping. The Audit tab and the RewiredInputManager Inspector both render it.
    /// </summary>
    internal static class RewiredAuditInfo
    {
        private static UnityEngine.Object Manager() => RewiredHelperAudit.FindAll<RewiredInputManager>().FirstOrDefault();
        private static UnityEngine.Object RewiredInputManagerObject() => DefaultSetupGenerator.FindInputManagerInScene();
        private static UnityEngine.Object CanvasObject() => RewiredHelperAudit.FindAll<Canvas>().FirstOrDefault();
        private static UnityEngine.Object FirstEventSystem() => RewiredHelperAudit.FindAll<EventSystem>().FirstOrDefault();

        private static UnityEngine.Object RewiredModule()
        {
            var type = DefaultSetupGenerator.FindRewiredInputModuleType();
            return type != null ? UnityEngine.Object.FindObjectOfType(type) : null;
        }

        private static UnityEngine.Object GameCursorObject() => (Manager() as RewiredInputManager)?.GameCursor;

        private static UnityEngine.Object PlayerMouseObject() =>
            DefaultSetupGenerator.FindPlayerMouseInScene(DefaultSetupGenerator.FindPlayerMouseType());

        private static UnityEngine.Object KeyboardObject() => DefaultSetupGenerator.FindOnScreenKeyboardInScene();

        private static UnityEngine.Object PauseScreen() => (Manager() as RewiredInputManager)?.GamePaused;

        private static readonly Dictionary<string, AuditInfo> Entries = new Dictionary<string, AuditInfo>(StringComparer.Ordinal)
        {
            // ---- Scene Setup
            ["Rewired Input Manager in the scene"] = new AuditInfo(
                "Rewired's own Input Manager object. It reads every device and holds the actions and controller maps; without it no input is processed at all.",
                "var player = Rewired.ReInput.players.GetPlayer(0);\nbool pressed = player.GetButtonDown(\"UISubmit\");", RewiredInputManagerObject),

            ["RewiredInputManager component"] = new AuditInfo(
                "Rewired Helper's manager. It tells which input type is active (keyboard, mouse, touch, gamepad), drives the cursor, routes Escape/Return and applies the pause policy.",
                "RewiredInputManager.Instance.CurrentControllerType;   // Keyboard, Mouse, Joystick, Custom\nRewiredInputManager.IsUsingTouch;", Manager),

            ["Event System uses Rewired's input module"] = new AuditInfo(
                "Unity's default input module ignores Rewired. RewiredStandaloneInputModule turns Rewired actions into UI navigation, clicks and the joystick-driven cursor.",
                "EventSystem (GameObject)\n  ├─ EventSystem\n  └─ RewiredStandaloneInputModule   // not StandaloneInputModule", RewiredModule),

            ["Rewired actions used by the Event System exist"] = new AuditInfo(
                "The input module reads four named actions. If the Input Manager does not define them, gamepad and keyboard menu navigation and Submit/Cancel silently do nothing.",
                "UIHorizontal  Axis    left stick, D-pad, arrow keys\nUIVertical    Axis    left stick, D-pad, arrow keys\nUISubmit      Button  A, Enter\nUICancel      Button  B, Escape", RewiredInputManagerObject),

            ["Single Event System"] = new AuditInfo(
                "Unity supports one active Event System. A second one (often auto-created by a UI Toolkit overlay) can become the active one and leave Rewired's module unused.",
                "// Unity: \"There can be only one active Event System.\"\n// RewiredInputManager removes foreign ones at runtime once a Rewired Event System exists.", FirstEventSystem),

            ["UI Canvas"] = new AuditInfo(
                "The game cursor and modal dialogs are drawn on a Canvas.",
                "Canvas (Screen Space - Overlay)\n  └─ GraphicRaycaster", CanvasObject),

            ["Game Cursor (controller/remote cursor image)"] = new AuditInfo(
                "The on-screen pointer a gamepad or TV remote moves. It stays hidden until joystick input is detected.",
                "public Image GameCursor;   // RewiredInputManager, raycastTarget = false", GameCursorObject),

            ["Custom cursor texture assigned"] = new AuditInfo(
                "Custom Cursor Enabled replaces the OS cursor with your texture.",
                "CursorTexture: Read/Write enabled, uncompressed, no mipmaps", () => (Manager() as RewiredInputManager)?.CursorTexture),

            ["Using the legacy RewiredHelper component"] = new AuditInfo(
                "An older class name kept for compatibility. It works, but migrating gives you the current inspector and Dashboard.",
                "// global::RewiredHelper  ->  Wagenheimer.RewiredHelper.RewiredInputManager", Manager),

            ["Runtime: Configure() called"] = new AuditInfo(
                "At runtime the manager must be configured (automatically on Start, or from your bootstrap).",
                "RewiredInputManager.SetGlobalConfiguration(\n    uiBlocker: myBlocker,\n    modalStack: new DefaultModalStackProvider());", Manager),

            ["Player Mouse (joystick cursor movement)"] = new AuditInfo(
                "Rewired's PlayerMouse converts stick input into pointer movement. Without it the cursor shows but never moves.",
                "Rewired.Components.PlayerMouse\n  Elements > Movement > Horizontal (MouseX), Vertical (MouseY)", PlayerMouseObject),

            ["Game Cursor Positioner"] = new AuditInfo(
                "Places the cursor image where PlayerMouse says the pointer is, converting through the Canvas.",
                "GameCursorPositioner.SetScreenPosition(Vector2 screenPosition);", GameCursorObject),

            ["Player Mouse events wired to the cursor"] = new AuditInfo(
                "PlayerMouse raises events when the pointer moves or is enabled; they must call the positioner and show/hide the cursor.",
                "PlayerMouse.OnScreenPositionChanged -> GameCursorPositioner.SetScreenPosition\nPlayerMouse.OnPointerEnabledChanged  -> GameCursor.enabled", PlayerMouseObject),

            ["On-screen keyboard uses an outdated generated layout"] = new AuditInfo(
                "The keyboard in the scene was generated by an older version and keeps its old spacing and colours. Upgrading replaces it with the current QWERTY layout.",
                "RewiredOnScreenKeyboard.layoutVersion < DefaultSetupGenerator.KeyboardLayoutVersion", KeyboardObject),

            ["On-Screen Keyboard for gamepad text input"] = new AuditInfo(
                "A gamepad or Steam Deck player needs a way to type (required by Valve's Deck Verified). One keyboard in the scene covers every TMP_InputField; it opens by itself on Steam Deck.",
                "RewiredInputManager.ShowOnScreenKeyboardOnGamepadTextInput = true;\nRewiredInputManager.OnScreenKeyboardOnlyOnSteamDeck = true;   // Editor always shows it", KeyboardObject),

            ["Steamworks.NET supports Steam Deck detection"] = new AuditInfo(
                "Steam Deck detection (suspend/resume handling, keyboard) needs a Steamworks.NET with SteamUtils.IsRunningOnSteamHardware.",
                "SteamUtils.IsRunningOnSteamHardware() == ESteamHardwareType.k_ESteamHardwareTypeSteamDeck"),

            ["Rewired Glyphs add-on"] = new AuditInfo(
                "Rewired's glyph add-on supplies the controller button icons shown in the controller help form.",
                "Assets/Rewired/Extras/Glyphs"),

            ["Glyph Provider"] = new AuditInfo(
                "A provider tells the glyph helpers which icon set to use.",
                "Rewired.Glyphs.UnityUI.UnityUIControllerElementGlyphHelper"),

            ["Android Remote glyphs ready"] = new AuditInfo(
                "Android TV / Fire TV remotes use a custom controller; its buttons need glyphs too.",
                "Tools > Wagenheimer > Rewired Helper > Setup > Ensure Android Remote Glyphs", RewiredInputManagerObject),

            ["I2 SpecializationManager is partial"] = new AuditInfo(
                "The I2 integration extends I2's SpecializationManager, which must be declared partial.",
                "public partial class SpecializationManager : MonoBehaviour"),

            ["I2 Localization integration imported"] = new AuditInfo(
                "Optional: localises the controller help texts through I2.",
                "Assets/Wagenheimer/RewiredHelper/I2Integration"),

            ["I2 Localization terms"] = new AuditInfo(
                "The terms the controller help form uses must exist in your I2 sources.",
                "I2.Loc.LocalizationManager.GetTranslation(\"RewiredHelper/...\")"),

            // ---- Mobile & Pause
            ["Pause policy"] = new AuditInfo(
                "The pause behaviour is chosen from the platform of each build; nothing to configure.",
                "var policy = PausePolicy.ForPlatform(isMobile);", Manager),

            ["Pause policy is automatic for the build platform"] = new AuditInfo(
                "Overriding the automatic policy turns off the per-platform defaults.",
                "RewiredInputManager.OverridePlatformDefaults = false;", Manager),

            ["Overridden app-background pause is safe on mobile"] = new AuditInfo(
                "On mobile the OS suspends the app, and ads/IAP sheets also fire OnApplicationPause, so an overlay pause would pop up constantly.",
                "PauseOnAppBackground = Silent;   // mobile default", Manager),

            ["Overridden controller-disconnect pause is safe on mobile"] = new AuditInfo(
                "Bluetooth pads connect and disconnect often; pausing on disconnect interrupts touch play.",
                "PauseOnControllerDisconnect = false;   // mobile default", Manager),

            ["Pause screen"] = new AuditInfo(
                "Whether the current settings can ever freeze the game, and so need a pause screen.",
                "RewiredInputManager.IsFrozen", Manager),

            ["Pause screen assigned (Game Paused)"] = new AuditInfo(
                "When the game can freeze, a pause screen with a Resume button must exist.",
                "public GameObject GamePaused;   // RewiredInputManager\nRewiredInputManager.Instance.Resume();", PauseScreen),

            ["A frozen game can always be resumed"] = new AuditInfo(
                "A frozen game with no input that resumes it would be stuck.",
                "RewiredInputManager.Instance.Resume();   // wired to a Resume button", PauseScreen),

            // ---- Project
            ["Configure()/SetGlobalConfiguration called from game code"] = new AuditInfo(
                "Only needed when Auto Configure On Start is off or you pass custom providers.",
                "RewiredInputManager.SetGlobalConfiguration(uiBlocker: ..., modalStack: ..., controllerHelpGate: ..., pauseGate: ...);"),

            ["ControllerHelpBlockedScenes match Build Settings"] = new AuditInfo(
                "Scenes listed here never show the controller help; names must match scenes in Build Settings.",
                "ControllerHelpBlockedScenes: [\"mainmenu\", \"credits\"]", Manager),

            ["Steam readiness can be detected"] = new AuditInfo(
                "The manager waits for Steam to be ready before configuring on Steam builds.",
                "SteamManager.Initialized"),

            ["Package version"] = new AuditInfo(
                "The installed Rewired Helper version.",
                "Packages/manifest.json: \"com.wagenheimer.rewiredhelper\": \"...git#vX.Y.Z\""),
        };

        /// <summary>Fills in the explanation, code snippet and ping target of a finding, plus what was already done about it.</summary>
        internal static AuditResult Enrich(AuditResult result)
        {
            if (string.IsNullOrEmpty(result.About) || result.Target == null)
            {
                if (Entries.TryGetValue(result.Title ?? string.Empty, out var info))
                {
                    if (string.IsNullOrEmpty(result.About)) result.About = info.About;
                    if (string.IsNullOrEmpty(result.Code)) result.Code = info.Code;
                    if (result.Target == null && info.Ping != null)
                    {
                        try { result.Target = info.Ping(); }
                        catch (Exception) { result.Target = null; }
                    }
                }
                else if (string.IsNullOrEmpty(result.About))
                {
                    result.About = DefaultAbout(result.Category);
                }
            }

            if (string.IsNullOrEmpty(result.DoneNote))
                result.DoneNote = RewiredAuditHistory.DescribeLast(result.Title);

            return result;
        }

        private static string DefaultAbout(string category) => category switch
        {
            "Mobile & Pause" => "Part of the per-platform pause policy.",
            "Project" => "A project-level check.",
            _ => "Part of the scene setup Rewired Helper needs."
        };
    }

    /// <summary>
    /// A small log of the fixes applied from the Audit (kept for the editor session), so the person can see what was already done instead
    /// of guessing why a finding disappeared.
    /// </summary>
    internal static class RewiredAuditHistory
    {
        private const string SessionKey = "RewiredHelper.AuditHistory";
        private const char FieldSeparator = '\u001f';
        private const int MaxEntries = 50;

        internal readonly struct Entry
        {
            public readonly DateTime Time;
            public readonly string Title;
            public readonly string FixLabel;

            public Entry(DateTime time, string title, string fixLabel)
            {
                Time = time;
                Title = title;
                FixLabel = fixLabel;
            }
        }

        internal static void Record(string title, string fixLabel)
        {
            var entries = Entries().Take(MaxEntries - 1).ToList();
            entries.Insert(0, new Entry(DateTime.Now, title, fixLabel));
            Save(entries);
        }

        internal static void Clear() => SessionState.EraseString(SessionKey);

        internal static List<Entry> Entries()
        {
            var raw = SessionState.GetString(SessionKey, string.Empty);
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(raw)) return entries;

            foreach (var line in raw.Split('\n'))
            {
                var parts = line.Split(FieldSeparator);
                if (parts.Length != 3 || !long.TryParse(parts[0], out var ticks)) continue;

                entries.Add(new Entry(new DateTime(ticks), parts[1], parts[2]));
            }

            return entries;
        }

        /// <summary>"Fixed with 'X' at 10:42" for the most recent fix of that finding, or null.</summary>
        internal static string DescribeLast(string title)
        {
            foreach (var entry in Entries())
            {
                if (entry.Title == title)
                    return $"Fixed with \"{entry.FixLabel}\" at {entry.Time:HH:mm:ss}.";
            }

            return null;
        }

        private static void Save(List<Entry> entries) =>
            SessionState.SetString(SessionKey, string.Join("\n", entries.Select(e =>
                string.Join(FieldSeparator.ToString(), e.Time.Ticks, Sanitize(e.Title), Sanitize(e.FixLabel)))));

        private static string Sanitize(string text) =>
            (text ?? string.Empty).Replace('\n', ' ').Replace(FieldSeparator, ' ');
    }
}
