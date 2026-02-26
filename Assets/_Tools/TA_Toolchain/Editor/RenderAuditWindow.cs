using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TA.Toolchain.RenderAudit
{
    public sealed class RenderAuditWindow : EditorWindow
    {
        private RenderAuditConfig _config;
        private RenderAuditScanResult _scanResult;
        private Vector2 _scroll;
        private IssueSeverityFilter _severityFilter = IssueSeverityFilter.All;
        private string _categoryFilter = "All";
        private string _search = string.Empty;
        private int _selectedIndex = -1;
        private string _lastReportPath;

        private enum IssueSeverityFilter
        {
            All,
            Error,
            Warning,
            Info
        }

        [MenuItem("Tools/TA Toolchain/Render Audit")]
        public static void Open()
        {
            GetWindow<RenderAuditWindow>("Render Audit");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            _config = (RenderAuditConfig)EditorGUILayout.ObjectField("Config", _config, typeof(RenderAuditConfig), false);
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assign a RenderAuditConfig asset to run audit.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _config != null;
                if (GUILayout.Button("Run Audit", GUILayout.Height(28)))
                {
                    RunAudit();
                }

                GUI.enabled = true;
                if (GUILayout.Button("Open Reports Folder", GUILayout.Height(28)))
                {
                    OpenReportsFolder();
                }

                GUI.enabled = _scanResult != null && _selectedIndex >= 0;
                if (GUILayout.Button("Copy Selected Item", GUILayout.Height(28)))
                {
                    CopySelectedItem();
                }
                GUI.enabled = true;
            }

            DrawFilters();
            DrawSummary();
            DrawResults();
            DrawTopOffenders();

            if (!string.IsNullOrEmpty(_lastReportPath))
            {
                EditorGUILayout.HelpBox($"Last report: {_lastReportPath}", MessageType.None);
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                _severityFilter = (IssueSeverityFilter)EditorGUILayout.EnumPopup("Severity", _severityFilter, GUILayout.Width(220));

                var categories = new List<string> { "All" };
                categories.AddRange(Enum.GetNames(typeof(IssueCategory)));
                var idx = Math.Max(0, categories.IndexOf(_categoryFilter));
                idx = EditorGUILayout.Popup("Category", idx, categories.ToArray(), GUILayout.Width(260));
                _categoryFilter = categories[idx];

                _search = EditorGUILayout.TextField("Search", _search);
            }
        }

        private void DrawSummary()
        {
            if (_scanResult?.report?.summary == null)
            {
                return;
            }

            var s = _scanResult.report.summary;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Issues: {s.issueCount} (E:{s.errorCount} / W:{s.warningCount} / I:{s.infoCount})");
            EditorGUILayout.LabelField($"Renderers: {s.rendererCount} (Transparent: {s.transparentRendererCount})");
            EditorGUILayout.LabelField($"Lights: {s.lightCount} (Realtime shadow: {s.realtimeShadowLightCount})");
            EditorGUILayout.LabelField($"ParticleSystems: {s.particleSystemCount}, ReflectionProbes: {s.reflectionProbeCount}");
        }

        private void DrawResults()
        {
            if (_scanResult == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);

            var filtered = FilterEntries(_scanResult.entries);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220));
            for (var i = 0; i < filtered.Count; i++)
            {
                var entry = filtered[i];
                var issue = entry.issue;
                var style = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
                if (GUILayout.Button($"[{issue.severity}] [{issue.category}] {issue.title} - {issue.detail}", style))
                {
                    _selectedIndex = _scanResult.entries.IndexOf(entry);
                    if (_config != null && _config.pingSceneObjectsAndAssets && entry.pingTarget != null)
                    {
                        EditorGUIUtility.PingObject(entry.pingTarget);
                        Selection.activeObject = entry.pingTarget;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTopOffenders()
        {
            if (_scanResult?.report?.topOffenders == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Top 20 textures by estimated memory", EditorStyles.boldLabel);
            foreach (var t in _scanResult.report.topOffenders.textures)
            {
                EditorGUILayout.LabelField($"{FormatBytes(t.estimatedBytes)}  {t.assetPath}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Top 20 meshes by triangles", EditorStyles.boldLabel);
            foreach (var m in _scanResult.report.topOffenders.meshes)
            {
                EditorGUILayout.LabelField($"{m.triangles} tris / {m.vertices} verts  {m.hierarchyPath}");
            }
        }

        private void RunAudit()
        {
            _scanResult = RenderAuditScanner.Scan(_config);
            _lastReportPath = ReportWriter.WriteReport(_scanResult.report, _config.outputDir);
            Repaint();
        }

        private void OpenReportsFolder()
        {
            var dir = _config != null ? _config.outputDir : "Assets/_Tools/TA_Toolchain/Reports/";
            var full = Path.GetFullPath(dir.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? dir : Path.Combine("Assets", dir));
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
                AssetDatabase.Refresh();
            }
            EditorUtility.RevealInFinder(full);
        }

        private void CopySelectedItem()
        {
            if (_scanResult == null || _selectedIndex < 0 || _selectedIndex >= _scanResult.entries.Count)
            {
                return;
            }

            var issue = _scanResult.entries[_selectedIndex].issue;
            EditorGUIUtility.systemCopyBuffer = $"[{issue.severity}/{issue.category}] {issue.title}\n{issue.detail}\nTarget: {issue.targetPath}";
        }

        private List<ScanResultEntry> FilterEntries(List<ScanResultEntry> entries)
        {
            return entries.Where(e =>
            {
                if (_severityFilter != IssueSeverityFilter.All && !string.Equals(e.issue.severity.ToString(), _severityFilter.ToString(), StringComparison.Ordinal))
                {
                    return false;
                }

                if (_categoryFilter != "All" && !string.Equals(e.issue.category.ToString(), _categoryFilter, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_search))
                {
                    var haystack = $"{e.issue.title} {e.issue.detail} {e.issue.targetPath}";
                    if (haystack.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F2} MB";
        }
    }
}
