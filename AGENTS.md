# Rewired Helper — Agent Notes

UPM package. Repo root = package root, installed via git URL, no wrapper Unity project.

## Structure

- `Runtime/RewiredInputManager.cs` — the only `MonoBehaviour` singleton. Owns all Rewired
  polling (`Update()`), cursor state, and Escape/Return routing decisions.
- `Runtime/EscapeButton.cs`, `ReturnEscapeEvent.cs`, `InputVisibilityController.cs` — generic,
  standalone components. None of them reference `RewiredInputManager` except through its public
  static surface (`IsUsingTouch`, `RegisterVisibilityController`, `PressedScape`, the two static
  flags on `ReturnEscapeEvent`).
- `Runtime/Integration/*.cs` — the three optional extension-point interfaces
  (`IUiBlocker`, `IModalStackProvider`, `IControllerHelpGate`), each with an internal
  `Null*` default implementation. `RewiredInputManager.Configure(...)` wires them in; omit any
  argument to keep the no-op default.
- `Editor/UpdateChecker.cs` + `UpdateAvailableWindow.cs` — copy-pasted-and-renamed from the
  sibling packages (UnityRateControl/UnityCloudSave), not a shared library. If you fix a bug
  here, port the fix to the other three `wagenheimer/Unity*` repos by hand.
- `Samples~/DefaultSetup` — minimal bootstrap, no optional interfaces wired.
- `Samples~/I2LocalizationIntegration` — **not** part of the Runtime asmdef. Only compiled if a
  consumer copies these files into their own project (which already has I2 Localization).

## Conventions & Gotchas

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
