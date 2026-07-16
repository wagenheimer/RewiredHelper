# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.22.2] - 2026-07-16

### Fixed
- remove duplicate MenuItem attribute for Setup Checker

## [0.22.1] - 2026-07-16

### Fixed
- dynamically load Player when ready and guard Player references to ensure BackButton works correctly

## [0.22.0] - 2026-07-16

### Added
- enforce high-sorting Canvas on GameCursor so it always stays on top of all UI

## [0.21.0] - 2026-07-16

### Added
- group X and Y suffix actions, increase icon font sizes, and layout in 2 columns

## [0.20.2] - 2026-07-16

### Fixed
- read actions from _userData.actions serialized property in design-time

## [0.20.1] - 2026-07-16

### Fixed
- check ReInput.isReady before accessing mapping in design-time

## [0.20.0] - 2026-07-16

### Added
- add design-time row building support for ControllerHelpRowBuilder

## [0.19.1] - 2026-07-16

### Fixed
- switch ControllerHelpForm viewport to RectMask2D and auto-wire Glyph Provider

## [0.19.0] - 2026-07-16

### Added
- build ControllerHelpForm rows at runtime from the live action map

## [0.18.17] - 2026-07-16

### Fixed
- stop ControllerHelpForm's transparent Mask from clipping out all rows

## [0.18.16] - 2026-07-16

### Fixed
- stop fallback controller-help rows from binding nonexistent actions

## [0.18.15] - 2026-07-16

### Fixed
- set text property of UnityUITextMeshProGlyphHelper during generation

## [0.18.13] - 2026-07-16

### Fixed
- stop controller help form rows collapsing to zero height

## [0.18.12] - 2026-07-16

### Fixed
- add missing .meta file for GameCursorPositioner.cs

## [0.18.11] - 2026-07-16

### Fixed
- convert PlayerMouse screen position through the Canvas instead of anchoredPosition

## [0.18.10] - 2026-07-16

### Changed
- editor: rewrite Wire Events to use SerializedObject instead of UnityEventTools to bypass Rewired's internal event subclass type constraints

## [0.18.9] - 2026-07-16

### Changed
- editor: fix CS1503 by calling UnityEventTools.AddPersistentListener via reflection to avoid compile-time type constraint on UnityEventBase

## [0.18.8] - 2026-07-16

### Changed
- editor: fix duplicate listener additions on Wire Events and use generic UnityEventBase with Delegate.CreateDelegate for GameObject.SetActive

## [0.18.7] - 2026-07-16

### Changed
- editor: refactor Player Mouse event auto-wiring to use official UnityEventTools API and reflection, resolving missing methods

## [0.18.6] - 2026-07-16

### Changed
- editor: improve IsEventWired validation to verify non-null target and non-empty method name

## [0.18.5] - 2026-07-16

### Changed
- editor: fix serialized property names for Player Mouse events by using leading underscores

## [0.18.4] - 2026-07-16

### Changed
- editor: add diagnostics check and one-click quick-fix to wire Player Mouse events to Game Cursor
- editor: regroup configuration vs runtime status and hide generator button when already wired
- docs: update README with setup checker, code-free options and custom inspectors
- chore: translate UI, docs and comments to English

## [0.18.3] - 2026-07-15

### Changed
- editor: regroup configuration vs runtime status and hide generator button when already wired

## [0.18.2] - 2026-07-15

### Changed
- docs: update README with setup checker, code-free options and custom inspectors

## [0.18.1] - 2026-07-15

### Changed
- chore: translate UI, docs and comments to English

## [0.18.0] - 2026-07-15

### Added
- diagnose and auto-configure Rewired's Player Mouse for joystick cursor movement

## [0.17.4] - 2026-07-15

### Fixed
- stop blank Controller Help descriptions when action has no Descriptive Name

## [0.17.3] - 2026-07-15

### Fixed
- make standalone cursor path testable in Editor regardless of active Build Target

## [0.17.2] - 2026-07-15

### Fixed
- expose CustomCursorEnabled/CursorTexture in Inspector, render rich text in status boxes

## [0.17.1] - 2026-07-13

### Fixed
- keep overlay hidden during Move/Scale show animation, fade in fast after

## [0.17.0] - 2026-07-13

### Added
- add custom inspector for Dialog with grouped, color-coded sections

## [0.16.1] - 2026-07-13

### Changed
- refactor: rename ModalDialog component to Dialog

## [0.16.0] - 2026-07-13

### Changed
- rename `ModalDialog` component to `Dialog`, matching the original Storm Tale 2 naming

## [0.15.0] - 2026-07-13

### Added
- add Dialogs legacy alias for ModalDialogStack

## [0.14.0] - 2026-07-10

### Added
- make GameCursor a required check in diagnostics and add automatic generation and linking shortcut

## [0.13.0] - 2026-07-10

### Added
- add AutoConfigureOnStart and UseDefaultModalStack properties to RewiredInputManager for code-free setup

## [0.12.1] - 2026-07-10

### Fixed
- restore missing script reference for RewiredCustomController_AndroidRemote on PlayerMouse child in Event System prefab

## [0.12.0] - 2026-07-10

### Added
- copy and enhance RewiredCustomController_AndroidRemote component to package

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
