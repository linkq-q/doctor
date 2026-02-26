using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TA.Toolchain.RenderAudit
{
    public sealed class DeepSeekClient
    {
        [Serializable]
        private sealed class ChatRequest
        {
            public string model;
            public Message[] messages;
            public float temperature;
            public int max_tokens;
            public bool stream;
        }

        [Serializable]
        private sealed class Message
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class ChatResponse
        {
            public Choice[] choices;
            public ErrorInfo error;
        }

        [Serializable]
        private sealed class Choice
        {
            public Message message;
        }

        [Serializable]
        private sealed class ErrorInfo
        {
            public string message;
        }

        public sealed class RequestOptions
        {
            public string endpoint = "https://api.deepseek.com/chat/completions";
            public string model = "deepseek-reasoner";
            public float temperature = 0.2f;
            public int timeoutSeconds = 20;
            public int maxTokens = 320;
        }

        public void RequestSummary(string apiKey, string systemPrompt, string userPrompt, RequestOptions options, Action<string, string> onDone)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onDone?.Invoke(null, "API Key is empty.");
                return;
            }

            var payload = new ChatRequest
            {
                model = options.model,
                temperature = options.temperature,
                max_tokens = options.maxTokens,
                stream = false,
                messages = new[]
                {
                    new Message { role = "system", content = systemPrompt },
                    new Message { role = "user", content = userPrompt }
                }
            };

            var json = JsonUtility.ToJson(payload);
            var request = new UnityWebRequest(options.endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                timeout = Mathf.Max(1, options.timeoutSeconds)
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var operation = request.SendWebRequest();
            void Tick()
            {
                if (!operation.isDone)
                {
                    return;
                }

                EditorApplication.update -= Tick;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onDone?.Invoke(null, request.error);
                    request.Dispose();
                    return;
                }

                var text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                ChatResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<ChatResponse>(text);
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(null, $"Parse failed: {ex.Message}");
                    request.Dispose();
                    return;
                }

                var content = response?.choices != null && response.choices.Length > 0 ? response.choices[0]?.message?.content : null;
                if (string.IsNullOrWhiteSpace(content))
                {
                    var err = response?.error?.message;
                    onDone?.Invoke(null, string.IsNullOrWhiteSpace(err) ? "No content in response." : err);
                }
                else
                {
                    onDone?.Invoke(content.Trim(), null);
                }

                request.Dispose();
            }

            EditorApplication.update += Tick;
        }
    }
}
