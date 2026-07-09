# I2 Localization Integration (optional)

Only import this sample if your project already uses **I2 Localization** (a paid Asset Store
plugin) together with Rewired's TextMeshPro glyph helper. It is not required by
`RewiredInputManager` and is not compiled as part of the package's Runtime assembly.

## Contents

- `SpecializationManager.cs` — extends I2 Localization's own partial `SpecializationManager`
  class to return `"Touch"`, `"Controller"`, or `"PC"` based on `RewiredInputManager`'s current
  input state, so I2 can pick the right term/glyph variant automatically.
- `LocalizeTarget_UnityUITextMeshProGlyphHelper.cs` — registers a localization target for
  Rewired's `UnityUITextMeshProGlyphHelper`, so glyph-based labels (e.g. "Press [A] to jump")
  can be localized like any other I2 term.

## Setup

1. Copy both files into your project (anywhere compiled into Runtime, or your own Editor/Runtime
   assembly that already references I2 Localization and Rewired).
2. In your bootstrap, wherever you call `RewiredInputManager` setup, no extra wiring is needed —
   I2 Localization discovers `SpecializationManager`'s partial-class extension automatically.
3. Optionally call `SpecializationManager.Singleton.ForceUpdateSpecialization()` (or subscribe to
   `RewiredInputManager.OnInputSpecializationChanged` and call it there) to force a re-localization
   pass whenever the input device changes.
