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
- **Audio & SFX Bridge (`AutoBridgeSubmitToPointerDown`)** — automatically fires `IPointerDown` / `IPointerUp` on selected UI elements when confirming via Gamepad (`UISubmit` / Button A), ensuring audio systems like MasterAudio's `EventSounds` configured with PointerDown work seamlessly without code edits.
- **Mobile-safe pause policy** — app-background, controller-disconnect and Steam-overlay pauses are
  configurable per platform, never overwrite your `Time.timeScale`, and can be vetoed with `IPauseGate`.
  See [Pause Behavior & Mobile Best Practices](#pause-behavior--mobile-best-practices).
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
| Steamworks.NET *(optional)* | Auto-detected via the `com.rlabrecque.steamworks.net` package — enables Steam overlay pause |
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

### New game in three steps

1. Install the package (the Package Hub adds the other Wagenheimer dependencies for you).
2. Open your first scene and run **Tools → Wagenheimer → Rewired Helper → Setup → Set Up Everything (fix all)**
   (or **Dashboard → Setup Audit → Fix All Safe Issues**). It creates the Rewired Input Manager, the Rewired Event System, a Canvas, the
   Game Cursor, the Rewired UI actions (navigation, submit, cancel, mapped to gamepad and keyboard) and the on-screen keyboard, repeating
   until the audit has nothing left to fix.
3. Run the **Setup Audit** once more and read what is left: it lists anything manual (pause screen, controller help, glyphs) with a fix or a hint.

What you get without writing code: joystick cursor and menu navigation, Escape/Return routing, per-platform pause policy, a Steam Deck
on-screen keyboard that hides when its form closes, and forms that lift themselves above the soft keyboard (OS keyboard on mobile).
Everything is on `RewiredInputManager` as plain toggles (`OnScreenKeyboardOnlyOnSteamDeck`, `HideCursorWhileOnScreenKeyboardOpen`,
`AutoLiftFormsAboveKeyboard`, ...).

### Code-Free / Auto-Configuration (Recommended)

By default, `RewiredInputManager` has **Auto Configure On Start** and **Use Default Modal Stack** enabled in the Inspector. This means you do not need to write any bootstrapping code! 

1. Add `RewiredInputManager` to a GameObject in your scene.
2. Assign the `GameCursor` image under *Cursor & Visuals*.
3. The manager will configure itself on `Start()`.

### Manual Bootstrap / Advanced Setup

If you want to manually configure the manager (e.g., to disable auto-configuration or pass custom stack/blocker providers), uncheck **Auto Configure On Start** and call `Configure()` in `Awake()`:

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

## Diagnostics & Premium Inspectors

Rewired Helper features professional, color-coded custom inspectors to speed up your workflow:

- **Dashboard** (**Tools → Wagenheimer → Rewired Helper → Dashboard...**): UI Toolkit window like the other Wagenheimer packages, with four tabs:
  **Setup Audit** (scene + project scan with one-click fixes, *Copy Report* and *Copy AI Fix Prompt*), **Automatic** (the policy per platform and the status of your scene; manual overrides are tucked into a collapsed *Advanced* section),
  **Checklist** (persistent manual release checklist, mirrored in [`REWIRED-CHECKLIST.md`](REWIRED-CHECKLIST.md)) and **Docs & Updates**.
  The audit also runs headless for CI: `-executeMethod Wagenheimer.RewiredHelper.Editor.RewiredHelperAudit.RunHeadlessAndLog`.
- **One-Click Setup Generators**: Directly from the `RewiredInputManager` inspector, you can generate a default Pause Screen or a Controller Help Form.
- **Player Mouse Auto-Configuration**: The diagnostics window can automatically configure Rewired's Player Mouse settings for joystick cursor movement.
- **Dialog Inspector**: Features color-coded grouping for Transition Effects, slide directions, timing, and UnityEvents.

---

## Inspector

The `RewiredInputManager` inspector is a UI Toolkit panel sharing the Dashboard theme: a **Live Status** strip in Play Mode
(platform, active policy, input device, pause state, system-UI scope, with *Test Pause* / *Resume* buttons), an
**Automatic** list of everything decided for you (pause policy per build platform, Steam detection, cursor per device,
controller help), a **Setup Health** list with one-click fixes for whatever is missing in the scene, and collapsible
settings sections. The Setup Health checks are the same ones the Dashboard audit uses.

## Escape / Return Routing

- **`EscapeButton`** — attach to a `Button`. When Escape is pressed and no modal (see below)
  claims it, the highest-`Priority` active `EscapeButton` fires (its `UnityEvent`, or its
  `Button.onClick` if no `UnityEvent` listeners are set).
- **`ReturnEscapeEvent`** — attach to any GameObject that needs to react generically to Return
  or Escape (when no `EscapeButton` handled it). Wire `ReturnEvent`/`EscapeEvent`.
- **`InputVisibilityController`** — attach to any GameObject that should show/hide itself based
  on input type (e.g. on-screen touch controls).

### ReturnEscapeEvent Priority (who wins when several are active?)

`ReturnEscapeEvent` has a **`Priority`** field (0–1000, default **0**). When Escape/Back/Return is
pressed and the event is triggered:

1. Only instances whose GameObject is **active in hierarchy** are considered.
2. Among those, **only the one(s) with the highest `Priority` fire** — everything else is skipped.

This replaces the old behavior where *every* active instance fired simultaneously (which caused
bugs like a panel closing and the main menu opening from a single Back press).

**Typical setup — a game with a main menu plus context panels:**

| Component on... | Priority | Behavior |
|---|---|---|
| Context panel (e.g. building selection) | **10** | Consumes Back while it's open and closes itself |
| Main pause/menu handler | **0** | Receives Back only when no higher-priority panel is active |

Because the list is driven by `OnEnable`/`OnDisable`, gating happens automatically: put the
component on the panel's own GameObject so it's only registered while the panel is open. The
moment the panel closes itself in response to Back, it leaves the list — the next press falls
through to the lower-priority handler (e.g. opens the menu). No code needed, just priorities.

> Note the resolution order for a single press:
> 1. Top modal's `EscapeButton` (if a modal from your `IModalStackProvider` is open)
> 2. Highest-priority active `EscapeButton`
> 3. Highest-priority active `ReturnEscapeEvent`

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

### Static Alias (`Dialogs`)
For cleaner code, you can use the static `Dialogs` class as an alias to show and close dialogs:
```csharp
using Wagenheimer.RewiredHelper.UI;

Dialogs.ShowDialog(myDialog);
Dialogs.CloseDialog(myDialog);
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

## UI Blocker Overlay (`UIBlockerOverlay`)

Static helper that blocks **all pointer input** when you need the game to be temporarily
unclickable (scene transitions, cutscenes, countdowns):

```csharp
UIBlockerOverlay.Enabled = true;  // fullscreen transparent Image swallows every click/touch
UIBlockerOverlay.Enabled = false; // back to normal
```

- Lazily creates its own topmost overlay `Canvas` (sortingOrder 32767, `DontDestroyOnLoad`) with a
  fully transparent `Image` whose `raycastTarget = true` — so it intercepts clicks/touches without
  visually covering anything.
- Unlike disabling the EventSystem's input modules (the old `UIDisableInput` approach), UI
  navigation, Rewired input and animations keep working; only raycast targeting is blocked.

Typical wiring: keep it in sync with your own block flag every frame:

```csharp
UIBlockerOverlay.Enabled = myUiIsBlocked;
```

## Input-Based UI, Touch & Controller Controls (`InputVisibilityController`)

Attach **`InputVisibilityController`** to any UI element (or GameObject) to automatically adapt its state based on the current active input device (Mouse/Keyboard, Joystick/Gamepad, or Touch):

- **`TargetAction`**:
  - `ToggleGameObject` (default): Sets `gameObject.SetActive(true/false)`.
  - `ToggleSelectableInteractable`: Toggles `interactable` on a `Button`, `Toggle`, `Slider`, etc. (keeps it visible but grays it out / prevents focus).
  - `ToggleCanvasGroup`: Controls `alpha` (1 / 0) and `blocksRaycasts`/`interactable` on a `CanvasGroup`.
- **`VisibilityMode`**:
  - `ShowOnMouseOrKeyboardHideOnJoystickOrTouch`: Perfect for mouse-specific settings (like Custom Cursor toggles, mouse sensitivity).
  - `ShowOnJoystickOnly` / `HideOnJoystickOnly`: Gamepad specific prompts or panels.
  - `ShowOnTouchHideOtherwise`: Shows when touch is active; hides on mouse/keyboard/joystick (ideal for virtual DPads/touch buttons).
  - `HideOnTouchShowOtherwise`: Hides on touch; shows otherwise.
  - `ShowOnMouseOnly`, `HideOnMouseOnly`, `ShowOnTouchOnly`, `HideOnTouchOnly`, `ShowOnJoystickOrTouchHideOnMouse`, `ShowOnJoystickHideOnMouseOrTouch`.
  - `AlwaysShow` / `AlwaysHide`: Force state regardless of input.
- Automatically registers with `RewiredInputManager` and updates immediately whenever the input device changes (`OnInputTypeChanged` / `OnInputSpecializationChanged`).
- Exposes `_onVisibilityChanged (UnityEvent<bool>)` passing the evaluated boolean.

---

## Custom Cursor

Both fields are exposed in the Inspector under **Cursor & Visuals**, so you can assign a default
`Cursor Texture` and toggle `Custom Cursor Enabled` at design time. They can also be set from your
own save data/settings at runtime — the manager swaps the OS cursor on standalone builds when the
active device is a mouse:

```csharp
// Update the RewiredInputManager directly so it stays in sync:
_input.CustomCursorEnabled = MySaveData.CustomCursor;
_input.CursorTexture = MyConfig.cursorTexture;
```

> ⚠️ **Important:** Do not call `Cursor.SetCursor(...)` directly in your game code. `RewiredInputManager` manages the hardware OS cursor and joystick virtual cursor every frame. Changing `CustomCursorEnabled` on the manager ensures the preference is respected without getting overwritten.

### Toggling/Disabling Custom Cursor UI in Options
When a player is using a Gamepad / Joystick, hardware cursor customization is irrelevant. You can:
1. **Hide/Show the Option with `InputVisibilityController`** on the Toggle GameObject.
2. **Listen to input changes** in your Options script to enable/disable interactability:
```csharp
private void OnEnable()
{
    RewiredInputManager.OnInputSpecializationChanged += UpdateCursorToggleState;
    UpdateCursorToggleState();
}

private void OnDisable()
{
    RewiredInputManager.OnInputSpecializationChanged -= UpdateCursorToggleState;
}

private void UpdateCursorToggleState()
{
    if (cbCustomCursor == null) return;
    bool isJoystickOrTouch = RewiredInputManager.Instance != null &&
        (RewiredInputManager.Instance.CurrentControllerType == ControllerType.Joystick || RewiredInputManager.IsUsingTouch);
    
    cbCustomCursor.interactable = !isJoystickOrTouch;
}
```

**Testing in the Editor:** the standalone cursor path runs whenever `UNITY_STANDALONE` *or*
`UNITY_EDITOR` is defined, so Play Mode always exercises it regardless of the active Build Target
(e.g. it still works when the target platform is Android/iOS). If the cursor still doesn't change:
- Make sure `Custom Cursor Enabled` is checked and `Cursor Texture` is assigned.
- Check the texture's **Import Settings** — `Cursor.SetCursor` silently no-ops on a compressed or
  non-readable texture. Use the **Cursor** texture type preset (or Advanced → uncompressed RGBA32,
  no mipmaps) so the OS can actually read the pixels.
- The active input device must be `Mouse` — moving a connected controller/joystick switches
  `CurrentControllerType` away from `Mouse` and disables the OS cursor entirely.

---

## Pause Behavior & Mobile Best Practices

The manager can pause for four reasons (`PauseReason`): **AppBackground**, **ControllerDisconnected**,
**SteamOverlay** and **Manual**. Several can be active at once without releasing each other.

**Nothing to configure.** The policy is chosen automatically from the platform of each build (Unity Build Pipeline,
Build Settings or CLI) when the game runs, never from what was saved in a scene:

| Setting | Android / iOS | Desktop / console |
|---|---|---|
| App goes to background | `Silent` (event only, no overlay, `timeScale` untouched) | `Overlay` (freeze + `GamePaused`) |
| Pause on controller disconnect | **off** | on (only if the disconnected pad was the active device and the player isn't using touch) |
| Resume on any input | **off** (use a Resume button wired to `Resume()`) | on |
| Steam overlay | n/a | pauses, resumes on close; Steam readiness is auto-detected from `SteamManager.Initialized` |
| Android TV / Fire TV | uses the desktop / console column (detected at runtime with `UiModeManager`) | |
| Steam Deck | n/a | app-background pause also fires on `OnApplicationFocus` (suspend/resume), detected with `RewiredInputManager.IsSteamDeck` |

Every player build logs the policy it uses (`RewiredBuildPreprocessor`) and warns if a scene/prefab contains an explicit
override that breaks it on mobile. To customise, turn on **Override Platform Defaults** on the manager and edit the
three settings (each still has an `Auto` value = the platform default). The Dashboard's *Automatic* tab shows the
per-platform table.

Why mobile differs: the OS already suspends the app, and notification shade / ads / IAP sheets / app switches
all raise `OnApplicationPause`, so a "GAME PAUSED" overlay popping up constantly is noise. Bluetooth pads and
remotes connect/disconnect often, and "tap anywhere to resume" leaks taps into gameplay.

`Time.timeScale` is only touched while a *frozen* pause is active: the manager saves the value it found and
restores it, and if your game changed the time scale meanwhile, your value wins.

```csharp
// Ads / IAP / share sheets / permission prompts: one line, no setup, works from any package.
// Automatic pauses are vetoed while it is open (+1.5 s grace); a lost Dispose expires after 3 min.
using (RewiredInputManager.BeginSystemUi()) { ShowInterstitial(); }
RewiredInputManager.SuppressPauseFor(3f);     // fire-and-forget variant

// Custom rules (loading screens, cutscenes...). Manual pauses are never vetoed.
class MyPauseGate : IPauseGate { public bool CanPause(PauseReason r) => !Loading.IsActive; }
RewiredInputManager.SetGlobalConfiguration(pauseGate: new MyPauseGate());

// React to any pause (silent ones have frozen == false), e.g. mute music or stop a timer.
RewiredInputManager.OnPauseChanged += (paused, frozen) => Music.SetPaused(paused);

// Your own pause menu
RewiredInputManager.Instance.RequestPause();
RewiredInputManager.Instance.Resume();      // wire to the Resume button
```

**A pause screen is required** wherever the game can freeze (desktop / console / TV): assign your own to `Game Paused`, or use
**Create Pause Screen** (inspector or Dashboard), which builds one with a Resume button wired to `Resume()`. The Setup Audit
**fails** when `Game Paused` is empty (checked for every platform class, not just the active build target), and when a frozen
game would have no way out. Desktop/console builds also log a warning if a scene/prefab has it empty. At runtime the first
freeze without one logs a warning. With the Auto defaults an ad or IAP sheet no longer shows any overlay on mobile; `BeginSystemUi()` matters for Overlay mode and for games that react to `OnPauseChanged`.

### Steam overlay

If the `com.rlabrecque.steamworks.net` package is installed, the manager pauses when the Steam overlay opens
and resumes when it closes. Steam readiness is detected from `SteamManager.Initialized` (standard Steamworks.NET
bootstrap); set `RewiredInputManager.SteamIsInitialized = true` yourself only if you use a different bootstrap.
With IL2CPP managed stripping, keep `SteamManager` in a `link.xml` (or assign `SteamIsInitialized` yourself), since it is read by reflection. Disable with `PauseOnSteamOverlay = false`.

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

Once installed, use **Tools → Wagenheimer → Rewired Helper → Setup → Create Controller Help Form** to
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

## Steam Deck Suspend/Resume

Unity does not reliably call `OnApplicationPause` on a Standalone build (only mobile/console are guaranteed), so a
Deck suspend (lid close, power button, Steam's "Suspend Game") could otherwise slip past the pause policy entirely.
`RewiredInputManager.IsSteamDeck` (compiled against `Steamworks.SteamUtils.IsRunningOnSteamHardware()`, gated on
Steamworks.NET 2025.165.0+ via `versionDefines` — that API replaced the older, now-removed
`IsSteamRunningOnSteamDeck`; `RewiredInputManager.SupportsSteamDeckDetection` reports whether the installed version
is new enough, and the Setup Audit flags it when it isn't) makes `OnApplicationFocus` also drive the same
app-background pause/resume path on Deck specifically, leaving other desktop/console games unaffected. This is what Valve's "Deck Verified" checklist calls
"seamless suspend": the game freezes cleanly and resumes without a stuck or crashed state — exactly the classic
Overlay pause screen this package already has, as long as `Game Paused` is assigned (the Setup Audit fails without it).

## On-Screen Keyboard (gamepad text input, Steam Deck)

Steam Deck's "Deck Verified" checklist fails a game whose text fields have no way to type with only a
controller — its Gaming Mode has no physical keyboard by default. `RewiredOnScreenKeyboard` (ported from this
project's own `Assets/OSK`, with the hard MasterAudio dependency replaced by a `UnityEvent OnKeyPressed` you
can wire to your own SFX) covers this with **no per-field setup**: put one in the scene and
`RewiredInputManager` shows/hides it automatically whenever a `TMP_InputField` is selected while the active
input is a joystick (Steam Deck, Xbox/PlayStation pads, any Rewired joystick). Touch already gets the OS's own
keyboard for free; mouse/keyboard needs none.

```csharp
// Optional: your own theme, or wire your own key art. Otherwise skip this entirely —
// Tools → Wagenheimer → Rewired Helper → Setup → Create On-Screen Keyboard builds a plain, fully working one.
RewiredInputManager.RegisterOnScreenKeyboard(myKeyboard);  // only if you built it by hand, not via the generator
```

The Setup Audit warns when the scene has `TMP_InputField`s but no `RewiredOnScreenKeyboard`, with a **Create
On-Screen Keyboard** one-click fix (builds a plain QWERTY + digits + backspace/space/enter/caps keyboard from
code — no art assets, restyle it however you like afterwards). Turn off the automatic show/hide with
`RewiredInputManager.Instance.ShowOnScreenKeyboardOnGamepadTextInput = false` if you wire it yourself.

By default the keyboard only opens **on Steam Deck** (`OnScreenKeyboardOnlyOnSteamDeck`); other gamepads keep their own system
keyboard. The Unity Editor always shows it when a gamepad is active, so it can be tested without a Deck.

**Keep the field visible.** Add `RewiredKeyboardAvoider` to a form's root (optionally assign the input field) and the form is lifted
just enough to stay above the soft keyboard: the OS keyboard on mobile and this on-screen keyboard on Steam Deck. It only moves when
the keyboard would cover the field, and returns afterwards.

## Legacy `RewiredHelper` Component

Projects that never renamed their `RewiredHelper` GameObject/component still work: the package ships a
backward-compatibility subclass (`global::RewiredHelper : RewiredInputManager`), and the Inspector/Dashboard
customization applies to it too. The Setup Audit flags it (Warning) with a one-click **Migrate to
RewiredInputManager** fix that swaps the component's script reference in place — the same technique Unity
uses for an in-place class rename, so no data is lost and no other object's reference to that exact component
breaks. For a project-wide sweep (every scene and prefab, not just the open scene), use
**Tools → Wagenheimer → Rewired Helper → Migrate → Legacy RewiredHelper Component...**.

## Editor Utilities

| Menu | Action |
|---|---|
| Tools → Wagenheimer → Rewired Helper → Dashboard... | Setup audit, mobile & pause policy, checklist, docs |
| Tools → Wagenheimer → Rewired Helper → Verify Setup... | Opens the Dashboard on the Setup Audit tab |
| Tools → Wagenheimer → Rewired Helper → Setup → Create Rewired Input Manager | Adds a `RewiredInputManager` GameObject to the open scene |
| Tools → Wagenheimer → Rewired Helper → Setup → Create Controller Help Form | Generates a controller-help panel using Rewired's official glyph addon, if present |
| Tools → Wagenheimer → Rewired Helper → Setup → Create On-Screen Keyboard | Builds a gamepad / Steam Deck on-screen keyboard in the open scene |
| Tools → Wagenheimer → Rewired Helper → Fix → … | Verify & fix Game Cursor wiring, remove duplicate Event Systems |
| Tools → Wagenheimer → Rewired Helper → Migrate → … | Migrate a legacy `RewiredHelper` component or the old `Dialog.cs` |
| Tools → Wagenheimer → Rewired Helper → Reset Window Position | Centers the Dashboard if it opened off-screen (e.g. saved on a disconnected monitor) |
| Tools → Wagenheimer → Rewired Helper → Check for Updates... | Manually check for a new package version |
| Tools → Wagenheimer → Rewired Helper → Integration Guide (README) | Opens this README on GitHub |
| Tools → Wagenheimer → Rewired Helper → Report Issue | Opens a new GitHub issue |

---

## License

MIT — see [LICENSE](LICENSE).
