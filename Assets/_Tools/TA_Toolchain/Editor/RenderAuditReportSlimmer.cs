using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TA.Toolchain.RenderAudit
{
    public static class RenderAuditReportSlimmer
    {
        private static readonly Regex StringFieldRegex = new Regex("\"(?<k>[^\"]+)\"\\s*:\\s*\"(?<v>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled);
        private static readonly Regex NumberFieldRegex = new Regex("\"(?<k>[^\"]+)\"\\s*:\\s*(?<v>-?\\d+(?:\\.\\d+)?)", RegexOptions.Compiled);

        public static string Slim(string rawJson, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return "{}";
            }

            var issueLimit = 20;
            var offenderLimit = 10;
            var level = 0;
            while (level < 3)
            {
                var slim = Build(rawJson, issueLimit, offenderLimit, level >= 2);
                if (slim.Length <= maxChars)
                {
                    return slim;
                }

                if (level == 0)
                {
                    issueLimit = 12;
                    offenderLimit = 6;
                }
                else
                {
                    issueLimit = 8;
                    offenderLimit = 4;
                }

                level++;
            }

            var minimal = Build(rawJson, 6, 2, true);
            return minimal.Length <= maxChars ? minimal : minimal.Substring(0, Mathf.Max(64, maxChars));
        }

        private static string Build(string rawJson, int issueLimit, int offenderLimit, bool summaryOnly)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{");

            AppendObjectByKey(rawJson, "meta", sb, "metadata");
            sb.Append(",");
            AppendObjectByKey(rawJson, "summary", sb, "summary");

            if (!summaryOnly)
            {
                sb.Append(",\"issues\":[");
                var issues = ExtractIssues(rawJson, issueLimit);
                for (var i = 0; i < issues.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(issues[i]);
                }
                sb.Append("]");

                sb.Append(",\"topOffenders\":{");
                AppendOffenders(rawJson, sb, "textures", offenderLimit);
                sb.Append(",");
                AppendOffenders(rawJson, sb, "meshes", offenderLimit);
                sb.Append(",");
                AppendOffenders(rawJson, sb, "renderers", offenderLimit);
                sb.Append(",");
                AppendOffenders(rawJson, sb, "lights", offenderLimit);
                sb.Append("}");

                var recArray = ExtractArray(rawJson, "recommendations");
                if (!string.IsNullOrEmpty(recArray))
                {
                    sb.Append(",\"recommendations\":");
                    sb.Append(recArray);
                }
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendObjectByKey(string json, string key, StringBuilder sb, string alias)
        {
            var block = ExtractObject(json, key);
            sb.Append("\"").Append(alias).Append("\":");
            sb.Append(string.IsNullOrEmpty(block) ? "{}" : block);
        }

        private static List<string> ExtractIssues(string json, int take)
        {
            var list = new List<IssueLite>();
            var array = ExtractArray(json, "issues");
            if (string.IsNullOrEmpty(array))
            {
                return new List<string>();
            }

            var objects = SplitObjects(array);
            foreach (var obj in objects)
            {
                var severity = GetString(obj, "severity");
                var item = new IssueLite
                {
                    severity = severity,
                    category = GetString(obj, "category"),
                    title = GetString(obj, "title"),
                    detail = GetString(obj, "detail"),
                    targetPath = GetString(obj, "targetPath"),
                    score = SeverityScore(severity)
                };
                list.Add(item);
            }

            list.Sort((a, b) => b.score.CompareTo(a.score));
            var output = new List<string>();
            for (var i = 0; i < Math.Min(take, list.Count); i++)
            {
                var it = list[i];
                output.Add($"{{\"severity\":\"{Escape(it.severity)}\",\"category\":\"{Escape(it.category)}\",\"title\":\"{Escape(Trim(it.title, 48))}\",\"brief\":\"{Escape(Trim(it.detail, 64))}\",\"target\":\"{Escape(Trim(it.targetPath, 40))}\"}}");
            }

            return output;
        }

        private static void AppendOffenders(string json, StringBuilder sb, string name, int take)
        {
            sb.Append("\"").Append(name).Append("\":[");
            var top = ExtractTop(json, name, take);
            for (var i = 0; i < top.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(top[i]);
            }
            sb.Append("]");
        }

        private static List<string> ExtractTop(string json, string key, int take)
        {
            var result = new List<string>();
            var offenders = ExtractObject(json, "topOffenders");
            if (string.IsNullOrEmpty(offenders))
            {
                return result;
            }

            var array = ExtractArray(offenders, key);
            if (string.IsNullOrEmpty(array))
            {
                return result;
            }

            var objects = SplitObjects(array);
            for (var i = 0; i < Math.Min(take, objects.Count); i++)
            {
                var obj = objects[i];
                var nameValue = GetString(obj, "name");
                if (string.IsNullOrEmpty(nameValue)) nameValue = GetString(obj, "assetPath");
                var tri = GetNumber(obj, "triangles");
                var vert = GetNumber(obj, "vertices");
                var mem = GetNumber(obj, "estimatedBytes");
                var mat = GetNumber(obj, "materialCount");
                var intensity = GetNumber(obj, "intensity");
                result.Add($"{{\"name\":\"{Escape(Trim(nameValue, 48))}\",\"value\":\"{tri}{(vert > 0 ? "/" + vert : string.Empty)}{(mat > 0 ? "/mat:" + mat : string.Empty)}{(intensity > 0 ? "/i:" + intensity : string.Empty)}\",\"estimated\":\"{mem}\"}}");
            }

            return result;
        }

        private static string ExtractObject(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            var brace = json.IndexOf('{', idx);
            return brace >= 0 ? ReadEnclosed(json, brace, '{', '}') : null;
        }

        private static string ExtractArray(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            var bracket = json.IndexOf('[', idx);
            return bracket >= 0 ? ReadEnclosed(json, bracket, '[', ']') : null;
        }

        private static string ReadEnclosed(string text, int start, char open, char close)
        {
            var depth = 0;
            var inString = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
                if (inString) continue;
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }
            return null;
        }

        private static List<string> SplitObjects(string arrayJson)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(arrayJson) || arrayJson.Length < 2) return result;

            var inString = false;
            var depth = 0;
            var start = -1;
            for (var i = 0; i < arrayJson.Length; i++)
            {
                var c = arrayJson[i];
                if (c == '"' && (i == 0 || arrayJson[i - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        result.Add(arrayJson.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return result;
        }

        private static string GetString(string json, string key)
        {
            var rx = new Regex($"\\\"{Regex.Escape(key)}\\\"\\s*:\\s*\\\"(?<v>(?:\\\\.|[^\\\"])*)\\\"");
            var m = rx.Match(json);
            return m.Success ? Unescape(m.Groups["v"].Value) : string.Empty;
        }

        private static string GetNumber(string json, string key)
        {
            var rx = new Regex($"\\\"{Regex.Escape(key)}\\\"\\s*:\\s*(?<v>-?\\d+(?:\\.\\d+)?)");
            var m = rx.Match(json);
            return m.Success ? m.Groups["v"].Value : string.Empty;
        }

        private static int SeverityScore(string severity)
        {
            if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)) return 2;
            return 1;
        }

        private static string Trim(string text, int limit)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= limit ? text : text.Substring(0, limit);
        }

        private static string Escape(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        private static string Unescape(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\\\"", "\"").Replace("\\n", " ").Replace("\\r", " ").Replace("\\\\", "\\");
        }

        private sealed class IssueLite
        {
            public string severity;
            public string category;
            public string title;
            public string detail;
            public string targetPath;
            public int score;
        }
    }
}
