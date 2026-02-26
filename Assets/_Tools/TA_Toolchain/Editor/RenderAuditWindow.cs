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
        private Vector2 _windowScroll;
        private IssueSeverityFilter _severityFilter = IssueSeverityFilter.All;
        private string _categoryFilter = "All";
        private string _search = string.Empty;
        private int _selectedIndex = -1;
        private string _lastReportPath;

        private const string ApiKeyPrefs = "TA_TOOLCHAIN_DEEPSEEK_API_KEY";
        private bool _aiFoldout = true;
        private string _aiApiKey = string.Empty;
        private string _aiEndpoint = "https://api.deepseek.com/chat/completions";
        private string _aiModel = "deepseek-reasoner";
        private float _aiTemperature = 0.2f;
        private int _aiTimeoutSeconds = 20;
        private int _aiMaxTokens = 320;
        private int _aiMaxInputChars = 12000;
        private string _aiStatus = "Ready";
        private string _aiOutput = string.Empty;
        private string _latestReportAbsolutePath = string.Empty;
        private string _latestReportInfo = "No report found.";

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

        private void OnEnable()
        {
            _aiApiKey = EditorPrefs.GetString(ApiKeyPrefs, string.Empty);
            RefreshLatestReportInfo();
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

            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
            DrawFilters();
            DrawSummary();
            DrawResults();
            DrawTopOffenders();
            DrawAISummarySection();

            if (!string.IsNullOrEmpty(_lastReportPath))
            {
                EditorGUILayout.HelpBox($"Last report: {_lastReportPath}", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
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
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
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
            RefreshLatestReportInfo();
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


        private void DrawAISummarySection()
        {
            EditorGUILayout.Space();
            _aiFoldout = EditorGUILayout.Foldout(_aiFoldout, "AI Summary (DeepSeek)", true);
            if (!_aiFoldout)
            {
                return;
            }

            EditorGUILayout.LabelField($"Status: {_aiStatus}");
            EditorGUILayout.LabelField($"Report: {_latestReportInfo}");

            var newKey = EditorGUILayout.PasswordField("API Key", _aiApiKey);
            if (!string.Equals(newKey, _aiApiKey, StringComparison.Ordinal))
            {
                _aiApiKey = newKey;
                EditorPrefs.SetString(ApiKeyPrefs, _aiApiKey ?? string.Empty);
            }

            _aiEndpoint = EditorGUILayout.TextField("Endpoint", _aiEndpoint);
            _aiModel = EditorGUILayout.TextField("Model", _aiModel);
            _aiTemperature = EditorGUILayout.Slider("Temperature", _aiTemperature, 0f, 1f);
            _aiTimeoutSeconds = EditorGUILayout.IntField("Timeout Seconds", _aiTimeoutSeconds);
            _aiMaxTokens = EditorGUILayout.IntField("Max Tokens", _aiMaxTokens);
            _aiMaxInputChars = EditorGUILayout.IntField("Max Input Chars", _aiMaxInputChars);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成AI精简建议", GUILayout.Height(24)))
                {
                    GenerateAISummary();
                }

                if (GUILayout.Button("刷新报告选择", GUILayout.Height(24)))
                {
                    RefreshLatestReportInfo();
                }
            }

            EditorGUILayout.LabelField("输出（可复制）");
            _aiOutput = EditorGUILayout.TextArea(_aiOutput, GUILayout.MinHeight(120));
        }

        private void GenerateAISummary()
        {
            RefreshLatestReportInfo();
            if (string.IsNullOrEmpty(_latestReportAbsolutePath))
            {
                _aiStatus = "Failed -> Fallback";
                _aiOutput = RenderAuditLocalFallback.Build(string.Empty);
                return;
            }

            _aiStatus = "Calling...";
            _aiOutput = string.Empty;

            var settings = new RenderAuditAISummarizer.Settings
            {
                apiKey = _aiApiKey,
                endpoint = _aiEndpoint,
                model = _aiModel,
                temperature = _aiTemperature,
                timeoutSeconds = Mathf.Max(1, _aiTimeoutSeconds),
                maxTokens = Mathf.Max(64, _aiMaxTokens),
                maxInputChars = Mathf.Max(1000, _aiMaxInputChars)
            };

            RenderAuditAISummarizer.Generate(_latestReportAbsolutePath, settings, (text, error) =>
            {
                _aiOutput = text ?? string.Empty;
                _aiStatus = string.IsNullOrWhiteSpace(error) ? "Ready" : "Failed -> Fallback";
                Repaint();
            });
        }

        private void RefreshLatestReportInfo()
        {
            if (RenderAuditAISummarizer.TryGetLatestReportAbsolutePath(out var absolutePath, out var fileName, out var sizeBytes))
            {
                _latestReportAbsolutePath = absolutePath;
                _latestReportInfo = $"{fileName} ({sizeBytes / 1024f:F1} KB)";
            }
            else
            {
                _latestReportAbsolutePath = string.Empty;
                _latestReportInfo = "No report found in Assets/_Tools/TA_Toolchain/Reports";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F2} MB";
        }
    }
}
