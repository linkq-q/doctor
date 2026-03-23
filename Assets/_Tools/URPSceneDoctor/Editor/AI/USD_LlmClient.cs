using System;
using System.Collections.Generic;
using System.Text;
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

                using (var uwr = new UnityWebRequest(url, "POST"))
                {
                    var bodyRaw = Encoding.UTF8.GetBytes(json);
                    uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    uwr.SetRequestHeader("Content-Type", "application/json");
                    uwr.SetRequestHeader("Authorization", "Bearer " + GetApiKey());
                    uwr.timeout = Mathf.Max(3, settings.llmTimeoutSec);

                    var op = uwr.SendWebRequest();
                    while (!op.isDone) { }

                    var raw = uwr.downloadHandler != null ? uwr.downloadHandler.text : string.Empty;
                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        return new USD_LlmResult { success = false, error = uwr.error, raw_json = raw };
                    }

                    var parsed = JsonUtility.FromJson<USD_LlmResponse>(raw);
                    var text = parsed != null && parsed.choices != null && parsed.choices.Count > 0 && parsed.choices[0].message != null
                        ? parsed.choices[0].message.content
                        : string.Empty;
                    return new USD_LlmResult { success = !string.IsNullOrWhiteSpace(text), text = text, raw_json = raw, error = string.IsNullOrWhiteSpace(text) ? "Empty response content." : string.Empty };
                }
            }
            catch (Exception e)
            {
                return new USD_LlmResult { success = false, error = e.Message };
            }
        }
    }
}
