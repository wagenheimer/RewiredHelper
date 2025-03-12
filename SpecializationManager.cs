namespace I2.Loc
{
    public partial class SpecializationManager
    {
        /// <summary>
        /// Determines the current input device specialization to adapt UI/messaging.
        /// Ensures consistent cross-platform experiences by detecting active input type.
        /// </summary>
        /// <returns>
        /// Specialization name as a string:
        /// • "Touch" for touchscreen devices
        /// • "PC" for mouse input
        /// • "Controller" for gamepad/joystick
        /// • Default fallback: "PC"
        /// </returns>
        public override string GetCurrentSpecialization()
        {
            // Original note: "If using joystick, replace all Click with Tap" - now obsolete.
            // Current implementation only detects specialization without modifying UI text.

            // Prioritize touch input detection for mobile devices
            if (RewiredHelper.IsUsingTouch)
            {
                return "Touch";
            }

            // Detect active controller when Rewired is initialized
            if (RewiredHelper.instance?.UltimoControleAtivo != null)
            {
                return RewiredHelper.instance.UltimoControleAtivo.type switch
                {
                    Rewired.ControllerType.Mouse => "PC",        // Mouse implies desktop
                    Rewired.ControllerType.Joystick => "Controller", // Gamepad/joystick
                    _ => "Touch"                                // Unmapped controller types
                };
            }

            // Default to PC input when no controllers detected
            return "PC";
        }
    }
}