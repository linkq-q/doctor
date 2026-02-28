using System.Collections.Generic;

namespace URPSceneDoctor.Editor
{
    public static class USD_AdviceLocalizer
    {
        private static readonly Dictionary<string, string> Terms = new Dictionary<string, string>
        {
            {"LUT", "查找表（LUT）"},
            {"HDR", "高动态范围（HDR）"},
            {"MSAA", "多重采样抗锯齿（MSAA）"},
            {"URP", "通用渲染管线（URP）"},
            {"Draw Call", "绘制调用（Draw Call）"},
            {"DC", "绘制调用（Draw Call）"},
            {"Fog", "雾效"},
            {"Volumetric Light", "体积光"}
        };

        public static string NormalizeSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var ret = text;
            foreach (var kv in Terms)
            {
                ret = ret.Replace(kv.Key, kv.Value);
            }

            if (!ret.Contains("建议："))
            {
                ret = "建议：" + ret;
            }

            if (!ret.Contains("；原因："))
            {
                ret += "；原因：基于当前快照证据触发。";
            }

            if (!ret.Contains("；如何验证："))
            {
                ret += "；如何验证：对比整改前后截图与指标变化。";
            }

            return ret;
        }

        public static void NormalizeWorkOrder(USD_WorkOrder wo)
        {
            if (wo == null) return;
            wo.title = NormalizeSentence(wo.title);
            wo.diagnosis = NormalizeSentence(wo.diagnosis);
            wo.costNotes = NormalizeSentence(wo.costNotes);
            for (var i = 0; i < wo.symptoms.Count; i++) wo.symptoms[i] = NormalizeSentence(wo.symptoms[i]);
            for (var i = 0; i < wo.evidence.Count; i++) wo.evidence[i] = NormalizeSentence(wo.evidence[i]);
            foreach (var p in wo.prescriptions)
            {
                p.actionText = NormalizeSentence(p.actionText);
                p.targetPathHint = NormalizeSentence(p.targetPathHint);
                p.recommendedRange = NormalizeSentence(p.recommendedRange);
                for (var i = 0; i < p.alternatives.Count; i++) p.alternatives[i] = NormalizeSentence(p.alternatives[i]);
            }

            for (var i = 0; i < wo.verification.steps.Count; i++) wo.verification.steps[i] = NormalizeSentence(wo.verification.steps[i]);
            wo.verification.performanceBudgetHint = NormalizeSentence(wo.verification.performanceBudgetHint);
        }
    }
}
