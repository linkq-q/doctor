# USD AI 调用链 CloudResponder 化改造记录

## 改动范围
- `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_LlmClient.cs`
- `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_AiAssistModule.cs`
- `Assets/_Tools/URPSceneDoctor/Editor/USD_Settings.cs`
- `Assets/_Tools/URPSceneDoctor/Editor/Hub/USD_HubWindow.cs`
- `Assets/_Tools/URPSceneDoctor/Editor/I18N/USD_Loc.cs`

## 关键实现
1. **Endpoint Mode 配置**
   - 新增 `llmEndpointMode`：`NoV1 / V1 / Auto`。
   - `Auto` 策略：先请求 `https://api.deepseek.com/chat/completions`，若网络类失败/超时/404，再回退到 `https://api.deepseek.com/v1/chat/completions`。

2. **CloudResponder 风格 DTO**
   - 按请求要求在 `USD_LlmClient` 中使用：`DSRequest / Message / DSResponse / Choice`。
   - 序列化方式使用 `JsonUtility.ToJson(reqObj)`。

3. **Editor 非阻塞调用 API**
   - 新增：
     `ChatOnce(USD_Settings settings, List<(string role, string content)> messages, Action<string> onOk, Action<string> onFail)`
   - 内部通过 Editor 侧协程 runner 发起 `UnityWebRequest`，逐帧 `yield return null`，不阻塞 UI。

4. **双超时保险**
   - `req.timeout = llmTimeoutSec`
   - 业务兜底 `singleCallTimeoutSec`
   - 业务超时触发 `req.Abort()` 并 Fail 返回。

5. **可信诊断日志**
   - 统一带出：`result/error/responseCode/elapsedSec/endpoint/bodyPrefix(200)`。
   - 仅在 `req.result == Success` 时按成功路径处理，不再出现“ConnectionError 但写 HTTP 200”的混淆输出。

6. **调用侧改造（AiAssist）**
   - `GenerateCurrentDraft`、`RunExplainableSummary`、`RunRuleDraft` 改为 `ChatOnce` 回调式流程。
   - 避免 `.Result/.Wait/GetAwaiter().GetResult()`，并在回调中释放 `_busy`。

## 验证建议
1. `EndpointMode=NoV1`：应可正常返回。
2. `EndpointMode=V1`：应可正常返回（若该路径可达）。
3. `EndpointMode=Auto`：NoV1 失败时应自动切换 V1，并输出最终 endpoint。
4. `llmTimeoutSec=5`：应在约 5 秒失败退出，不阻塞 Editor。
