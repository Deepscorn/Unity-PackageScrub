using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using File = UnityEngine.Windows.File;

namespace PluginScrub
{
    internal static class PackageRemover
    {
        [MenuItem("Assets/Import Package/Delete ALL installed custom packages")]
        internal static void UninstallPackages()
        {
            if (!EditorUtility.DisplayDialog("Warning",
                "This will delete ALL custom unity packages including those coming from Package Manager. Are you sure?",
                "Yes, delete all", "No-no-no!"))
                return;
            
            foreach (var packageFilePath in Directory.EnumerateFiles(PackageRegistrar.RegistryDir, "*.json"))
            {
                //SimpleLogProfiler.Action($"Delete {packageFilePath}", () => // TODO
                var package = PackageRegistrar.ReadPackageData(packageFilePath);
                foreach (var file in package.Imports.SelectMany(import => import.Files))
                {
                    // not using this because in asset pipeline v2 this calls drawing window with progress. For each asset
                    // this increases removal times in hundreds of times
                    //AssetDatabase.DeleteAsset(file);
                    DeleteAssetNoRefresh(file);
                }
                //TODO DeletePossibleEmptyDirectories(GetDirectories(packageFiles));
                File.Delete(packageFilePath);
                Debug.Log($"Deleted {package.Name}");
            }
            AssetDatabase.Refresh();
        }

        // TODO more methods or maybe gui like here https://assetstore.unity.com/packages/tools/utilities/package-uninstaller-35439

        private static void DeleteAssetNoRefresh(string assetPath)
        {
            var absPath = UnityPathHelper.AssetPathToAbs(assetPath);
            if (File.Exists(absPath))
            {
                File.Delete(absPath);
            }
        
            if (File.Exists(absPath + ".meta"))
            {
                File.Delete(absPath + ".meta");
            }
        }
    }
}