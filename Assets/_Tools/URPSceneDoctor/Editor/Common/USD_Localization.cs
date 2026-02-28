using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public enum USD_Language
    {
        Auto,
        Zh,
        En
    }

    public static class USD_Localization
    {
        private static readonly Dictionary<string, (string zh, string en)> Dict = new Dictionary<string, (string zh, string en)>
        {
            {"tab.atmos", ("氛围诊断", "Atmosphere Doctor")},
            {"tab.render", ("渲染诊断", "Render Doctor")},
            {"tab.tuning", ("调参(前后)", "Tuning (Before/After)")},
            {"tab.evidence", ("证据包", "Evidence Pack")},
            {"tab.delta", ("增量学习库", "Delta Library")},
            {"tab.pipeline", ("管线包", "Pipeline Pack")},
            {"tab.batch", ("批量采样", "Batch Sampler")},
            {"tab.reports", ("报告", "Reports")},
            {"tab.settings", ("设置", "Settings")},
            {"btn.scan", ("扫描", "Scan")},
            {"btn.dryrun", ("仅分析", "Dry Run")},
            {"btn.apply", ("应用", "Apply")},
            {"btn.export", ("导出报告", "Export Report")},
            {"btn.batchrun", ("开始批跑", "Batch Run")}
        };

        public static bool IsZh()
        {
            var s = USD_Settings.GetOrCreateSettings();
            var lang = s != null ? s.language : "Auto";
            if (lang == "中文") return true;
            if (lang == "English") return false;
            return Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseSimplified || Application.systemLanguage == SystemLanguage.ChineseTraditional;
        }

        public static string T(string key)
        {
            if (!Dict.TryGetValue(key, out var val)) return key;
            return IsZh() ? val.zh : val.en;
        }

        public static string Label(string zh, string en)
        {
            return IsZh() ? zh : en;
        }
    }
}
