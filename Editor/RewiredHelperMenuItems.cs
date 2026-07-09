using UnityEditor;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Editor utilities for the Rewired Helper package.
    /// Accessible via <b>Tools → Wagenheimer → Rewired Helper</b> in the Unity menu bar.
    /// </summary>
    internal static class RewiredHelperMenuItems
    {
        [MenuItem("Tools/Wagenheimer/Rewired Helper/Integration Guide (README)", priority = 21)]
        private static void OpenIntegrationGuide()
        {
            Application.OpenURL("https://github.com/wagenheimer/RewiredHelper/blob/main/README.md");
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Report Issue", priority = 22)]
        private static void ReportIssue()
        {
            Application.OpenURL("https://github.com/wagenheimer/RewiredHelper/issues/new");
        }
    }
}
