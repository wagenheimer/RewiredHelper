# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
