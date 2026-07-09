# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-07-08

### Added

- `ModalDialog`/`ModalDialogStack` (`Wagenheimer.RewiredHelper.UI`): a generic modal dialog
  stack — overlay, fade/move show-hide animation, Escape/OK button wiring, sprite/TMP fade-out —
  generalized from a game's local `Dialogs`/`Dialog` classes. Zero third-party dependency by
  default (built-in coroutine tween); subclass `ModalDialog` and override
  `PlayFadeIn`/`PlayFadeOut`/`PlayMoveIn`/`PlayMoveOut` to swap in DOTween or another tweener
  from your own project (a package assembly cannot reference a loose-script DOTween install
  directly, since those compile into the default Assembly-CSharp).
- `DefaultModalStackProvider`: ready-to-use `IModalStackProvider` backed by `ModalDialogStack`,
  for `RewiredInputManager.Configure(modalStack: new DefaultModalStackProvider())`.
- `[MovedFrom]` attributes on `RewiredInputManager`, `EscapeButton`, `ReturnEscapeEvent`, and
  `InputVisibilityController` so existing scene/prefab references to the pre-0.1.0 flat-script
  versions of these components resolve to the package types automatically.

## [0.1.0] - 2026-07-08

### Added

- Initial UPM package release, ported from the original loose-script `RewiredHelper` repo.
- `RewiredInputManager`: input-type detection (mouse / touch / controller), cursor visibility,
  Escape/Return routing, controller connect/disconnect handling.
- `EscapeButton`, `ReturnEscapeEvent`, `InputVisibilityController` — generic UI helper components,
  ported without any third-party inspector dependency.
- Optional integration interfaces: `IUiBlocker`, `IModalStackProvider`, `IControllerHelpGate` —
  replace the previous hard dependency on game-specific singletons.
- Editor auto-update checker (`Tools/Wagenheimer/Rewired Helper/Check for Updates...`).
- `Samples~/DefaultSetup` — minimal bootstrap example.
- `Samples~/I2LocalizationIntegration` — optional I2 Localization + Rewired Glyphs integration,
  only needed by consumers who already use I2 Loc.
