using System.IO;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_SnapshotUtil
    {
        public static string SaveSnapshot(string sceneName, string timestamp, USD_ScanSnapshot snapshot)
        {
            var folder = USD_EditorUtil.EnsureSceneSubFolder("Snapshots", sceneName);
            var path = USD_EditorUtil.EnsureUniqueAssetPath($"{folder}/{timestamp}_snapshot.json");
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
            AssetDatabase.Refresh();
            return path;
        }

        public static USD_ScanSnapshot LoadSnapshot(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<USD_ScanSnapshot>(File.ReadAllText(path));
        }

        public static string SaveDeltaPatch(string sceneName, string timestamp, USD_DeltaPatch patch)
        {
            var folder = USD_EditorUtil.EnsureSceneSubFolder("Patches", sceneName);
            var path = USD_EditorUtil.EnsureUniqueAssetPath($"{folder}/{timestamp}_deltaPatch.json");
            File.WriteAllText(path, JsonUtility.ToJson(patch, true));
            AssetDatabase.Refresh();
            return path;
        }

        public static USD_DeltaPatch LoadDeltaPatch(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<USD_DeltaPatch>(File.ReadAllText(path));
        }
    }
}
