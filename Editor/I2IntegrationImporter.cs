using System;
using System.IO;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>Copies the optional I2 Localization specialization sample into the consuming project.</summary>
    internal static class I2IntegrationImporter
    {
        internal static void Import()
        {
            string sourcePath = "Packages/com.wagenheimer.rewiredhelper/Samples~/I2LocalizationIntegration/SpecializationManager.cs";
            string destDir = "Assets/Samples/Rewired Helper/I2 Localization Integration";
            string destPath = Path.Combine(destDir, "SpecializationManager.cs");

            try
            {
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, true);
                    AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                    Debug.Log("[RewiredHelper] Imported I2 Localization Integration successfully!");
                }
                else
                {
                    Debug.LogError($"[RewiredHelper] Integration source file not found at: {sourcePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RewiredHelper] Failed to import I2 Localization Integration: {ex.Message}");
            }
        }
    }
}
