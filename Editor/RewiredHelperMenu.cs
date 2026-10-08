namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Single source of truth for the Tools > Wagenheimer > Rewired Helper menu layout. Priorities leave a gap of
    /// more than 10 between groups so Unity draws a separator between them.
    /// </summary>
    internal static class RewiredHelperMenu
    {
        public const string Root = "Tools/Wagenheimer/Rewired Helper";

        // Entry points
        public const string Dashboard = Root + "/Dashboard...";
        public const int DashboardPriority = 0;
        public const string VerifySetup = Root + "/Verify Setup...";
        public const int VerifySetupPriority = 1;

        // Setup/ (create what is missing)
        private const string SetupGroup = Root + "/Setup/";
        public const string CreateInputManager = SetupGroup + "Create Rewired Input Manager";
        public const int CreateInputManagerPriority = 20;
        public const string CreateEventSystem = SetupGroup + "Create Rewired Event System";
        public const int CreateEventSystemPriority = 21;
        public const string CreateOnScreenKeyboard = SetupGroup + "Create On-Screen Keyboard";
        public const int CreateOnScreenKeyboardPriority = 22;
        public const string CreateControllerHelp = SetupGroup + "Create Controller Help Form";
        public const int CreateControllerHelpPriority = 23;
        public const string EnsureAndroidGlyphs = SetupGroup + "Ensure Android Remote Glyphs";
        public const int EnsureAndroidGlyphsPriority = 24;
        public const string CreateUiActions = SetupGroup + "Create Rewired UI Actions (menu navigation)";
        public const int CreateUiActionsPriority = 25;

        // Fix/ (repair existing wiring)
        private const string FixGroup = Root + "/Fix/";
        public const string FixCursorWiring = FixGroup + "Verify && Fix Game Cursor Wiring";
        public const int FixCursorWiringPriority = 40;
        public const string RemoveDuplicateEventSystems = FixGroup + "Remove Duplicate Event Systems (All Scenes)";
        public const int RemoveDuplicateEventSystemsPriority = 41;

        // Migrate/ (legacy projects)
        private const string MigrateGroup = Root + "/Migrate/";
        public const string MigrateLegacyComponent = MigrateGroup + "Legacy RewiredHelper Component...";
        public const int MigrateLegacyComponentPriority = 60;
        public const string MigrateLegacyDialog = MigrateGroup + "Legacy Dialog (delete old Dialog.cs)";
        public const int MigrateLegacyDialogPriority = 61;

        // Utilities and links
        public const string ResetWindowPosition = Root + "/Reset Window Position";
        public const int ResetWindowPositionPriority = 90;
        public const string IntegrationGuide = Root + "/Integration Guide (README)";
        public const int IntegrationGuidePriority = 100;
        public const string ReportIssue = Root + "/Report Issue";
        public const int ReportIssuePriority = 101;
        public const string CheckForUpdates = Root + "/Check for Updates...";
        public const int CheckForUpdatesPriority = 110;
    }
}
