using System;
using System.IO;
using UnityEngine;

namespace TA.Toolchain.RenderAudit
{
    public static class RenderAuditAISummarizer
    {
        public sealed class Settings
        {
            public string apiKey;
            public string endpoint = "https://api.deepseek.com/chat/completions";
            public string model = "deepseek-reasoner";
            public float temperature = 0.2f;
            public int timeoutSeconds = 20;
            public int maxTokens = 320;
            public int maxInputChars = 12000;
        }

        public static void Generate(string reportAbsolutePath, Settings settings, Action<string, string> onDone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reportAbsolutePath) || !File.Exists(reportAbsolutePath))
                {
                    onDone?.Invoke(RenderAuditLocalFallback.Build(string.Empty), "Fallback: report missing");
                    return;
                }

                var raw = File.ReadAllText(reportAbsolutePath);
                var slim = RenderAuditReportSlimmer.Slim(raw, Mathf.Max(2000, settings.maxInputChars));

                if (string.IsNullOrWhiteSpace(settings.apiKey))
                {
                    onDone?.Invoke(RenderAuditLocalFallback.Build(slim), "Fallback: empty key");
                    return;
                }

                var systemPrompt = "你是技术美术/渲染优化顾问+场景氛围导演。任务：从审计摘要里挑最大影响问题，极短输出；再给氛围增强建议。强约束：严格输出格式、极短、不要解释过程、不复述报告。";
                var userPrompt =
                    "请基于以下审计摘要输出中文结果，严格按格式：\n" +
                    "【改进建议（按影响排序）】\n" +
                    "P0 - ...\nP0 - ...\nP1 - ...\nP1 - ...\nP2 - ...\n\n" +
                    "【建议补充的氛围效果】\n" +
                    "P0 - ...\nP1 - ...\nP2 - ...\n\n" +
                    "要求：改进建议5条，每条一句=问题→影响→最小动作；氛围3条，每条一句=效果→提升→实现方式；总输出尽量≤250汉字；场景方向偏黄昏森林遗迹+河流。\n\n" +
                    "审计摘要：\n" + slim;

                var client = new DeepSeekClient();
                var options = new DeepSeekClient.RequestOptions
                {
                    endpoint = settings.endpoint,
                    model = settings.model,
                    temperature = settings.temperature,
                    timeoutSeconds = settings.timeoutSeconds,
                    maxTokens = settings.maxTokens
                };

                client.RequestSummary(settings.apiKey, systemPrompt, userPrompt, options, (text, error) =>
                {
                    if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(text) || !IsValidShape(text))
                    {
                        onDone?.Invoke(RenderAuditLocalFallback.Build(slim), $"Fallback: {error}");
                        return;
                    }

                    onDone?.Invoke(TrimToMaxChineseLength(text, 250), null);
                });
            }
            catch (Exception ex)
            {
                onDone?.Invoke(RenderAuditLocalFallback.Build(string.Empty), $"Fallback: {ex.Message}");
            }
        }

        public static bool TryGetLatestReportAbsolutePath(out string absolutePath, out string fileName, out long sizeBytes)
        {
            absolutePath = string.Empty;
            fileName = string.Empty;
            sizeBytes = 0;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var reportsRelative = "Assets/_Tools/TA_Toolchain/Reports";
            var reportsAbsolute = Path.Combine(projectRoot, reportsRelative).Replace('\\', '/');
            if (!Directory.Exists(reportsAbsolute))
            {
                return false;
            }

            var files = Directory.GetFiles(reportsAbsolute, "*.json", SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                return false;
            }

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            absolutePath = files[0];
            var info = new FileInfo(absolutePath);
            fileName = info.Name;
            sizeBytes = info.Length;
            return true;
        }
        private static bool IsValidShape(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var hasA = text.Contains("【改进建议（按影响排序）】");
            var hasB = text.Contains("【建议补充的氛围效果】");
            var pCount = 0;
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var t = line.TrimStart();
                if (t.StartsWith("P0 -") || t.StartsWith("P1 -") || t.StartsWith("P2 -")) pCount++;
            }
            return hasA && hasB && pCount == 8;
        }

        private static string TrimToMaxChineseLength(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var trimmed = text.Trim();
            return trimmed.Length <= maxChars ? trimmed : trimmed.Substring(0, maxChars);
        }

    }
}
