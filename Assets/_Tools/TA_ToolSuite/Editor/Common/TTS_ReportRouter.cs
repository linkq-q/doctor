using System;
using System.IO;
using UnityEngine;

namespace TA.ToolSuite
{
    public static class TTS_ReportRouter
    {
        public const string Root = "Assets/_Tools/TA_ToolSuite";

        public static string Timestamp => DateTime.Now.ToString("yyyyMMdd_HHmmss");

        public static string EnsureToolSceneRunFolder(string channel, string toolName, string sceneName, string timestamp = null)
        {
            var safeScene = string.IsNullOrWhiteSpace(sceneName) ? "UntitledScene" : sceneName;
            var ts = string.IsNullOrWhiteSpace(timestamp) ? Timestamp : timestamp;
            var folder = $"{Root}/{channel}/{toolName}/{safeScene}/{ts}";
            EnsureFolder(folder);
            return folder;
        }

        public static string RouteRenderAuditOutputDir(string sceneName)
        {
            return EnsureToolSceneRunFolder("Reports", "RenderAudit", sceneName);
        }

        public static string RouteAnimReportsOutputDir(string sceneName)
        {
            return EnsureToolSceneRunFolder("Reports", "AnimTools", sceneName);
        }

        public static string RouteAnimControllersOutputDir(string sceneName)
        {
            return EnsureToolSceneRunFolder("Patches", "AnimTools", sceneName);
        }

        public static void EnsureFolder(string assetPath)
        {
            var full = ToAbsolutePath(assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }

        public static string ToAbsolutePath(string assetOrRelative)
        {
            if (Path.IsPathRooted(assetOrRelative)) return assetOrRelative;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, assetOrRelative));
        }
    }
}
