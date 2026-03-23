using System;

namespace URPSceneDoctor.Editor
{
    public enum USD_LlmCallState
    {
        Idle,
        Sending,
        Waiting,
        Success,
        Fail
    }

    [Serializable]
    public sealed class USD_LlmCallStatus
    {
        public USD_LlmCallState state = USD_LlmCallState.Idle;
        public string lastMessage = string.Empty;
        public string lastError = string.Empty;
        public string lastRequestAt = string.Empty;
        public string lastResponseAt = string.Empty;

        public void MarkSending(string message)
        {
            state = USD_LlmCallState.Sending;
            lastMessage = message;
            lastError = string.Empty;
            lastRequestAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public void MarkWaiting(string message)
        {
            state = USD_LlmCallState.Waiting;
            lastMessage = message;
        }

        public void MarkSuccess(string message)
        {
            state = USD_LlmCallState.Success;
            lastMessage = message;
            lastError = string.Empty;
            lastResponseAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public void MarkFail(string error)
        {
            state = USD_LlmCallState.Fail;
            lastError = error;
            lastMessage = "AI 调用失败";
            lastResponseAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
