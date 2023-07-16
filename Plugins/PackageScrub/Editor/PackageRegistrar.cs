#define DEBUG_PACKAGE_REGISTRAR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace PluginScrub
{
    // TODO make dependency to some assembly with that and remove
    internal static class StopWatchHelper
    {
        public static double PassedSeconds(long fromTimestamp)
        {
            return (Stopwatch.GetTimestamp() - fromTimestamp) / (double) TimeSpan.TicksPerSecond;
        }
    }
    
    [Serializable]
    internal class State
    {
        public string PackageName;
        public double TimeSinceEditorStartup;
        public List<string> Files = new List<string>();
        
        internal State(string packageName)
        {
            PackageName = packageName;
            TimeSinceEditorStartup = EditorApplication.timeSinceStartup;
        }
    }
    
    [InitializeOnLoad]
    internal class PackageRegistrar : AssetPostprocessor
    {
        [Serializable]
        internal class PackageData
        {
            // ReSharper disable once InconsistentNaming
            public string Name;
            // ReSharper disable once InconsistentNaming
            public List<ImportData> Imports = new List<ImportData>();

            public PackageData(string packageName)
            {
                Name = packageName;
            }
        }

        [Serializable]
        internal class ImportData
        {
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once NotAccessedField.Local
            public long Timestamp;
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once NotAccessedField.Local
            public List<string> Files = new List<string>();
        }

        private static State _state;
        internal static readonly string RegistryDir =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PackageHistory"));
        
        static PackageRegistrar()
        {
            // playmode speedup and no-spam - skip everything on playmode changes
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            
            InitState();
            //LogDebug("PackageRegistrar: s_ctor", _state?.PackageName ?? "no package");
            AssetDatabase.importPackageStarted += OnImportPackageStarted;
            AssetDatabase.importPackageCancelled += OnImportPackageCancelled; // TODO report this event not fired on closing import package window from AssetStore
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            AssetDatabase.importPackageFailed += OnImportPackageFailed;
        }

        
        private static void InitState()
        {
            // writing state to PlayerPrefs to save it between recompiles (reproduced - importing package with code
            // causes recompile: s_ctor -> importStarted -> s_ctor -> importCompleted)
            LoadState();
        
            // there can be situation when app crash after import started, so state is not null, but
            // we wount receive proper completion
            // so, need to reset state on next editor launch.
            // I understand, that there was a relaunch between InitState() calls comparing EditorTimespans

            if (_state != null && _state.TimeSinceEditorStartup > EditorApplication.timeSinceStartup)
            {
                Debug.Log($"PackageRegistrar({_state.PackageName}): import started, but then unity was closed before import finished. Losing information about {_state.Files.Count} files");
                _state = null;
                SaveState();
            }
        }

        private static void SaveState()
        {
            PlayerPrefs.SetString("PackageRegistrar.State", _state == null ? null : JsonUtility.ToJson(_state));
        }

        private static void LoadState()
        {
            var temp = PlayerPrefs.GetString("PackageRegistrar.State");
            _state = string.IsNullOrEmpty(temp) ? null : JsonUtility.FromJson<State>(temp); // unity shit framework returns "" even after you called Prefs.SetString(null)
        }

        // to fix bug when OnImportPackageCancelled not delivered when just closing import window with cross
        [MenuItem("Assets/Import Package/Stop recording history")]
        internal static void StopRecordingHistory()
        {
            Debug.Log($"PackageRegistrar({_state.PackageName ?? "no package"}): stop recording history");
            _state = null;
            SaveState();
        }

        // // not working, only happens on some asset types
        // // using OnPreprocessAsset, not OnPostprocessAllAssets(imported).
        // // Because when importing again (means, overwriting existing files with the same),
        // // OnPostprocessAllAssets gives more files (dependencies?) then OnPreprocessAsset.
        // // Sadly, didn't find method that don't give any
        // void OnPreprocessAsset()
        // {
        //     if (_state == null)
        //     {
        //         return;
        //     }
        //     var startTimestamp = Stopwatch.GetTimestamp();
        //     var registered = RegisterAssetIfNeeded(assetPath);
        //     LogDebug($"{(registered ? "registered" : "skipped")}({StopWatchHelper.PassedSeconds(startTimestamp)} s) {assetPath}");
        // }

        // TODO do not register overwrites with same files and dependent assets (also imported)
        internal static void OnPostprocessAllAssets(
            string[] importedAssets
            , string[] deleted
            , string[] movedAssets
            , string[] movedFrom)
        {
            if (_state == null)
            {
                return;
            }
            
            var startTimestamp = Stopwatch.GetTimestamp();
            LogDebugIfAny("importedAssets", importedAssets);
        
            {
                var oldCount = _state.Files.Count;
        
                foreach (var importedAsset in importedAssets)
                {
                    RegisterAssetIfNeeded(importedAsset);
                }
                
                // BTW Reproduced, when 2 calls OnPostProcessAllAssets goes on one package import:
                // first one is the import of directory
                // second - of content
                // We don't write empty imports - it's useless
        
                // TODO uncomment if recompile happen during imports
                // if (_state.Files.Count != oldCount)
                // {
                //     SaveState(); // saving state each time because recompile can happen
                // }
        
                var registeredCount = _state.Files.Count - oldCount;
                LogDebug($"registered {registeredCount} files in {StopWatchHelper.PassedSeconds(startTimestamp)} s\n{string.Join("\n", _state.Files.Skip(oldCount).Take(registeredCount))}");
            }
        }

        private static void OnImportPackageStarted(string packagename)
        {
            LogDebug("OnImportPackageStarted", packagename);
            if (_state != null)
            {
                Debug.LogWarning($"PackageRegistrar({packagename}): OnImportPackageStarted: package {_state.PackageName} import started, but not finished. Now importing {packagename}. So old state discarded");
            }
            _state = new State(packagename);
            SaveState();
        }
        
        private static void OnImportPackageCompleted(string packagename)
        {
            try
            {
// #if DEBUG_PACKAGE_REGISTRAR
//                 using (SimpleLogProfiler.Use($"PackageRegistrar({_state.PackageName}): Write to file")) // TODO
// #endif // TODO
                {
                    var packageDataPath = Path.Combine(RegistryDir, _state.PackageName + ".json");
                    Debug.Log($"PackageRegistrar({packagename}): Registering imported files({_state.Files.Count}) to {packageDataPath}");
                    Assert.AreEqual(_state.PackageName, packagename);

                    Directory.CreateDirectory(RegistryDir);
                    var packageData = ReadPackageData(_state.PackageName, packageDataPath);

                    var outImportData = new ImportData { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Files = _state.Files };
                    outImportData.Files.Sort();
                    
                    packageData.Imports.Add(outImportData);
                    WritePackageData(packageData, packageDataPath);
                }
            }
            finally
            {
                _state = null;
                SaveState();
            }
        }
        
        private static void OnImportPackageFailed(string packagename, string errorMessage)
        {
            try
            {
                LogDebug($"OnImportPackageFailed: error = {errorMessage} imported = {_state.Files.Count}", packagename);
                Assert.AreEqual(_state.PackageName, packagename);
            }
            finally
            {
                _state = null;
                SaveState();
            }
        }
        
        private static void OnImportPackageCancelled(string packagename)
        {
            try
            {
                LogDebug($"OnImportPackageCancelled imported = {_state.Files.Count}", packagename);
                Assert.AreEqual(_state.PackageName, packagename);
            }
            finally
            {
                _state = null;
                SaveState();
            }
        }

        private static bool RegisterAssetIfNeeded(string assetPath)
        {
            if (Directory.Exists(UnityPathHelper.AssetPathToAbs(assetPath)))
            {
                // not registering directories, because when file is added to existing dir, that directory must not
                // be deleted on package uninstall
                // LogDebug($"skipping {importedAsset} because it is directory");
                return false;
            }

            if (!_state.Files.Contains(assetPath))
            {
                //LogDebug($"registering {importedAsset}");
                _state.Files.Add(assetPath);
                return true;
            }

            return false;
        }

        internal static PackageData ReadPackageData(string packageDataPath)
        {
            var str = File.ReadAllText(packageDataPath);
            return JsonUtility.FromJson<PackageData>(str);
        }

        private static PackageData ReadPackageData(string packageName, string packageDataPath)
        {
            if (!File.Exists(packageDataPath))
            {
                return new PackageData(packageName);
            }

            return ReadPackageData(packageDataPath);
        }

        private static void WritePackageData(PackageData packageData, string packageDataPath)
        {
            var str = JsonUtility.ToJson(packageData, true);
            
            File.WriteAllText(packageDataPath, str);
            LogDebug($"Wrote {packageDataPath}", packageData.Name);
        }
        
        [Conditional("DEBUG_PACKAGE_REGISTRAR")]
        private static void LogDebugIfAny(string logName, string[] assets)
        {
            if (assets.Length > 0)
            {
                Debug.Log($"PackageRegistrar({_state.PackageName}): {logName}({assets.Length}): {string.Join(", ", assets)}");
            }
        }

        [Conditional("DEBUG_PACKAGE_REGISTRAR")]
        private static void LogDebug(string msg, string packageName = null)
        {
            Debug.Log($"PackageRegistrar({packageName ?? _state.PackageName}): {msg}");
        }
    }
}