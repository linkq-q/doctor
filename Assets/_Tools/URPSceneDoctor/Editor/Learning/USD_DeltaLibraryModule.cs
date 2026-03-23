using System.IO;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_DeltaLibraryModule : IToolModule
    {
        public string ModuleName => "Delta Library";
        private USD_DeltaStats _lastStats;

        public void DrawUI(USD_HubWindow hub)
        {
            var library = USD_DeltaStatsUtil.GetOrCreateLibrary();
            EditorGUILayout.HelpBox($"Samples: {library.entries.Count} | Last Updated: {library.lastUpdated}", MessageType.Info);

            if (GUILayout.Button(USD_Loc.C("delta.addLast")))
            {
                AddLastTuningResult(hub, library);
            }

            if (GUILayout.Button(USD_Loc.C("delta.importFolder")))
            {
                var folder = EditorUtility.OpenFolderPanel("Import delta sample folder", Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(folder))
                {
                    ImportFolder(folder, library);
                }
            }

            if (GUILayout.Button(USD_Loc.C("delta.recompute")))
            {
                _lastStats = USD_DeltaStatsUtil.RecomputeStats(library);
                hub.SetLearningStats(_lastStats);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("delta.openSamples"))) EditorUtility.RevealInFinder("Assets/_Tools/URPSceneDoctor/DeltaLibrary/Samples");
            if (GUILayout.Button(USD_Loc.C("delta.openStats"))) EditorUtility.RevealInFinder("Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats");
            EditorGUILayout.EndHorizontal();

            var stats = _lastStats ?? hub.CurrentLearningStats;
            if (stats != null)
            {
                EditorGUILayout.LabelField(USD_Loc.T("delta.topHints"), EditorStyles.boldLabel);
                foreach (var hint in stats.topHints) EditorGUILayout.LabelField(USD_Loc.T("common.bullet", hint), EditorStyles.wordWrappedLabel);
            }
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        private static void AddLastTuningResult(USD_HubWindow hub, USD_DeltaLibraryAsset library)
        {
            if (string.IsNullOrEmpty(hub.LastBeforeSnapshotPath) || string.IsNullOrEmpty(hub.LastAfterSnapshotPath) || string.IsNullOrEmpty(hub.OptionalDeltaPatchPath))
            {
                EditorUtility.DisplayDialog(USD_Loc.T("delta.noTuningTitle"), USD_Loc.T("delta.noTuningMsg"), USD_Loc.T("common.ok"));
                return;
            }

            var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
            var ts = USD_EditorUtil.Timestamp;
            var folder = $"Assets/_Tools/URPSceneDoctor/DeltaLibrary/Samples/{sceneName}/{ts}";
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/DeltaLibrary/Samples");
            USD_EditorUtil.EnsureFolder($"Assets/_Tools/URPSceneDoctor/DeltaLibrary/Samples/{sceneName}");
            USD_EditorUtil.EnsureFolder(folder);

            var dstBefore = $"{folder}/snapshot_before.json";
            var dstAfter = $"{folder}/snapshot_after.json";
            var dstPatch = $"{folder}/deltaPatch.json";
            File.Copy(hub.LastBeforeSnapshotPath, dstBefore, true);
            File.Copy(hub.LastAfterSnapshotPath, dstAfter, true);
            File.Copy(hub.OptionalDeltaPatchPath, dstPatch, true);

            library.entries.Add(new USD_DeltaEntry
            {
                sceneName = sceneName,
                timestamp = ts,
                snapshotBeforePath = dstBefore,
                snapshotAfterPath = dstAfter,
                deltaPatchPath = dstPatch
            });

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ImportFolder(string folderAbsPath, USD_DeltaLibraryAsset library)
        {
            var relative = folderAbsPath.Replace('\\', '/');
            var projectPath = Application.dataPath.Replace("/Assets", string.Empty).Replace('\\', '/');
            if (!relative.StartsWith(projectPath))
            {
                EditorUtility.DisplayDialog(USD_Loc.T("delta.importFailed"), USD_Loc.T("delta.importInsideProject"), USD_Loc.T("common.ok"));
                return;
            }

            var assetPath = relative.Substring(projectPath.Length + 1);
            var patch = assetPath + "/deltaPatch.json";
            if (!File.Exists(patch))
            {
                EditorUtility.DisplayDialog(USD_Loc.T("delta.importFailed"), USD_Loc.T("delta.importMissingPatch"), USD_Loc.T("common.ok"));
                return;
            }

            library.entries.Add(new USD_DeltaEntry
            {
                sceneName = Path.GetFileName(Path.GetDirectoryName(assetPath)),
                timestamp = Path.GetFileName(assetPath),
                deltaPatchPath = patch,
                snapshotBeforePath = assetPath + "/snapshot_before.json",
                snapshotAfterPath = assetPath + "/snapshot_after.json"
            });
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
