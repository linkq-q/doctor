using System.IO;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_MetricsPanelModule : IToolModule
    {
        public string ModuleName => "Metrics";

        private string _folder;
        private USD_ImageMetricsAggregate _before;
        private USD_ImageMetricsAggregate _after;

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.HelpBox(USD_Loc.T("metrics.overview"), MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("metrics.latest"), GUILayout.Width(180)))
            {
                _folder = FindLatestEvidenceFolder(hub.ActiveSceneName);
                LoadMetrics();
            }

            if (GUILayout.Button(USD_Loc.C("btn.pickFolder"), GUILayout.Width(120)))
            {
                var abs = EditorUtility.OpenFolderPanel(USD_Loc.T("btn.pickFolder"), Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(abs))
                {
                    _folder = abs.Replace('\\', '/');
                    LoadMetrics();
                }
            }

            if (GUILayout.Button(USD_Loc.C("metrics.capture"), GUILayout.Width(180)))
            {
                var snap = USD_AtmosScanner.CaptureSnapshot();
                _before = BuildFromSnapshot(snap);
                _after = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(USD_Loc.T("ai.currentFolder"), string.IsNullOrEmpty(_folder) ? USD_Loc.T("common.none") : _folder);
            DrawAggregateBlock(USD_Loc.T("metrics.before"), _before);
            DrawAggregateBlock(USD_Loc.T("metrics.after"), _after);
            DrawCompareTable();
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        private void DrawAggregateBlock(string title, USD_ImageMetricsAggregate aggregate)
        {
            if (aggregate == null) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var provider in USD_MetricsRegistry.Providers)
            {
                var value = provider.Evaluate(aggregate);
                USD_MetricDrawUtil.DrawMetricBar(provider, value);
            }
        }

        private void DrawCompareTable()
        {
            if (_before == null || _after == null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("metrics.compare"), EditorStyles.boldLabel);
            foreach (var provider in USD_MetricsRegistry.Providers)
            {
                var b = provider.Evaluate(_before);
                var a = provider.Evaluate(_after);
                if (!b.HasNumeric || !a.HasNumeric) continue;
                var d = a.Numeric - b.Numeric;
                var warn = provider.Id == "overexposure_ratio" ? d > 0.01f : (provider.Id == "center_contrast_ratio" ? a.Numeric < 1.1f : false);
                var color = GUI.color;
                if (warn) GUI.color = Color.yellow;
                EditorGUILayout.LabelField($"{USD_MetricsRegistry.DisplayName(provider)} | B:{b.Numeric:0.000} A:{a.Numeric:0.000} Δ:{d:+0.000;-0.000}");
                GUI.color = color;
            }
        }

        private void LoadMetrics()
        {
            if (string.IsNullOrEmpty(_folder)) return;
            var beforePath = Path.Combine(_folder, "image_metrics_before.json").Replace('\\', '/');
            var afterPath = Path.Combine(_folder, "image_metrics_after.json").Replace('\\', '/');
            _before = File.Exists(beforePath) ? JsonUtility.FromJson<USD_ImageMetricsFile>(File.ReadAllText(beforePath)).aggregate : null;
            _after = File.Exists(afterPath) ? JsonUtility.FromJson<USD_ImageMetricsFile>(File.ReadAllText(afterPath)).aggregate : null;
        }

        private static USD_ImageMetricsAggregate BuildFromSnapshot(USD_ScanSnapshot snapshot)
        {
            return new USD_ImageMetricsAggregate
            {
                brightness_bucket = "mid",
                luma_mean = 0.5f,
                luma_std = Mathf.Clamp01(snapshot.shadowDistance / 100f),
                overexposure_ratio = 0f,
                center_contrast_ratio = 1f,
                saturation_mean = 0.5f,
                warm_cool_balance = Mathf.Clamp(snapshot.dirLightTemperature / 6500f - 1f, -1f, 1f)
            };
        }

        private static string FindLatestEvidenceFolder(string sceneName)
        {
            var root = Path.Combine(Application.dataPath, "_Tools/URPSceneDoctor/EvidencePacks/" + sceneName).Replace('\\', '/');
            if (!Directory.Exists(root)) return string.Empty;
            var dirs = Directory.GetDirectories(root);
            if (dirs.Length == 0) return string.Empty;
            System.Array.Sort(dirs);
            return dirs[dirs.Length - 1].Replace('\\', '/');
        }
    }
}
