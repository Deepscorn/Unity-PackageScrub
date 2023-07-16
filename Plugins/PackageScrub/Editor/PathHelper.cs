using System;
using UnityEngine;

namespace PluginScrub
{
    // TODO make dependency to some assembly with that and remove
    internal static class UnityPathHelper
    {
        private static readonly int cutLength = "Assets".Length;

        // Converts e.g. on windows:
        // from C:/Users/Venom/projects/SimpleGame/Assets/1 — копия (5)/2
        // to Assets/1 — копия (5)\2
        // Note: it converts slashes
        // TODO rename to AbsToRelativePath
        internal static string AbsPathToRelativeAssetPath(string absPath)
        {
            return absPath.Substring(Application.dataPath.Length - cutLength).Replace('\\', '/');
        }
        
        // Converts e.g. on windows:
        // from Assets/1 — копия (5)/2
        // to C:/Users/Venom/projects/SimpleGame/Assets/1 — копия (5)/2
        // Note: input string must be in unity format (use / separators)
        // TODO rename to RelativePathToAbs
        internal static string AssetPathToAbs(string assetPath)
        {
            return Application.dataPath.Substring(
                       0, Application.dataPath.LastIndexOf("/", StringComparison.Ordinal) + 1) + assetPath;
        }
    }
}