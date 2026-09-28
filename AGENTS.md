# Rewired Helper — Agent Notes

UPM package. Repo root = package root, installed via git URL, no wrapper Unity project.

## Structure

- `Runtime/RewiredInputManager*.cs` — the only `MonoBehaviour` singleton, split as a `partial` class
  (main: polling/cursor; `.Pause.cs`: pause policy; `.Routing.cs`: Escape/Return + submit bridge;
  `.Glyphs.cs`: glyph selector reflection). Keep each file under ~800 lines.
- `Runtime/PauseController.cs` — pure (no Unity/Rewired calls) pause-reason + `timeScale` ownership
  logic, unit-tested in `Tests/Editor/PauseControllerTests.cs`. Never write `Time.timeScale` from the
  manager directly: go through it so a host-changed scale is not overwritten.
- `Runtime/EscapeButton.cs`, `ReturnEscapeEvent.cs`, `InputVisibilityController.cs` — generic,
  standalone components. None of them reference `RewiredInputManager` except through its public
  static surface (`IsUsingTouch`, `RegisterVisibilityController`, `PressedScape`, the two static
  flags on `ReturnEscapeEvent`).
- `Runtime/Integration/*.cs` — the four optional extension-point interfaces
  (`IUiBlocker`, `IModalStackProvider`, `IControllerHelpGate`, `IPauseGate`), each with an internal
  `Null*` default implementation. `RewiredInputManager.Configure(...)` wires them in; omit any
  argument to keep the no-op default.
- `Editor/DefaultSetupGenerator.cs` — menu items that build a `RewiredInputManager` GameObject and
  a controller-help form directly in the open scene (not shipped as a hand-authored `.prefab`
  file — this package's CI can't validate a raw prefab YAML actually instantiates correctly,
  generating it via code is safer). The form uses Rewired's own official glyph addon
  (`Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper`, from
  `Assets/Rewired/Internal/Assets/Extras/GlyphsUnityUITMProAddonV2.zip` — extracted by the
  consumer, not bundled here) via `Type.GetType`/reflection, since this package cannot reference
  it directly (it ships under Rewired's commercial license, not this package's MIT one) or even
  guarantee it's present. Do not reintroduce a hand-rolled glyph reader
  (`ActionElementMap.elementIdentifierGlyph` etc.) — Rewired's own addon already solves this
  better; this was tried and removed on 2026-07-09.
- `Editor/RewiredHelperDashboardWindow.cs` + `Editor/UI/*` + `Editor/RewiredHelperAudit.cs` — UI Toolkit
  dashboard (Setup Audit / Automatic / Checklist / Docs), same structure as `UnityIAPHelper`'s dashboard
  (`rh-` USS prefix). Audit results carry an optional one-click `Fix`. Manual items live in
  `REWIRED-CHECKLIST.md` and `RewiredHelperChecklistView.Items` — keep both in sync.
- Pause policy is automatic per build platform: `PausePolicy.ForPlatform(isMobile)` (mobile = silent app-background
  pause, no pause on controller disconnect, no tap-anywhere resume), resolved at runtime from the compile-time
  platform (`AutoIsMobile` in `RewiredInputManager.Pause.cs`), never from serialized values, unless the scene
  explicitly sets `OverridePlatformDefaults`. `Editor/RewiredBuildPreprocessor.cs` (IPreprocessBuildWithReport, like
  RateControl) logs the policy per build and warns about mobile-breaking overrides (text scan in
  `RewiredOverrideScanner`, pure and unit-tested); it must stay read-only. Android TV / Fire TV are detected at
  runtime (`RewiredInputManager.IsTelevisionDevice`) and get the desktop policy. The pause screen is deliberately NOT auto-created at runtime: a missing `GamePaused` must FAIL the audit
  (`AuditPauseScreen`, checked for every platform class). Do not add "fix it" buttons for the
  pause policy: it is automatic, an override is a deliberate user choice and is only reported.
- `Runtime/RewiredHelper.cs` — a global-namespace `RewiredHelper : RewiredInputManager` backward-compatibility
  subclass (`[AddComponentMenu("")]`, hidden from the Add Component menu) for projects that never renamed their
  component. `[CustomEditor(typeof(RewiredInputManager), editorForChildClasses: true)]` makes the custom
  Inspector apply to it too — do not drop `editorForChildClasses`, or this legacy component silently falls
  back to Unity's plain reflection-based inspector with no visible error (this exact regression happened once).
  `Editor/RewiredLegacyMigrator.cs` detects it (surfaced as a Setup Audit warning with a fix) and migrates it by
  swapping the component's `m_Script` reference in place, never destroying/recreating the Component — that
  preserves every other asset's reference to it, unlike an add-new+copy-fields+destroy-old approach.
- `RewiredInputManager.IsSteamDeck`/`SupportsSteamDeckDetection` (in `.Pause.cs`) compile directly against
  `Steamworks.SteamUtils.IsRunningOnSteamHardware()` (verified present in Steamworks.NET 2025.165.0, which
  replaced the older, now-removed `IsSteamRunningOnSteamDeck`), gated by a SECOND `versionDefines` entry in
  `Runtime/Wagenheimer.RewiredHelper.asmdef` (`WAGENHEIMER_STEAM_DECK_DETECTION`, expression `"2025.165.0"`) on
  top of the existing `WAGENHEIMER_STEAMWORKS` one. A REFLECTION-BASED probe was tried first and reverted: this
  package is used across many of Cezar's own projects where Steamworks.NET is a raw `Assets/` drop with no
  `package.json` at all (so `versionDefines` never fires there either) or an OLD version — always verify a
  Steamworks.NET API actually exists in a REAL resolved copy (`Library/PackageCache/com.rlabrecque.steamworks.net@*`)
  before assuming it, API names change across SDK versions (this one did). `OnApplicationFocus` reuses
  `OnApplicationPause` on Deck specifically, since Unity does not reliably call `OnApplicationPause` on a
  Standalone build.
- `Runtime/UI/RewiredOnScreenKeyboard.cs` + `RewiredOnScreenKeyboardKey.cs` + `Runtime/RewiredInputManager.Keyboard.cs` —
  on-screen keyboard for gamepad/Steam Deck text input, ported from this project's own `Assets/OSK` (renamed,
  `DarkTonic.MasterAudio` hard dependency replaced by `OnKeyPressed`). `RewiredInputManager` auto-shows/hides any
  registered instance when a `TMP_InputField` is selected with a joystick — do not require per-field wiring, that
  defeats the point. `DefaultSetupGenerator.CreateOnScreenKeyboardAndWire()` builds a plain functional one from
  code (`GridLayoutGroup`, no art assets) since a real prefab can't be hand-authored blind in this repo.
- Scene checks live once in `Editor/RewiredSetupChecks.cs` and feed both the Dashboard audit and the Inspector
  (`Editor/RewiredInputManagerEditor.cs`, UI Toolkit, `rh-` USS). `Editor/AssemblyInfo.cs` exposes internals to
  `Wagenheimer.RewiredHelper.EditorTests`. Don't reintroduce an unconditional
  `OnApplicationPause -> freeze + overlay`; that was the "game keeps pausing on mobile" bug.
- `Editor/UpdateChecker.cs` + `UpdateAvailableWindow.cs` — copy-pasted-and-renamed from the
  sibling packages (UnityRateControl/UnityCloudSave/UnityNativeSocial), not a shared library. If
  you fix a bug here, port the fix to the other three `wagenheimer/Unity*` repos by hand.
- `Samples~/DefaultSetup` — minimal bootstrap, no optional interfaces wired.
- `Samples~/I2LocalizationIntegration` — not compiled by default. Only built if a consumer copies
  these files into their own project (which already has I2 Localization).

## Conventions & Gotchas

- **This package DOES have `Runtime`/`Editor` `.asmdef` files, and must keep them.** A commit on
  2026-07-09 (`cb07f45`) briefly removed them on the theory that Rewired ships its core namespace
  (`Player`, `Controller`, `ControllerType`, `ReInput`, etc.) as loose scripts with no asmdef,
  making it unreachable from an isolated package assembly. That theory was wrong and was never
  verified against a real install — it was reverted the same day. In every Rewired distribution
  actually seen so far, the core namespace ships as **precompiled per-platform DLLs**
  (`Assets/Rewired/Internal/Libraries/Runtime/Rewired_*.dll`) with `isExplicitlyReferenced: 0` in
  the plugin importer, which Unity auto-references into **every** assembly — including an isolated
  package asmdef — as long as that asmdef leaves `overrideReferences: false` (the default, and
  what this package uses). So the package asmdef needs no `"Rewired"` entry in `references` at
  all; the precompiled DLL just resolves automatically. If a future CS0246 on a Rewired type
  reappears, verify what Rewired's core namespace actually compiles into in that project
  (`Library/ScriptAssemblies/*.dll` containing the types, or the plugin importer's
  `isExplicitlyReferenced` flag) before touching this asmdef again — do not re-remove it on
  assumption.
- No Odin Inspector, no I2 Localization dependency in `Runtime/` or `Editor/` — those are the two
  things this package was explicitly de-coupled from when ported out of the original game
  (NordStormSolitaire). Do not reintroduce a hard reference to either.
- Rewired itself is **not** a UPM dependency (it's an Asset Store asset, not distributable via
  `package.json`), so it's undeclared there — document it as a manual prerequisite in the README
  instead, and don't add a `references` entry for it in the asmdef (precompiled DLLs auto-resolve).
- `RewiredInputManager.Configure()` must be called manually by the host game — it is not invoked
  automatically from `Awake()`, so the manager works correctly (with no-op defaults) even before
  `Configure()` runs, but a host game that needs `IUiBlocker`/`IModalStackProvider`/
  `IControllerHelpGate` must call `Configure()` before relying on that behavior.
- Version bump/tag/release is fully automated by `.github/workflows/bump-version.yml` on every
  push to `main` — do not manually edit `package.json`'s `version` or add CHANGELOG entries by
  hand for released versions; the workflow derives the bump type from Conventional Commit
  prefixes (`feat:` → minor, `fix:`/other → patch, `!`/`BREAKING CHANGE` → major).
