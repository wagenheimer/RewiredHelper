# Default Setup Sample

1. Create an empty GameObject in your first scene (e.g. `RewiredHelper`).
2. Add the `RewiredInputManager` component to it.
3. Add `RewiredHelperBootstrap.cs` (this sample) to the same GameObject, or copy its `Configure()`
   call into your own bootstrap script.
4. Assign `GameCursor` (an `Image` used as your custom cursor) in the Inspector if your game uses
   a custom on-screen cursor. Optional.
5. If your game has its own modal/dialog stack, UI-blocked state, or controller-help gating,
   implement `IUiBlocker`, `IModalStackProvider`, and/or `IControllerHelpGate` and pass them to
   `Configure(uiBlocker:, modalStack:, controllerHelpGate:)` instead of calling it with no
   arguments.
6. Wire `RewiredInputManager.OnShowControllerHelp` (a `UnityEvent`) to whatever UI you want to
   show the first time a controller is detected.
