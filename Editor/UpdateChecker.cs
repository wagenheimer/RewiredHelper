using UnityEditor;
using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.RewiredHelper.Editor
{
    public static class UpdateChecker
    {
        [MenuItem("Tools/Wagenheimer/Rewired Helper/Check for Updates...", priority = 100)]
        public static void CheckForUpdateMenu() => CheckForUpdate(true);

        public static void CheckForUpdate(bool force = false)
        {
            PackageHubWindow.OpenToPackage("com.wagenheimer.rewiredhelper");
        }
    }
}
