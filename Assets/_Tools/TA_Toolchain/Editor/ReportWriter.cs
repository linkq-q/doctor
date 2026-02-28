using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TA.ToolSuite;

namespace TA.Toolchain.RenderAudit
{
    public static class ReportWriter
    {
        public static string WriteReport(RenderAuditReport report, string outputDir)
        {
            var directory = NormalizeOutputDirectory(outputDir);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileName = $"RenderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var fullPath = Path.Combine(directory, fileName);
            var json = JsonUtility.ToJson(report, true);
            File.WriteAllText(fullPath, json);

            AssetDatabase.Refresh();
            return ToAssetRelativePath(fullPath);
        }

        private static string NormalizeOutputDirectory(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                var sceneName = EditorSceneManager.GetActiveScene().name;
                return TTS_ReportRouter.ToAbsolutePath(TTS_ReportRouter.RouteRenderAuditOutputDir(sceneName));
            }

            if (outputDir.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(outputDir);
            }

            return Path.GetFullPath(Path.Combine("Assets", outputDir));
        }

        private static string ToAssetRelativePath(string absolutePath)
        {
            var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!absolutePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath;
            }

            var relative = absolutePath.Substring(projectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace('\\', '/');
        }
    }
}
