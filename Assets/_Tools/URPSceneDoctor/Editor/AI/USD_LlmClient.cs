using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    internal sealed class USD_LlmMessage { public string role; public string content; }
    [Serializable]
    internal sealed class USD_LlmRequest
    {
        public string model;
        public List<USD_LlmMessage> messages = new List<USD_LlmMessage>();
        public float temperature;
        public int max_tokens;
    }

    [Serializable] internal sealed class USD_LlmResponseChoiceMessage { public string content; }
    [Serializable] internal sealed class USD_LlmResponseChoice { public USD_LlmResponseChoiceMessage message; }
    [Serializable] internal sealed class USD_LlmResponse { public List<USD_LlmResponseChoice> choices; }

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
    }

    public static class USD_LlmClient
    {
        private const string ApiKeyEditorPref = "USD_LLM_API_KEY";

        public static string GetApiKey() => EditorPrefs.GetString(ApiKeyEditorPref, string.Empty);
        public static void SetApiKey(string key) => EditorPrefs.SetString(ApiKeyEditorPref, key ?? string.Empty);

        public static bool IsEnabled(USD_Settings settings)
        {
            if (settings == null || settings.llmProvider == "Off") return false;
            return !string.IsNullOrWhiteSpace(GetApiKey());
        }

        public static USD_LlmResult Chat(USD_Settings settings, string systemPrompt, string userPrompt)
        {
            if (!IsEnabled(settings))
            {
                return new USD_LlmResult { success = false, error = "LLM disabled or API key missing." };
            }

            try
            {
                var req = new USD_LlmRequest
                {
                    model = string.IsNullOrWhiteSpace(settings.llmModel) ? "deepseek-chat" : settings.llmModel,
                    temperature = settings.llmTemperature,
                    max_tokens = Mathf.Max(128, settings.llmMaxTokens)
                };
                req.messages.Add(new USD_LlmMessage { role = "system", content = systemPrompt });
                req.messages.Add(new USD_LlmMessage { role = "user", content = userPrompt });

                var json = JsonUtility.ToJson(req);
                var baseUrl = (settings.llmBaseUrl ?? "https://api.deepseek.com").TrimEnd('/');
                var url = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : baseUrl + "/chat/completions";
                var requestTimeoutSec = Mathf.Max(3, settings.llmTimeoutSec);
                var singleCallTimeoutSec = Mathf.Max(requestTimeoutSec, settings.singleCallTimeoutSec);

                if (settings.verboseLogs)
                {
                    UnityEngine.Debug.Log($"[USD][LLM] Sending request: provider={settings.llmProvider}, model={req.model}, url={url}, timeout={requestTimeoutSec}s, singleCallTimeout={singleCallTimeoutSec}s, payloadBytes={Encoding.UTF8.GetByteCount(json)}");
                }

                using (var uwr = new UnityWebRequest(url, "POST"))
                {
                    var bodyRaw = Encoding.UTF8.GetBytes(json);
                    uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    uwr.SetRequestHeader("Content-Type", "application/json");
                    uwr.SetRequestHeader("Authorization", "Bearer " + GetApiKey());
                    uwr.timeout = requestTimeoutSec;

                    var watch = Stopwatch.StartNew();
                    var op = uwr.SendWebRequest();
                    var completion = new ManualResetEventSlim(false);
                    try
                    {
                        op.completed += _ => completion.Set();
                        var completedInTime = completion.Wait(TimeSpan.FromSeconds(singleCallTimeoutSec));
                        watch.Stop();

                        if (!completedInTime)
                        {
                            uwr.Abort();
                            return new USD_LlmResult
                            {
                                success = false,
                                error = $"SingleCallTimeout after {singleCallTimeoutSec}s",
                                timedOut = true,
                                latencyMs = watch.ElapsedMilliseconds
                            };
                        }
                    }
                    finally
                    {
                        completion.Dispose();
                    }


                    var raw = uwr.downloadHandler != null ? uwr.downloadHandler.text : string.Empty;
                    var statusCode = uwr.responseCode;
                    if (settings.verboseLogs)
                    {
                        UnityEngine.Debug.Log($"[USD][LLM] Response: status={statusCode}, result={uwr.result}, latencyMs={watch.ElapsedMilliseconds}, bytes={(raw ?? string.Empty).Length}");
                    }
                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        return new USD_LlmResult
                        {
                            success = false,
                            error = $"HTTP {(int)statusCode}: {uwr.error}",
                            raw_json = raw,
                            statusCode = statusCode,
                            latencyMs = watch.ElapsedMilliseconds,
                            responseSize = (raw ?? string.Empty).Length
                        };
                    }

                    var parsed = JsonUtility.FromJson<USD_LlmResponse>(raw);
                    var text = parsed != null && parsed.choices != null && parsed.choices.Count > 0 && parsed.choices[0].message != null
                        ? parsed.choices[0].message.content
                        : string.Empty;
                    return new USD_LlmResult
                    {
                        success = !string.IsNullOrWhiteSpace(text),
                        text = text,
                        raw_json = raw,
                        error = string.IsNullOrWhiteSpace(text) ? "Empty response content." : string.Empty,
                        statusCode = statusCode,
                        latencyMs = watch.ElapsedMilliseconds,
                        responseSize = (raw ?? string.Empty).Length
                    };
                }
            }
            catch (Exception e)
            {
                return new USD_LlmResult { success = false, error = e.Message };
            }
        }
    }
}
