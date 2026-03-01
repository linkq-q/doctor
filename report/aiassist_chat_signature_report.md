# USD_AiAssistModule Chat 调用签名定位报告

## 调用点与期望签名
根据 `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_AiAssistModule.cs` 的 3 处调用：

1. `GenerateDraft(...)` 内（约 L152）
   - 调用：`var res = USD_LlmClient.Chat(settings, systemPrompt, userPrompt);`
   - 参数：`(USD_Settings, string, string)`
   - 返回值期望：`USD_LlmResult`（后续读取 `res.text / res.raw_json / res.success / res.error`）

2. `RunExplainableSummary(...)` 内（约 L495）
   - 调用：`var res = USD_LlmClient.Chat(settings, systemPrompt, userPrompt);`
   - 参数：`(USD_Settings, string, string)`
   - 返回值期望：`USD_LlmResult`（后续读取 `res.text / res.raw_json / res.success / res.error`）

3. `RunRuleDraft(...)` 内（约 L533）
   - 调用：`var res = USD_LlmClient.Chat(settings, systemPrompt, userPrompt);`
   - 参数：`(USD_Settings, string, string)`
   - 返回值期望：`USD_LlmResult`（后续读取 `res.text / res.raw_json / res.success / res.error`）

## 根因分类
- 分类：**B + A 混合**
  - `USD_LlmClient` 中存在新接口 `ChatOnceCoroutine(...)`，但原 `Chat(...)` 方法缺失。
  - 调用方仍使用旧 API，导致 `CS0117: USD_LlmClient does not contain a definition for Chat`。

## 修复策略
- 采用兼容策略（2.2）：在 `USD_LlmClient` 增加 `Chat(USD_Settings, string, string)` 兼容适配层。
- 兼容层返回 `USD_LlmResult`，保持所有旧调用点无需改签名即可编译通过。
- 兼容层与新流程保持一致的超时与诊断字段（含 `singleCallTimeoutSec`、URL/状态码/result/body片段）。
