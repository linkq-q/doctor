using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace URPSceneDoctor.Editor
{
    [Serializable] internal sealed class Message { public string role; public string content; }
    [Serializable] internal sealed class Choice { public Message message; public string reasoning_content; public string finish_reason; }
    [Serializable] internal sealed class DSResponse { public Choice[] choices; }
    [Serializable]
    internal sealed class DSRequest
    {
        public string model;
        public List<Message> messages = new List<Message>();
        public float temperature;
        public int max_tokens;
        public bool stream;
    }

    public sealed class USD_LlmResult
    {
        public bool success;
        public string text;
        public string raw_json;
        public string error;
        public long statusCode;
        public long latencyMs;
        public bool timedOut;
        public int responseSize;
        public string url;
        public string unityResult;
        public string bodySnippet;
    }

    public static class USD_LlmClient
    {
        private const string ApiKeyEditorPref = "USD_LLM_API_KEY";
        private const string EndpointNoV1 = "https://api.deepseek.com/chat/completions";
        private const string EndpointV1 = "https://api.deepseek.com/v1/chat/completions";

        public static string GetApiKey() => EditorPrefs.GetString(ApiKeyEditorPref, string.Empty);
        public static void SetApiKey(string key) => EditorPrefs.SetString(ApiKeyEditorPref, key ?? string.Empty);

        public static bool IsEnabled(USD_Settings settings)
        {
            if (settings == null || settings.llmProvider == "Off") return false;
            return !string.IsNullOrWhiteSpace(GetApiKey());
        }

        public static void ChatOnce(USD_Settings settings, List<(string role, string content)> messages, Action<string> onOk, Action<string> onFail)
        {
            if (!IsEnabled(settings))
            {
                onFail?.Invoke("LLM disabled or API key missing.");
                return;
            }

            var request = new DSRequest
            {
                model = string.IsNullOrWhiteSpace(settings.llmModel) ? "deepseek-chat" : settings.llmModel,
                temperature = settings.llmTemperature,
                max_tokens = Mathf.Max(128, settings.llmMaxTokens),
                stream = false
            };

            if (messages != null)
            {
                foreach (var message in messages)
                {
                    request.messages.Add(new Message { role = message.role, content = message.content });
                }
            }

            var mode = (settings.llmEndpointMode ?? "Auto").Trim();
            if (mode != "NoV1" && mode != "V1" && mode != "Auto") mode = "Auto";

            if (mode == "NoV1")
            {
                USD_EditorCoroutineRunner.StartCoroutineOwnerless(ChatRequestCoroutine(settings, request, EndpointNoV1, onOk, onFail, false));
                return;
            }

            if (mode == "V1")
            {
                USD_EditorCoroutineRunner.StartCoroutineOwnerless(ChatRequestCoroutine(settings, request, EndpointV1, onOk, onFail, false));
                return;
            }

            // Auto fallback: NoV1 -> V1 when network/timeout/404
            USD_EditorCoroutineRunner.StartCoroutineOwnerless(ChatRequestCoroutine(settings, request, EndpointNoV1, onOk, firstError =>
            {
                if (!ShouldFallback(firstError))
                {
                    onFail?.Invoke(firstError);
                    return;
                }

                USD_EditorCoroutineRunner.StartCoroutineOwnerless(ChatRequestCoroutine(settings, request, EndpointV1, onOk, secondError => onFail?.Invoke(secondError), true));
            }, false));
        }

        // Compatibility API expected by legacy callsites.
        public static USD_LlmResult Chat(USD_Settings settings, string systemPrompt, string userPrompt)
        {
            if (!IsEnabled(settings))
            {
                return new USD_LlmResult { success = false, error = "LLM disabled or API key missing." };
            }

            var request = new DSRequest
            {
                model = string.IsNullOrWhiteSpace(settings.llmModel) ? "deepseek-chat" : settings.llmModel,
                temperature = settings.llmTemperature,
                max_tokens = Mathf.Max(128, settings.llmMaxTokens),
                stream = false,
                messages = new List<Message>
                {
                    new Message{ role = "system", content = systemPrompt },
                    new Message{ role = "user", content = userPrompt }
                }
            };

            return ExecuteSync(settings, request, ResolvePrimaryEndpoint(settings));
        }

        public static IEnumerator ChatOnceCoroutine(USD_Settings settings, string systemPrompt, string userPrompt, Action<USD_LlmResult> onComplete)
        {
            if (!IsEnabled(settings))
            {
                onComplete?.Invoke(new USD_LlmResult { success = false, error = "LLM disabled or API key missing." });
                yield break;
            }

            var request = new DSRequest
            {
                model = string.IsNullOrWhiteSpace(settings.llmModel) ? "deepseek-chat" : settings.llmModel,
                temperature = settings.llmTemperature,
                max_tokens = Mathf.Max(128, settings.llmMaxTokens),
                stream = false,
                messages = new List<Message>
                {
                    new Message{ role = "system", content = systemPrompt },
                    new Message{ role = "user", content = userPrompt }
                }
            };

            USD_LlmResult completed = null;
            yield return ChatRequestCoroutine(settings, request, ResolvePrimaryEndpoint(settings), _ => { }, fail => { }, false, r => completed = r);
            onComplete?.Invoke(completed ?? new USD_LlmResult { success = false, error = "Unknown LLM error" });
        }

        private static string ResolvePrimaryEndpoint(USD_Settings settings)
        {
            var mode = (settings.llmEndpointMode ?? "Auto").Trim();
            if (mode == "NoV1") return EndpointNoV1;
            if (mode == "V1") return EndpointV1;
            return EndpointNoV1;
        }

        private static bool ShouldFallback(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            return error.Contains("ConnectionError") || error.Contains("SingleCallTimeout") || error.Contains("responseCode=404") || error.Contains("HTTP 404");
        }

        private static IEnumerator ChatRequestCoroutine(USD_Settings settings, DSRequest request, string endpoint, Action<string> onOk, Action<string> onFail, bool isFallback, Action<USD_LlmResult> onCompleted = null)
        {
            var json = JsonUtility.ToJson(request);
            var requestTimeoutSec = Mathf.Max(3, settings.llmTimeoutSec);
            var singleCallTimeoutSec = Mathf.Max(requestTimeoutSec, settings.singleCallTimeoutSec);
            var startedAt = EditorApplication.timeSinceStartup;

            using (var req = new UnityWebRequest(endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = requestTimeoutSec;
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + GetApiKey());

                var op = req.SendWebRequest();
                var timeoutTriggered = false;
                while (!op.isDone)
                {
                    if (EditorApplication.timeSinceStartup - startedAt >= singleCallTimeoutSec)
                    {
                        timeoutTriggered = true;
                        req.Abort();
                        break;
                    }

                    yield return null;
                }

                var elapsedSec = (float)(EditorApplication.timeSinceStartup - startedAt);
                var responseBody = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                var bodyPrefix = string.IsNullOrEmpty(responseBody) ? string.Empty : responseBody.Substring(0, Mathf.Min(200, responseBody.Length));
                var responseCode = req.responseCode;
                var result = req.result.ToString();

                var resultObj = new USD_LlmResult
                {
                    success = false,
                    raw_json = responseBody,
                    statusCode = responseCode,
                    latencyMs = (long)(elapsedSec * 1000f),
                    responseSize = responseBody.Length,
                    url = endpoint,
                    unityResult = result,
                    bodySnippet = bodyPrefix
                };

                if (settings.verboseLogs)
                {
                    Debug.Log($"[USD][LLM] endpoint={(isFallback ? "fallback" : "primary")} {endpoint} | result={result} | error={req.error} | responseCode={responseCode} | elapsedSec={elapsedSec:0.###} | bodyPrefix={bodyPrefix}");
                }

                if (timeoutTriggered)
                {
                    resultObj.timedOut = true;
                    resultObj.error = $"SingleCallTimeout after {singleCallTimeoutSec}s; endpoint={endpoint}; result={result}; responseCode={responseCode}; bodyPrefix={bodyPrefix}";
                    onCompleted?.Invoke(resultObj);
                    onFail?.Invoke(resultObj.error);
                    yield break;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    resultObj.error = $"HTTP {(int)responseCode}: {req.error}; endpoint={endpoint}; result={result}; responseCode={responseCode}; bodyPrefix={bodyPrefix}";
                    onCompleted?.Invoke(resultObj);
                    onFail?.Invoke(resultObj.error);
                    yield break;
                }

                var parsed = JsonUtility.FromJson<DSResponse>(responseBody);
                var text = parsed != null && parsed.choices != null && parsed.choices.Length > 0
                    ? parsed.choices[0]?.message?.content
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    resultObj.error = $"Empty response content; endpoint={endpoint}; result={result}; responseCode={responseCode}; bodyPrefix={bodyPrefix}";
                    onCompleted?.Invoke(resultObj);
                    onFail?.Invoke(resultObj.error);
                    yield break;
                }

                resultObj.success = true;
                resultObj.text = text;
                onCompleted?.Invoke(resultObj);
                onOk?.Invoke(text);
            }
        }

        private static USD_LlmResult ExecuteSync(USD_Settings settings, DSRequest request, string endpoint)
        {
            var json = JsonUtility.ToJson(request);
            var requestTimeoutSec = Mathf.Max(3, settings.llmTimeoutSec);
            var singleCallTimeoutSec = Mathf.Max(requestTimeoutSec, settings.singleCallTimeoutSec);
            var startedAt = EditorApplication.timeSinceStartup;
            using (var req = new UnityWebRequest(endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = requestTimeoutSec;
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + GetApiKey());

                var op = req.SendWebRequest();
                var timeoutTriggered = false;
                while (!op.isDone)
                {
                    if (EditorApplication.timeSinceStartup - startedAt >= singleCallTimeoutSec)
                    {
                        timeoutTriggered = true;
                        req.Abort();
                        break;
                    }
                }

                var elapsedSec = (float)(EditorApplication.timeSinceStartup - startedAt);
                var responseBody = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                var bodyPrefix = string.IsNullOrEmpty(responseBody) ? string.Empty : responseBody.Substring(0, Mathf.Min(200, responseBody.Length));
                var responseCode = req.responseCode;
                var result = req.result.ToString();

                if (timeoutTriggered)
                {
                    return new USD_LlmResult { success = false, timedOut = true, error = $"SingleCallTimeout after {singleCallTimeoutSec}s; endpoint={endpoint}; result={result}; responseCode={responseCode}; bodyPrefix={bodyPrefix}", raw_json = responseBody, statusCode = responseCode, latencyMs = (long)(elapsedSec * 1000f), url = endpoint, unityResult = result, bodySnippet = bodyPrefix };
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    return new USD_LlmResult { success = false, error = $"HTTP {(int)responseCode}: {req.error}; endpoint={endpoint}; result={result}; responseCode={responseCode}; bodyPrefix={bodyPrefix}", raw_json = responseBody, statusCode = responseCode, latencyMs = (long)(elapsedSec * 1000f), url = endpoint, unityResult = result, bodySnippet = bodyPrefix };
                }

                var parsed = JsonUtility.FromJson<DSResponse>(responseBody);
                var text = parsed != null && parsed.choices != null && parsed.choices.Length > 0
                    ? parsed.choices[0]?.message?.content
                    : string.Empty;
                return new USD_LlmResult { success = !string.IsNullOrWhiteSpace(text), text = text, raw_json = responseBody, statusCode = responseCode, latencyMs = (long)(elapsedSec * 1000f), url = endpoint, unityResult = result, bodySnippet = bodyPrefix, error = string.IsNullOrWhiteSpace(text) ? "Empty response content." : string.Empty };
            }
        }
    }
}
