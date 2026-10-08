using UnityEditor;
using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.RewiredHelper.Editor
{
    public static class UpdateChecker
    {
        [MenuItem(RewiredHelperMenu.CheckForUpdates, priority = RewiredHelperMenu.CheckForUpdatesPriority)]
        public static void CheckForUpdateMenu() => CheckForUpdate(true);

        public static void CheckForUpdate(bool force = false)
        {
            PackageHubWindow.OpenToPackage("com.wagenheimer.rewiredhelper");
        }
    }
}
