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
        private static readonly Regex OverrideOn = new Regex(@"^\s*OverridePlatformDefaults:\s*1\s*$", RegexOptions.Multiline);
        private static readonly Regex ForcedOverlay = new Regex(@"^\s*PauseOnAppBackground:\s*3\s*$", RegexOptions.Multiline);
        private static readonly Regex ForcedDisconnectPause = new Regex(@"^\s*PauseOnControllerDisconnect:\s*1\s*$", RegexOptions.Multiline);

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
