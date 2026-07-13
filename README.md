# Rewired Helper

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-com.wagenheimer.rewiredhelper-green.svg)](https://github.com/wagenheimer/RewiredHelper)

An input-type detection and UI-routing layer built on top of [Rewired](https://guavaman.com/projects/rewired/)
— tracks whether the player is using mouse, touch, or a controller, drives cursor visibility,
routes Escape/Return to the right place, and handles controller connect/disconnect —
without hard-coding any assumptions about your game's UI or save system.

---

## Features

- **Input-type tracking** — mouse, touch, and controller are detected automatically every frame,
  exposed via `RewiredInputManager.IsUsingTouch` and `CurrentControllerType`.
- **Cursor management** — shows/hides a custom cursor image and the OS cursor based on the
  active input device; standalone builds get a `Cursor.SetCursor` texture swap.
- **Escape/Return routing** — `EscapeButton` (priority-ordered) and `ReturnEscapeEvent`
  components let any UI panel opt into Escape/Return handling without polling `Input` itself.
- **Controller connect/disconnect handling** — auto-pauses on disconnect (with a console-aware
  reconnect grace period), resumes on reconnect or any button press.
- **Zero forced dependencies** — no Odin Inspector, no I2 Localization required. Both are
  supported as opt-in extension points.
- **Dependency injection** — plug in `IUiBlocker`, `IModalStackProvider`, `IControllerHelpGate` to
  integrate with your own UI-blocking/modal/loading-screen systems. All optional.

---

## Requirements

| Dependency | Version |
|---|---|
| Unity | 2021.3 LTS or newer |
| [Rewired](https://guavaman.com/projects/rewired/) | Any recent version — imported manually, not a UPM package |
| TextMeshPro | Required by `UnityEngine.UI`/TMP-based components |
| Steamworks.NET *(optional)* | Auto-detected via `STEAMWORKS_NET` define — enables Steam overlay pause |
| I2 Localization *(optional)* | Only needed for the `Samples~/I2LocalizationIntegration` sample |

---

## Installation

Add the package via the Unity Package Manager **Add package from git URL**:

```
https://github.com/wagenheimer/RewiredHelper.git
```

Or add it manually to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.wagenheimer.rewiredhelper": "https://github.com/wagenheimer/RewiredHelper.git"
  }
}
```

To lock a specific tag:

```
https://github.com/wagenheimer/RewiredHelper.git#v1.0.0
```

Rewired itself must already be imported into your project — this package does not bundle or
substitute it.

---

## Quick Start

1. Create an empty GameObject (e.g. `RewiredHelper`) and add `RewiredInputManager` to it.
2. Call `Configure()` once at startup (see `Samples~/DefaultSetup` for a minimal bootstrap):

```csharp
using UnityEngine;
using Wagenheimer.RewiredHelper;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private RewiredInputManager _input;

    private void Awake()
    {
        // No arguments = harmless defaults: input never blocked, no modal stack,
        // controller help always allowed to show.
        _input.Configure();
    }
}
```

3. Assign `GameCursor` (an `Image`) in the Inspector if you use a custom on-screen cursor.
4. Add `EscapeButton` to any button that should react to Escape, and/or `ReturnEscapeEvent` to
   any panel that should react to Return — see below.

---

## Escape / Return Routing

- **`EscapeButton`** — attach to a `Button`. When Escape is pressed and no modal (see below)
  claims it, the highest-`Priority` active `EscapeButton` fires (its `UnityEvent`, or its
  `Button.onClick` if no `UnityEvent` listeners are set).
- **`ReturnEscapeEvent`** — attach to any GameObject that needs to react generically to Return
  or Escape (when no `EscapeButton` handled it). Wire `ReturnEvent`/`EscapeEvent`.
- **`InputVisibilityController`** — attach to any GameObject that should show/hide itself based
  on input type (e.g. on-screen touch controls).

---

## Optional Interfaces

These three interfaces are the extension points for the most common integration needs. All are
optional — the manager works with sensible defaults if you never implement them.

### `IUiBlocker` — suppress Escape/Return while your own UI is blocked

```csharp
public class MyUiBlocker : IUiBlocker
{
    public bool IsUiBlocked => MyGame.IsShowingCutscene || MyGame.IsLoading;
}
```

### `IModalStackProvider` — let your modal/dialog stack claim Escape/Return first

```csharp
public class MyModalStackProvider : IModalStackProvider
{
    public int ModalCount => MyDialogs.Modals.Count;
    public void PruneInactiveTop() { /* remove top modal if null/inactive */ }
    public bool TryGetTopEscapeButton(out Button b) { b = MyDialogs.Top?.EscapeButton; return b != null; }
    public bool TryGetTopOkButton(out Button b) { b = MyDialogs.Top?.OkButton; return b != null; }
}
```

### `IControllerHelpGate` — control when the first-time controller-help prompt can show

```csharp
public class MyControllerHelpGate : IControllerHelpGate
{
    public bool CanShowControllerHelp => !MyLoadingScreen.IsLoading && MyLoadingScreen.AlreadyShowedMainMenu;
}
```

Combine all three in one call:

```csharp
_input.Configure(
    uiBlocker: new MyUiBlocker(),
    modalStack: new MyModalStackProvider(),
    controllerHelpGate: new MyControllerHelpGate());
```

Wire `RewiredInputManager.OnShowControllerHelp` (a `UnityEvent`) to whatever dialog you want to
show the first time a controller is detected — the package never assumes a specific dialog system.

### Don't want to write your own `IModalStackProvider`?

Use the bundled `Dialog`/`ModalDialogStack` (`Wagenheimer.RewiredHelper.UI`) — a generic
modal dialog stack with overlay, fade/move show-hide animation, and Escape/OK button wiring — and
pass its ready-made `DefaultModalStackProvider`:

```csharp
_input.Configure(modalStack: new DefaultModalStackProvider());
```

See **Modal Dialog Stack** below.

---

## Modal Dialog Stack (`Wagenheimer.RewiredHelper.UI`)

`Dialog` (attach to a dialog GameObject, requires `CanvasGroup`) + `ModalDialogStack`
(static, tracks open dialogs) give you a ready-to-use modal system:

```csharp
using Wagenheimer.RewiredHelper.UI;

// Show/Hide via code:
ModalDialogStack.ShowDialog(myDialog);
ModalDialogStack.CloseDialog(myDialog);

bool anyOpen = ModalDialogStack.IsThereAnyVisible;
```

### Direct UnityEvent Wiring (No code needed)
The `Dialog` component exposes public instance methods so it can be called directly from any `UnityEvent` (like `OnShowControllerHelp` or a UI button click) from the Inspector:
- **`dialog.Show()`**: Pushes this dialog onto the active `ModalDialogStack`, triggerring the show animation.
- **`dialog.Hide()`**: Pops this dialog from the stack, triggerring the close/hide animation.

### Configurations & Advanced Effects
You can customize transitions directly in the `Dialog` Inspector:
- **`ShowEffect`**:
  - `Fade`: Classic alpha fade transition.
  - `Move`: Slides the dialog panel.
  - `Scale`: Zoom-in pop transition (from `StartScale` to `1.0`).
  - `FadeAndScale`: Combines alpha fade and zoom-in.
  - `FadeAndMove`: Combines alpha fade and slide.
- **`MoveDirection`**: Options to slide from `Up`, `Down`, `Left`, or `Right`. Uses the `RectTransform.anchoredPosition` for resolution-independent layouts.
- **`StartScale`**: The initial zoom scale for scale-based effects (default: `0.5`).
- **Timing & Audio**: Configure `ShowHideDialogTime`, or subscribe to `AfterShow`/`AfterHide` (`UnityEvent`) or `OnShow`/`OnHide` (`Action`) hooks to trigger custom audio.
- **`DefaultModalStackProvider`**: Pass `new DefaultModalStackProvider()` straight to `RewiredInputManager.Configure` to automatically route Escape/Return keys to close the top-most active dialog.

---

## Custom Cursor

Set `CustomCursorEnabled` and `CursorTexture` from your own save data/settings — the manager
swaps the OS cursor on standalone builds when the active device is a mouse:

```csharp
_input.CustomCursorEnabled = MySaveData.CustomCursor;
_input.CursorTexture = MyConfig.cursorTexture;
```

---

## Steam Overlay Pause

If `Steamworks.NET` is present (`STEAMWORKS_NET` define set) and `SteamManager.Initialized`,
the manager auto-pauses when the Steam overlay opens. Disable with `PauseOnSteamOverlay = false`.

---

## I2 Localization Integration (optional)

See `Samples~/I2LocalizationIntegration` — extends I2 Localization's specialization system so
localized terms/glyphs can react to the current input device ("Touch" / "Controller" / "PC").
Only import this sample if you already use I2 Localization.

---

## Controller Help Form

Rewired ships its own official glyph system as an installable extra — **Window → Rewired → Extras
→ Glyphs → Install** — which sets up both the icon sets (Xbox/PlayStation/Switch/etc.) and
`Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper`, a TMP component that parses
`<rewiredElement>`/`<rewiredAction>` tags in a text and swaps in the glyph for whichever
controller is currently active. **Install it via that menu first** — this package cannot bundle
that addon itself, since it ships under Rewired's own commercial license rather than this
package's MIT one.

Once installed, use **Tools → Wagenheimer → Rewired Helper → Create Controller Help Form** to
generate a panel in the open scene: it detects the addon via reflection, adds
`UnityUITextMeshProGlyphHelper` to a TMP label, and fills it with one
`<rewiredElement actionName="X"> <rewiredAction name="X">` line per Button-type Action already
defined in your Rewired Input Manager — real glyphs, correct action names, no manual wiring. If
the addon isn't found yet, the form is still created with placeholder text explaining what to
extract, so you can re-run the command after.

Restyle the generated form and save it as a prefab in your own project. Wire it to
`RewiredInputManager.OnShowControllerHelp` — that event already fires exactly once, the first time
a joystick/gamepad is detected (see `alreadyShowedControllerHelp` / `IControllerHelpGate` above):

```csharp
_input.OnShowControllerHelp.AddListener(() => controllerHelpForm.SetActive(true));
```

---

## Editor Utilities

| Menu | Action |
|---|---|
| Tools → Wagenheimer → Rewired Helper → Create Rewired Input Manager | Adds a `RewiredInputManager` GameObject to the open scene |
| Tools → Wagenheimer → Rewired Helper → Create Controller Help Form | Generates a controller-help panel using Rewired's official glyph addon, if present |
| Tools → Wagenheimer → Rewired Helper → Check for Updates... | Manually check for a new package version |
| Tools → Wagenheimer → Rewired Helper → Integration Guide (README) | Opens this README on GitHub |
| Tools → Wagenheimer → Rewired Helper → Report Issue | Opens a new GitHub issue |

---

## License

MIT — see [LICENSE](LICENSE).
