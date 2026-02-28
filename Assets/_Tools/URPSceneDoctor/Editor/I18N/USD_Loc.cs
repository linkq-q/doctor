using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_Loc
    {
        private const string LangPrefKey = "USD_LANG";
        private const string TablePath = "Assets/_Tools/URPSceneDoctor/Config/Localization/LocTable_Default.asset";

        private static Dictionary<string, (string zh, string en)> _table;
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();

        public static string CurrentLang()
        {
            var mode = EditorPrefs.GetString(LangPrefKey, "Auto");
            if (mode == "中文") return "zh";
            if (mode == "English") return "en";
            return IsSystemZh() ? "zh" : "en";
        }

        public static void SetLanguageMode(string mode)
        {
            EditorPrefs.SetString(LangPrefKey, string.IsNullOrWhiteSpace(mode) ? "Auto" : mode);
            InternalEditorUtility.RepaintAllViews();
        }

        public static string GetLanguageMode() => EditorPrefs.GetString(LangPrefKey, "Auto");

        public static string T(string key)
        {
            EnsureLoaded();
            if (!_table.TryGetValue(key, out var val))
            {
                if (MissingLogged.Add(key)) Debug.LogWarning("[URPSceneDoctor][i18n] Missing key: " + key);
                return "[[" + key + "]]";
            }

            return CurrentLang() == "zh" ? val.zh : val.en;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static GUIContent C(string key, string tooltipKey = null)
        {
            return new GUIContent(T(key), string.IsNullOrEmpty(tooltipKey) ? string.Empty : T(tooltipKey));
        }

        private static void EnsureLoaded()
        {
            if (_table != null) return;
            _table = BuildFallback();

            var asset = AssetDatabase.LoadAssetAtPath<USD_LocalizationTableAsset>(TablePath);
            if (asset == null)
            {
                EnsureDefaultAsset();
                asset = AssetDatabase.LoadAssetAtPath<USD_LocalizationTableAsset>(TablePath);
            }

            if (asset == null) return;
            foreach (var e in asset.entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.key)))
            {
                _table[e.key] = (string.IsNullOrEmpty(e.zh) ? e.key : e.zh, string.IsNullOrEmpty(e.en) ? e.key : e.en);
            }
        }

        private static void EnsureDefaultAsset()
        {
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Config");
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Config/Localization");
            var asset = ScriptableObject.CreateInstance<USD_LocalizationTableAsset>();
            asset.entries = BuildFallback().Select(kv => new USD_LocEntry { key = kv.Key, zh = kv.Value.zh, en = kv.Value.en }).ToList();
            AssetDatabase.CreateAsset(asset, TablePath);
            AssetDatabase.SaveAssets();
        }

        private static bool IsSystemZh()
        {
            var l = Application.systemLanguage;
            return l == SystemLanguage.Chinese || l == SystemLanguage.ChineseSimplified || l == SystemLanguage.ChineseTraditional;
        }

        private static Dictionary<string, (string zh, string en)> BuildFallback()
        {
            return new Dictionary<string, (string zh, string en)>
            {
                {"tab.atmos",("氛围诊断","Atmosphere Doctor")}, {"tab.render",("渲染诊断","Render Doctor")}, {"tab.tuning",("调参(前后)","Tuning (Before/After)")},
                {"tab.evidence",("证据包","Evidence Pack")}, {"tab.delta",("增量学习库","Delta Library")}, {"tab.pipeline",("管线包","Pipeline Pack")},
                {"tab.batch",("批量采样","Batch Sampler")}, {"tab.ai",("AI 助手","AI Assist")}, {"tab.reports",("报告","Reports")}, {"tab.settings",("设置","Settings")},
                {"btn.scan",("扫描","Scan")}, {"btn.dryrun",("仅分析","Dry Run")}, {"btn.apply",("应用","Apply")}, {"btn.export",("导出报告","Export Report")},
                {"btn.batchrun",("开始批跑","Batch Run")}, {"btn.open",("打开","Open")}, {"btn.pickFolder",("选择文件夹","Pick Folder")}, {"btn.save",("保存","Save")},
                {"help.settings.llm",("LLM 需要 API Key；未配置时 AI 功能自动降级，核心扫描/证据功能不受影响。","LLM needs an API key; without it AI features gracefully degrade and core scan/evidence still works.")},
                {"help.ai.overview",("AI 仅生成草稿，请你确认后再写回；不会自动进行云端训练。","AI generates drafts only; review before accepting. No automatic cloud training is performed.")},
                {"help.evidence.overview",("Evidence Pack 用于保存前后对比截图与证据文件，便于复现与分享。","Evidence Pack stores before/after shots and evidence files for reproducibility and sharing.")},
                {"help.tuning.overview",("先 Capture BEFORE，再手动调参，再 Capture AFTER，最后提取 Delta Patch。","Capture BEFORE, tweak manually, capture AFTER, then extract Delta Patch.")},
                {"help.batch.overview",("批量采样会扫描白名单场景逐个运行；单场景失败不会中断整体任务。","Batch sampling runs scenes in whitelist one by one; single-scene failure does not stop the whole run.")},
                {"help.pairwise",("A/B 偏好比单次打分更稳定，用于学习你的风格选择。","Pairwise A/B preference is often more stable than a single score for style learning.")},
                {"ai.disabled",("LLM 未启用：请在设置中配置 Provider 与 API Key；当前可使用离线草稿兜底。","LLM disabled: configure provider and API key in Settings. Offline fallback templates remain available.")},
                {"ai.enabled",("LLM 已启用，可生成可审计的 AI 草稿。","LLM enabled. Auditable AI drafts can be generated.")},
                {"settings.language",("语言","Language")}, {"settings.promptLang",("提示词语言","Prompt Language")},
                {"settings.llmProvider",("LLM 提供商","LLM Provider")}, {"settings.llmBaseUrl",("服务地址","Base URL")}, {"settings.llmModel",("模型","Model")},
                {"settings.llmTimeout",("超时秒数","Timeout Seconds")}, {"settings.llmMaxTokens",("最大 Tokens","Max Tokens")}, {"settings.llmTemp",("温度","Temperature")},
                {"settings.apiKey",("API Key","API Key")}, {"settings.dupLabelId",("标签配置存在重复 ID：{0}","Duplicate id in label catalog: {0}")},
                {"reports.hint",("报告输出目录：Assets/_Tools/URPSceneDoctor/Reports/{SceneName}","Reports are generated under Assets/_Tools/URPSceneDoctor/Reports/{SceneName}")},
                {"quickverify.title",("快速验证完成","Quick Verify Complete")}, {"common.ok",("确定","OK")},
                {"atmos.overview",("基于工程证据生成氛围整改建议。","Generate atmosphere suggestions based on project evidence.")},
                {"atmos.tastePolicy",("风格策略","Taste Policy")}, {"atmos.createEvidence",("生成证据包","Create Evidence Pack")},
                {"render.overview",("轻量渲染性能风险建议。","Lightweight render performance advisory checks.")},
                {"tuning.captureBefore",("捕获 BEFORE","Capture BEFORE")}, {"tuning.captureAfter",("捕获 AFTER","Capture AFTER")}, {"tuning.extractDelta",("提取 Delta Patch","Extract Delta Patch")},
                {"tuning.beforePath",("BEFORE 路径","BEFORE Path")}, {"tuning.afterPath",("AFTER 路径","AFTER Path")}, {"tuning.patchPath",("Patch 路径","Patch Path")},
                {"tuning.openSnap",("打开快照目录","Open Snapshot Folder")}, {"tuning.openPatch",("打开补丁目录","Open Patch Folder")}, {"tuning.deltaSummary",("Delta 摘要","Delta Summary")}, {"tuning.policyAlignment",("策略对齐","Policy Alignment")},
                {"evidence.create",("生成证据包","Create Evidence Pack")}, {"evidence.cameraOverride",("手动相机覆盖","Manual Camera Override")}, {"evidence.autoCamera",("自动相机","Auto Camera")}, {"evidence.pickReason",("选择原因","Pick Reason")},
                {"batch.refreshScenes",("刷新场景","Refresh Scenes")}, {"batch.openRoot",("打开批处理目录","Open Batch Root")}, {"batch.filter",("筛选","Filter")}, {"batch.selectAll",("全选","Select All")}, {"batch.clear",("清空","Clear")},
                {"batch.lastSummary",("最近批处理摘要","Last Batch Summary")}, {"batch.summaryPath",("摘要路径","Summary Path")}, {"batch.sample",("样本","Sample")}, {"batch.noScene",("未选择场景。","No scene selected.")},
                {"batch.done",("完成：{0}","Done: {0}")}, {"batch.quickLabel",("快速标注","Quick Labeling")}, {"batch.aiDraft",("AI 草稿：{0}","AI Draft: {0}")},
                {"batch.acceptAi",("接受 AI 草稿","Accept AI Draft")}, {"batch.rejectAi",("拒绝 AI 草稿","Reject AI Draft")}, {"batch.styleGoal",("风格目标","Style Goal")}, {"batch.rating",("评分","Rating")}, {"batch.saveAnnotation",("保存标注","Save Annotation")},
                {"ai.sampleFolder",("样本/证据目录","Sample/Evidence Folder")}, {"ai.draftLabel",("草稿标注","Draft Labeling")}, {"ai.explainSummary",("可解释摘要","Explainable Summary")}, {"ai.ruleAssist",("规则草案助手","Rule Authoring Assist")},
                {"ai.pairwise",("成对偏好","Pairwise Preference")}, {"ai.styleA",("风格 A","Style A")}, {"ai.styleB",("风格 B","Style B")},
                {"ai.aBetter",("A 更好","A better")}, {"ai.bBetter",("B 更好","B better")}, {"ai.tie",("持平","Tie")}, {"ai.savePairwise",("保存成对偏好","Save Pairwise Preference")},
                {"ai.folderNotSet",("未设置目录","Folder not set")}, {"ai.folderNotFound",("目录不存在","Folder not found")}, {"ai.dialogTitle",("AI 助手","AI Assist")},
                {"pipeline.overview",("Base Pack v1：Distance Fog + Volumetric Light（可安全安装/卸载）。","Base Pack v1: Distance Fog + Volumetric Light (safe install/uninstall).")},
                {"pipeline.pack",("管线包","Pack")}, {"pipeline.preflight",("预检查","Preflight")}, {"pipeline.install",("安装","Install")}, {"pipeline.uninstall",("卸载","Uninstall")}, {"pipeline.evidence",("证据包","Evidence Pack")},
                {"delta.addLast",("添加最近调参结果","Add Last Tuning Result")}, {"delta.importFolder",("导入文件夹...","Import Folder...")}, {"delta.recompute",("重算统计","Recompute Stats")},
                {"delta.openSamples",("打开样本目录","Open Samples Folder")}, {"delta.openStats",("打开统计目录","Open Stats Folder")}, {"delta.topHints",("Top 5 提示","Top 5 Hints")},
                {"hub.lastWorkOrders",("最近工单","Last Work Orders")}, {"hub.applyOptions",("应用选项","Apply Options")}, {"hub.applyMode",("应用模式","Apply Mode")},
                {"hub.styleProfile",("风格配置","Style Profile")}, {"hub.bindPolicy",("绑定策略（默认关闭）","Assign new profile to existing Global Volume (default OFF)")},
                {"hub.refreshHeader",("刷新头信息","Refresh Header")}, {"hub.openDemo",("打开示例场景","Open Demo Scene")}, {"hub.quickVerify",("快速验证","Quick Verify")}, {"hub.reportExported",("报告已导出","Report exported")},
                {"quickverify.reportPath",("报告路径","Report Path")}, {"quickverify.matched",("命中规则","Matched Rules")}, {"quickverify.warnings",("警告数","Warnings")},
                {"lang.auto",("自动","Auto")}, {"lang.zh",("中文","中文")}, {"lang.en",("English","English")},
                {"common.none",("(无)","(none)")}, {"common.invalid",("(无效)","(invalid)")}, {"common.notEvaluated",("(未评估)","(not evaluated)")}, {"common.outputFmt",("输出：{0}","Output: {0}")},
                {"help.settings.overview",("设置页用于配置输出路径、语言、相机模式和 AI 连接。","Use Settings to configure output paths, language, camera mode, and AI connectivity.")},
                {"help.atmos.overview",("氛围诊断会基于快照证据生成整改建议。","Atmosphere Doctor generates remediation suggestions from snapshot evidence.")},
                {"help.render.overview",("渲染诊断提供轻量性能风险建议，不替代 Profiler。","Render Doctor gives lightweight risk hints and does not replace profiler.")},
                {"tuning.changedFmt",("变更字段数 = {0}","changed fields = {0}")},
                {"delta.noTuningTitle",("无调参结果","No Tuning Result")}, {"delta.noTuningMsg",("请先执行 BEFORE/AFTER 并提取 Delta Patch。","Capture BEFORE/AFTER and Extract Delta Patch first.")},
                {"delta.importFailed",("导入失败","Import Failed")}, {"delta.importInsideProject",("目录必须位于项目内。","Folder must be inside project.")}, {"delta.importMissingPatch",("目录中未找到 deltaPatch.json。","deltaPatch.json not found in folder.")},

                {"common.bullet",("- {0}","- {0}")},
                {"i18n.auditTitle",("I18N 审计","I18N Audit")}, {"i18n.auditZero",("硬编码 UI 文案：0","Hardcoded UI strings: 0")}, {"i18n.auditFound",("发现 {0} 个问题，详见 {1}","Found {0} issue(s). See {1}")},
            };
        }
    }
}
