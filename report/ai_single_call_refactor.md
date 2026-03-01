# AI Tuning Single-Call Refactor Report

## 改造目标
- 将「生成双方案(AI)」改为一次请求一次响应（single call）。
- 去掉隐式轮询与重试请求，确保不会无限 waiting。

## 删除/替换的轮询与等待点
1. `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_LlmClient.cs`
   - 删除阻塞轮询：`while (!op.isDone) { }`
   - 替换为：`AsyncOperation.completed + ManualResetEventSlim.Wait(timeout)` 的单次等待。
2. `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_AiTuningModule.cs`
   - 删除 `MarkWaiting("正在等待 AI 回复...")` 与 `MarkWaiting("正在等待重试回复...")`。
   - 删除 Force Diversity 的第二次 LLM 请求重试逻辑（原先会再次调用 `USD_LlmClient.Chat`）。
   - 按策略改为程序性拉开 VariantB，不再发起二次请求。

## 新增/调整流程
- 新增统一入口：`GenerateProposalSingleCall()`
  - [Send] 立即提示请求已发送
  - 一次 `USD_LlmClient.Chat(...)`
  - 成功：解析并提示 [OK]
  - 失败：提示 [Fail]（包含超时/HTTP/解析失败）
  - `finally` 里释放 `_isProposing`，确保状态可退场

## 超时与可观测性
- `UnityWebRequest.timeout = llmTimeoutSec`
- 业务兜底超时：`singleCallTimeoutSec`
- Verbose 日志包含：url/model/timeout/payload bytes/status/latency/response size
- 失败日志包含：错误类型（HTTP/超时/解析）
- 支持 raw response dump：`rawResponseDumpPath`（支持 `{runId}`）

## Settings 新增字段
- `singleCallTimeoutSec`
- `showAiSendAndReceiveToast`
- `dumpRawResponseToFile`
- `rawResponseDumpPath`

## 验证建议（Unity 编辑器内）
1. 正常配置 API Key 后点击「生成双方案(AI)」，应看到 [Send] -> [OK]。
2. 配置错误 baseUrl，应在超时范围内 [Fail] 退出，不再无限 waiting。
3. 错误 API Key，提示 HTTP 401。
4. 返回非 JSON，提示解析失败并使用兜底方案（同时写 raw dump）。
