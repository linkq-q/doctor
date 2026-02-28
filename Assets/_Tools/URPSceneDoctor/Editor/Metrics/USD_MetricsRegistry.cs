using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public enum USD_MetricRenderHint
    {
        Progress01,
        SignedBar,
        Ratio,
        EnumBucket
    }

    public struct USD_MetricValue
    {
        public string DisplayText;
        public float Numeric;
        public bool HasNumeric;
    }

    public interface IUsdMetricProvider
    {
        string Id { get; }
        string DisplayNameZh { get; }
        string DisplayNameEn { get; }
        USD_MetricRenderHint RenderHint { get; }
        USD_MetricValue Evaluate(USD_ImageMetricsAggregate aggregate);
    }

    public static class USD_MetricsRegistry
    {
        private static List<IUsdMetricProvider> _providers;

        public static List<IUsdMetricProvider> Providers
        {
            get
            {
                if (_providers != null) return _providers;
                _providers = new List<IUsdMetricProvider>();
                foreach (var type in TypeCache.GetTypesDerivedFrom<IUsdMetricProvider>())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    try { _providers.Add((IUsdMetricProvider)Activator.CreateInstance(type)); }
                    catch { }
                }
                _providers = _providers.OrderBy(x => x.Id).ToList();
                return _providers;
            }
        }

        public static string DisplayName(IUsdMetricProvider p) => USD_Loc.CurrentLang() == "zh" ? p.DisplayNameZh : p.DisplayNameEn;
    }

    internal static class USD_MetricDrawUtil
    {
        public static void DrawMetricBar(IUsdMetricProvider provider, USD_MetricValue value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(USD_MetricsRegistry.DisplayName(provider), GUILayout.Width(220));
            if (!value.HasNumeric)
            {
                EditorGUILayout.LabelField(value.DisplayText);
                EditorGUILayout.EndHorizontal();
                return;
            }

            switch (provider.RenderHint)
            {
                case USD_MetricRenderHint.Progress01:
                    EditorGUI.ProgressBar(GUILayoutUtility.GetRect(220, 18), Mathf.Clamp01(value.Numeric), value.DisplayText);
                    break;
                case USD_MetricRenderHint.SignedBar:
                    var t = Mathf.InverseLerp(-1f, 1f, value.Numeric);
                    EditorGUI.ProgressBar(GUILayoutUtility.GetRect(220, 18), t, value.DisplayText);
                    break;
                case USD_MetricRenderHint.Ratio:
                    EditorGUI.ProgressBar(GUILayoutUtility.GetRect(220, 18), Mathf.InverseLerp(0f, 2f, value.Numeric), value.DisplayText);
                    break;
                default:
                    EditorGUILayout.LabelField(value.DisplayText);
                    break;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
