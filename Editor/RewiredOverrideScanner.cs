using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Finds pause-policy overrides in serialized (text YAML) scenes/prefabs that would break mobile best
    /// practices. Pure text logic, no Unity API, so it is unit-testable and cheap to run on every build.
    /// Field values match the serialized enum numbers: AppBackgroundPauseMode.Overlay = 3, AutoToggle.On = 1.
    /// </summary>
    public static class RewiredOverrideScanner
    {
        // Direct component fields ("  Field: value") and prefab-instance modifications ("propertyPath: Field" then "value: N").
        private static readonly Regex OverrideOn = Field("OverridePlatformDefaults", 1);
        private static readonly Regex ForcedOverlay = Field("PauseOnAppBackground", 3);
        private static readonly Regex ForcedDisconnectPause = Field("PauseOnControllerDisconnect", 1);

        private static Regex Field(string name, int value) => new Regex(
            @"^\s*" + name + @":\s*" + value + @"\s*$|propertyPath:\s*" + name + @"\s*\r?\n\s*value:\s*" + value + @"\s*$",
            RegexOptions.Multiline);

        private static readonly Regex GamePausedEmpty = new Regex(@"^\s*GamePaused:\s*\{fileID:\s*0\}\s*$", RegexOptions.Multiline);

        public const string MissingPauseScreenIssue =
            "The RewiredInputManager has no Game Paused screen assigned: on desktop/console/TV the game freezes and players see nothing. " +
            "Fix it in Tools > Wagenheimer > Rewired Helper > Dashboard > Setup Audit (Create Pause Screen).";

        /// <summary>True when the serialized manager (a component written directly in this scene/prefab) has an empty Game Paused reference.</summary>
        public static bool HasMissingPauseScreen(string yaml) => !string.IsNullOrEmpty(yaml) && GamePausedEmpty.IsMatch(yaml);

        public const string OverlayIssue =
            "Override Platform Defaults forces PauseOnAppBackground = Overlay: on a mobile build every notification shade, ad, IAP sheet " +
            "or app switch will freeze the game and show the pause screen.";

        public const string DisconnectIssue =
            "Override Platform Defaults forces PauseOnControllerDisconnect = On: Bluetooth pads/remotes connecting and disconnecting " +
            "will pause a touch game on a mobile build.";

        /// <summary>Returns the issues an explicit override causes on mobile, or an empty list when the policy is automatic.</summary>
        public static List<string> FindMobileBreakingOverrides(string yaml)
        {
            var issues = new List<string>();
            if (string.IsNullOrEmpty(yaml) || !OverrideOn.IsMatch(yaml)) return issues;

            if (ForcedOverlay.IsMatch(yaml)) issues.Add(OverlayIssue);
            if (ForcedDisconnectPause.IsMatch(yaml)) issues.Add(DisconnectIssue);
            return issues;
        }
    }
}
