using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_I18nAudit
    {
        private static readonly string[] UiApis =
        {
            "GUILayout.Button", "EditorGUILayout.LabelField", "EditorGUILayout.HelpBox", "EditorGUILayout.ToggleLeft", "EditorGUILayout.TextField",
            "EditorGUILayout.Popup", "EditorGUILayout.IntSlider", "EditorGUILayout.ObjectField", "EditorGUILayout.EnumPopup", "EditorUtility.DisplayDialog",
            "GUILayout.SelectionGrid"
        };

        [MenuItem("Tools/URP Scene Doctor/I18N Audit")]
        public static void Run()
        {
            var files = Directory.GetFiles("Assets/_Tools/URPSceneDoctor/Editor", "*.cs", SearchOption.AllDirectories);
            var issues = new List<string>();
            var regex = new Regex("\"[^\"]+\"");
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (var api in UiApis)
                    {
                        if (!line.Contains(api)) continue;
                        if (line.Contains("USD_Loc.T(") || line.Contains("USD_Loc.C(") || line.Contains("USD_Localization.T(")) continue;
                        if (regex.IsMatch(line))
                        {
                            issues.Add($"{file}:{i + 1}: {line.Trim()}");
                        }
                    }
                }
            }

            var reportPath = "Assets/_Tools/URPSceneDoctor/Reports/i18n_audit_report.txt";
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Reports");
            File.WriteAllLines(reportPath, issues);
            AssetDatabase.Refresh();
            Debug.Log($"[URPSceneDoctor][i18n] audit done. issues={issues.Count}, report={reportPath}");
            if (issues.Count == 0) EditorUtility.DisplayDialog(USD_Loc.T("i18n.auditTitle"), USD_Loc.T("i18n.auditZero"), USD_Loc.T("common.ok"));
            else EditorUtility.DisplayDialog(USD_Loc.T("i18n.auditTitle"), USD_Loc.T("i18n.auditFound", issues.Count, reportPath), USD_Loc.T("common.ok"));
        }
    }
}
