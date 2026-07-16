using UnityEngine;
using Rewired;
using Wagenheimer.RewiredHelper;

namespace I2.Loc
{
    // Requires I2 Localization to already define the partial SpecializationManager class.
    public partial class SpecializationManager
    {
        /// <summary>
        /// Determines the current input specialization ("Touch", "Controller", "PC") so I2
        /// Localization can pick the right term/glyph variant for the active input device.
        /// </summary>
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
