using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_AiPromptTemplates
    {
        [Serializable] private sealed class IssueWrap { public List<USD_IssueTag> issues; }

        public static string DraftLabelingSystem(string lang)
        {
            return lang == "en"
                ? "You are a TA assistant for URP scene atmosphere/perf remediation. Output strict JSON only. Choose IDs only from given style/issue ids. Base reasoning on metrics and delta."
                : "你是“URP 场景氛围与性能整改”的技术美术助手。你必须输出严格 JSON，不得输出任何多余文本。你只能从给定的 style/issue id 中选择。你必须基于输入的指标与 delta 给出可解释理由。";
        }

        public static string DraftLabelingUser(USD_LabelCatalogAsset catalog, string sceneName, USD_ScanSnapshot after, USD_DeltaPatch patch, USD_ImageMetricsFile mBefore, USD_ImageMetricsFile mAfter)
        {
            var sb = new StringBuilder();
            sb.AppendLine("输入数据（JSON）：");
            sb.AppendLine("label_catalog:");
            sb.AppendLine(JsonUtility.ToJson(catalog, true));
            sb.AppendLine("scene_context:");
            sb.AppendLine($"{{\"sceneName\":\"{sceneName}\",\"platformHint\":\"{after.platformHint}\",\"quality\":\"{after.activeQualityLevel}\"}}");
            sb.AppendLine("metrics_before:"); sb.AppendLine(JsonUtility.ToJson(mBefore.aggregate, true));
            sb.AppendLine("metrics_after:"); sb.AppendLine(JsonUtility.ToJson(mAfter.aggregate, true));
            sb.AppendLine("delta_patch:"); sb.AppendLine(JsonUtility.ToJson(patch, true));
            sb.AppendLine("snapshot_after:"); sb.AppendLine(JsonUtility.ToJson(after, true));
            sb.AppendLine("输出 JSON schema（必须完全符合）：");
            sb.AppendLine("{\"recommended_style_goal_id\":\"string\",\"recommended_score_1to10\":1,\"recommended_issue_tags_top3\":[\"id1\",\"id2\",\"id3\"],\"next_steps\":[\"...\",\"...\"],\"short_reason\":\"...\"}");
            sb.AppendLine("强约束：只能输出 JSON。style/issue 必须来自给定 id。short_reason 必须引用至少2个具体指标字段。");
            return sb.ToString();
        }

        public static USD_AiLabelDraft ParseLabelDraftOrFallback(string content, USD_LabelCatalogAsset catalog, USD_ImageMetricsFile after)
        {
            try
            {
                var json = ExtractJson(content);
                var parsed = JsonUtility.FromJson<USD_AiLabelDraft>(json);
                if (parsed == null) throw new Exception("null json parse");
                SanitizeDraft(parsed, catalog);
                return parsed;
            }
            catch
            {
                var fallback = new USD_AiLabelDraft
                {
                    recommended_style_goal_id = catalog.styles.Count > 0 ? catalog.styles[0].id : string.Empty,
                    recommended_score_1to10 = Mathf.Clamp(Mathf.RoundToInt((after.aggregate.center_contrast_ratio + 1f) * 3f), 1, 10),
                    short_reason = "Fallback draft: parse failed; please edit manually.",
                    error = "JSON parse failed"
                };
                fallback.recommended_issue_tags_top3 = catalog.issues.Take(3).Select(x => x.id).ToList();
                fallback.next_steps = new List<string> { "检查 overexposure_ratio", "检查 VolumeKey.Bloom.intensity", "手动确认风格标签" };
                return fallback;
            }
        }

        public static string ExplainSystem(string lang)
        {
            return lang == "en"
                ? "Write concise markdown summary with concrete metrics references."
                : "你是技术美术整改报告撰写助手。你将把结构化证据写成可交付的摘要，避免空话，必须引用具体指标或参数变化。输出为 Markdown。";
        }

        public static string ExplainUser(string reportJson, string diffJson, string deltaJson, string metricBefore, string metricAfter, string policyJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("输入：");
            sb.AppendLine("work_orders/report:"); sb.AppendLine(reportJson);
            sb.AppendLine("diff:"); sb.AppendLine(diffJson);
            sb.AppendLine("delta_patch:"); sb.AppendLine(deltaJson);
            sb.AppendLine("metrics_before:"); sb.AppendLine(metricBefore);
            sb.AppendLine("metrics_after:"); sb.AppendLine(metricAfter);
            sb.AppendLine("policy_check:"); sb.AppendLine(policyJson);
            sb.AppendLine("按以下标题输出 Markdown：摘要 / 关键改动（Top 3） / 风险点（Top 3） / 验收步骤（3 条）。");
            return sb.ToString();
        }

        public static string RuleSystem(string lang)
        {
            return lang == "en"
                ? "You are a rules engineer assistant. Output strict JSON only."
                : "你是规则工程师助手。你必须输出严格 JSON，不得输出多余文本。你生成的是规则草案，不可直接修改现有规则包。";
        }

        public static string RuleUser(List<string> selectedIssueTags, USD_LabelCatalogAsset catalog, string snapshotJson, string deltaJson, string metricsJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("输入：");
            sb.AppendLine("selected_issue_tags:"); sb.AppendLine(JsonUtility.ToJson(new StringListWrap { items = selectedIssueTags }, true));
            sb.AppendLine("label_catalog:"); sb.AppendLine(JsonUtility.ToJson(catalog, true));
            sb.AppendLine("available_fields: snapshot_fields=[transparentRendererCount,postProcessingEnabled],volume_keys=[CA.contrast,WB.temperature,Bloom.intensity],metrics_fields=[luma_mean,overexposure_ratio,center_contrast_ratio]");
            sb.AppendLine("snapshot_after:"); sb.AppendLine(snapshotJson);
            sb.AppendLine("delta_patch:"); sb.AppendLine(deltaJson);
            sb.AppendLine("metrics_after:"); sb.AppendLine(metricsJson);
            sb.AppendLine("输出 JSON schema：{\"proposed_rule_id\":\"string\",\"severity_suggestion\":\"P0|P1|P2\",\"trigger_candidates\":[{\"field\":\"string\",\"op\":\"<|<=|>|>=|==|!=\",\"value\":\"number|string\",\"note\":\"...\"}],\"recommendation_template\":[\"...\"],\"verification_steps\":[\"...\"]}");
            return sb.ToString();
        }

        public static USD_RuleDraft ParseRuleDraftOrFallback(string content, List<string> selectedIssues)
        {
            try
            {
                var json = ExtractJson(content);
                var parsed = JsonUtility.FromJson<USD_RuleDraft>(json);
                if (parsed == null) throw new Exception("null parse");
                return parsed;
            }
            catch
            {
                return new USD_RuleDraft
                {
                    proposed_rule_id = "DRAFT-" + (selectedIssues.Count > 0 ? selectedIssues[0] : "issue") + "-fallback",
                    severity_suggestion = "P1",
                    recommendation_template = new List<string> { "Bloom.intensity 建议 0.1~1.0", "overexposure_ratio 保持 < 0.08" },
                    verification_steps = new List<string> { "检查 overexposure_ratio", "对比 shot_01 before/after" },
                    error = "JSON parse failed"
                };
            }
        }

        [Serializable] private sealed class StringListWrap { public List<string> items; }

        private static void SanitizeDraft(USD_AiLabelDraft draft, USD_LabelCatalogAsset catalog)
        {
            var validStyles = new HashSet<string>(catalog.styles.Select(x => x.id));
            var validIssues = new HashSet<string>(catalog.issues.Select(x => x.id));
            if (!validStyles.Contains(draft.recommended_style_goal_id))
            {
                draft.recommended_style_goal_id = catalog.styles.Count > 0 ? catalog.styles[0].id : string.Empty;
            }

            draft.recommended_score_1to10 = Mathf.Clamp(draft.recommended_score_1to10, 1, 10);
            draft.recommended_issue_tags_top3 = (draft.recommended_issue_tags_top3 ?? new List<string>()).Where(validIssues.Contains).Distinct().Take(3).ToList();
            while (draft.recommended_issue_tags_top3.Count < Mathf.Min(3, catalog.issues.Count))
            {
                var candidate = catalog.issues[draft.recommended_issue_tags_top3.Count].id;
                if (!draft.recommended_issue_tags_top3.Contains(candidate)) draft.recommended_issue_tags_top3.Add(candidate);
            }
            if (draft.next_steps == null || draft.next_steps.Count == 0) draft.next_steps = new List<string> { "Review manually" };
        }

        private static string ExtractJson(string text)
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return text;
            return text.Substring(start, end - start + 1);
        }
    }
}
