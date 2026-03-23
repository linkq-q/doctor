using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_EditorUtil
    {
        public static readonly string RootFolder = "Assets/_Tools/URPSceneDoctor";

        // Include milliseconds to reduce collisions during quick consecutive writes (e.g. BEFORE/AFTER).
        public static string Timestamp => DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        public static string EnsureSceneSubFolder(string category, string sceneName)
        {
            var safeScene = string.IsNullOrWhiteSpace(sceneName) ? "UntitledScene" : sceneName;
            var path = $"{RootFolder}/{category}/{safeScene}";
            EnsureFolder(path);
            return path;
        }

        public static string MakeBackup(string assetPath)
        {
            if (!File.Exists(assetPath)) return string.Empty;
            var backupPath = assetPath + ".bak_" + Timestamp;
            File.Copy(assetPath, backupPath, true);
            AssetDatabase.Refresh();
            return backupPath;
        }

        public static string EnsureUniqueAssetPath(string candidatePath)
        {
            var normalized = candidatePath.Replace('\\', '/');
            if (!File.Exists(normalized)) return normalized;

            var dir = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            var file = Path.GetFileNameWithoutExtension(normalized);
            var ext = Path.GetExtension(normalized);
            var idx = 1;
            while (true)
            {
                var next = $"{dir}/{file}_{idx}{ext}";
                if (!File.Exists(next)) return next;
                idx++;
            }
        }

        public static void VerboseLog(string message)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            if (settings != null && settings.verboseLogs)
            {
                Debug.Log("[URP Scene Doctor] " + message);
            }
        }

        public static void RegisterCreatedObject(UnityEngine.Object obj, string action)
        {
            // We always register created objects for Undo so users can safely rollback Apply operations.
            Undo.RegisterCreatedObjectUndo(obj, action);
            EditorUtility.SetDirty(obj);
        }

        public static void RecordObject(UnityEngine.Object obj, string action)
        {
            // We record existing object modifications separately to make Undo deterministic.
            Undo.RecordObject(obj, action);
            EditorUtility.SetDirty(obj);
        }
    }

}
