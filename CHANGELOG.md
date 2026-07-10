# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.11.0] - 2026-07-10

### Added
- add automatic GUI generation buttons with auto-wiring for Pause Screen and Controller Help Form directly in Inspector

## [0.10.1] - 2026-07-10

### Changed
- design: custom inspector property grouping with detailed tooltips and premium headers

## [0.10.0] - 2026-07-10

### Added
- translate custom inspector in RewiredInputManagerEditor to English and improve layout with premium card designs

## [0.9.1] - 2026-07-10

### Changed
- docs: translate RewiredHelperSetupWindow UI to English and add detailed help for Modal Dialog System in README and Setup Window

## [0.9.0] - 2026-07-10

### Added
- add public Show/Hide methods in ModalDialog, expand to 5 transition effects, slide directions, and anchored RectTransform animations

## [0.8.0] - 2026-07-10

### Added
- attach ModalDialog component to generated ControllerHelpForm and structure it as full-screen overlay backdrop

## [0.7.1] - 2026-07-10

### Changed
- design: enhance ControllerHelpForm aesthetics with premium dark panel, accent bars, and grid alignment

## [0.7.0] - 2026-07-10

### Added
- rewrite CreateControllerHelpForm to generate structured scrollable rows for each mapped action

## [0.6.2] - 2026-07-10

### Fixed
- add missing GlyphHelperTypeName field in RewiredHelperSetupWindow

## [0.6.1] - 2026-07-10

### Fixed
- add missing using namespace Wagenheimer.RewiredHelper in EditorWindow

## [0.6.0] - 2026-07-10

### Added
- add dedicated Setup Checker & Help EditorWindow matching CloudSaveAudit design style

## [0.5.8] - 2026-07-10

### Fixed
- add missing meta files for new script and prefabs folder

## [0.5.7] - 2026-07-10

### Changed
- refactor: resolve compile error in editor using string-based Unity API instead of reflection

## [0.5.6] - 2026-07-09

### Fixed
- resolve compilation errors by loading Rewired.InputManager dynamically via reflection

## [0.5.5] - 2026-07-09

### Changed
- refactor: improve setup, fix modal stack bug, add diagnostics and integrated help GUI

## [0.5.4] - 2026-07-09

### Fixed
- disambiguate Object.FindObjectOfType calls with UnityEngine prefix

## [0.5.3] - 2026-07-09

### Fixed
- add missing .meta for DefaultSetupGenerator.cs

## [0.5.2] - 2026-07-09

### Changed
- docs: point to Rewired's Window > Rewired > Extras > Glyphs > Install menu

## [0.5.1] - 2026-07-09

### Changed
- refactor: use Rewired's official glyph addon instead of a hand-rolled reader

## [0.5.0] - 2026-07-09

### Added
- add ControllerHelpPanel + scene generator for a standard input-manager/UI setup

### Fixed
- declare samples in package.json so Package Manager shows the Samples tab

## [0.4.0] - 2026-07-09

### Added
- show dialog on manual check (up to date / errors), redesign update popup

## [0.3.5] - 2026-07-09

### Fixed
- remove dead CursorUIPointer field referencing Rewired's Demos namespace

## [0.3.4] - 2026-07-09

### Fixed
- log update-check failures instead of returning silently

## [0.3.3] - 2026-07-09

### Fixed
- restore package asmdef — Rewired core ships as precompiled DLLs, not loose scripts

## [0.3.2] - 2026-07-09

### Fixed
- remove package asmdef — breaks compilation against loose-script Rewired installs

## [0.3.1] - 2026-07-09

### Fixed

- Removed the package's own `Runtime`/`Editor` assembly definitions. Most Rewired installs ship
  the core `Rewired` namespace (`Player`, `Controller`, `ControllerType`, `ReInput`, etc.) as loose
  scripts with no `.asmdef` of their own, compiling into the default `Assembly-CSharp`. A package
  with its own separate asmdef can never reference types that only exist in `Assembly-CSharp` —
  so `RewiredInputManager.cs` failed to compile with `CS0246` errors for every Rewired type,
  regardless of whether Rewired was actually installed. The package now compiles as loose scripts
  too, alongside Rewired, exactly like the `Samples~/I2LocalizationIntegration` files already did
  and the `ModalDialog`/DOTween override pattern already assumed for optional dependencies —
  except here it applies to the package's core, unconditional dependency on Rewired itself.

## [0.3.0] - 2026-07-09

### Added
- add generic modal dialog stack, [MovedFrom] compatibility for renamed types
- restructure as a UPM package, decouple from game-specific singletons

### Changed
- Optimize controller change event: replace LocalizeAll with ForceUpdateSpecialization and reduce redundancy
- Use platform switch in IsConsolePlatform
- Refactor: Replace ReturnEscapeEvent script and fix RewiredHelper
- Improve controller handling for consoles and input detection
- Refactor input handling in RewiredHelper.cs
- Add mouse/touch movement tracking functionality
- Update RewiredHelper.cs
- Enhance null safety for input properties
- Enhance input handling in RewiredHelper.cs
- Update RewiredHelper.cs
- Improve input handling and add specialization detection
- Update RewiredHelper.cs
- Refactor RewiredHelper methods and add overlay handling
- Update RewiredHelper.cs
- Adiciona suporte para visibilidade e localização de UI
- Verifica se o GO está ativo
- Ajustes Detecção do Steam Overlay
- Atualizado GitIgnore
- Add escape and return button handling in Unity project
- Update RewiredHelper.cs
- Primeira Versão
- Initial commit

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
