using UnityEngine;
using Rewired;
using I2.Loc;
using Wagenheimer.RewiredHelper;

namespace I2.Loc
{
    /// <summary>
    /// Rewired-aware specialization: returns "Touch", "Controller" or "PC" from the
    /// last active input device so I2 picks the right term/glyph variant.
    ///
    /// This subclass replaces <see cref="SpecializationManager.Singleton"/> at startup,
    /// enabling compatibility with I2 Localization when packaged as an asmdef UPM package.
    /// </summary>
    public sealed class RewiredSpecializationManager : SpecializationManager
    {
        public override string GetCurrentSpecialization()
        {
            if (RewiredInputManager.IsUsingTouch)
                return "Touch";

            if (IsConsolePlatform())
                return "Controller";

            if (RewiredInputManager.Instance != null && RewiredInputManager.Instance.LastActiveController != null)
            {
                return RewiredInputManager.Instance.LastActiveController.type switch
                {
                    ControllerType.Keyboard => "PC",
                    ControllerType.Mouse => "PC",
                    ControllerType.Joystick => "Controller",
                    ControllerType.Custom => "Controller",
                    _ => "PC"
                };
            }

            return "PC";
        }

        private bool IsConsolePlatform()
        {
            return Application.platform switch
            {
                RuntimePlatform.Switch => true,
                RuntimePlatform.PS4 => true,
                RuntimePlatform.PS5 => true,
                RuntimePlatform.XboxOne => true,
                _ => false
            };
        }

        public void ForceUpdateSpecialization()
        {
            LocalizationManager.LocalizeAll(true);
        }
    }
}

namespace Wagenheimer.RewiredHelper.Integration
{
    /// <summary>
    /// Installs the Rewired specialization before the first scene loads.
    /// </summary>
    internal static class I2SpecializationInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (SpecializationManager.Singleton is RewiredSpecializationManager)
                return;

            SpecializationManager.Singleton = new RewiredSpecializationManager();
        }
    }

    // Marker type used by the Editor Setup Checker to detect if this integration is imported.
    internal struct I2SpecializationImportedMarker {}
}
