# AI Tuning Single-Call Refactor Report

## 改造目标
- 将「生成双方案(AI)」改为一次请求一次响应（single call）。
- 去掉主线程阻塞等待，避免 UnityEditor 出现 busy 卡死。
- 超时/错误时输出完整诊断信息（URL、responseCode、result、error、body片段）。

## 删除/替换的等待点
1. `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_LlmClient.cs`
   - 删除同步阻塞等待（`ManualResetEventSlim.Wait(...)`）。
   - 改为 `while (!op.isDone) { yield return null; }` 的协程推进，并接入 `singleCallTimeoutSec` 业务超时。
2. `Assets/_Tools/URPSceneDoctor/Editor/AI/USD_AiTuningModule.cs`
   - 按钮入口改为 `USD_EditorCoroutineRunner.StartCoroutineOwnerless(...)`。
   - 请求结果在协程完成后统一收敛，不再阻塞 UI 线程。

## 新流程（一次请求一次响应）
- 点击按钮后：
  - 立即显示 `[Send]`。
  - 启动 EditorCoroutine（ownerless）执行单次网络请求。
- 请求完成后：
  - 成功解析：显示 `[OK]` 并写 proposal。
  - 失败/超时：显示 `[Fail]`，写入 warnings，并输出完整诊断。
- `finally` 释放 `_isProposing`，确保状态可退场。

## 超时与诊断
- `UnityWebRequest.timeout = llmTimeoutSec`（网络超时）
- `singleCallTimeoutSec`（业务兜底超时）
  - 触发后会 `req.Abort()`
  - 返回失败并包含 url/responseCode/result/body 片段
- 失败日志样例包含：
  - `url=...`
  - `responseCode=...`
  - `result=...`
  - `error=...`
  - `body=...`

## Settings（已接入 UI）
- `singleCallTimeoutSec`
- `showAiSendAndReceiveToast`
- `dumpRawResponseToFile`
- `rawResponseDumpPath`

## 验证建议（Unity 编辑器内）
1. 正常网络：点击「生成双方案(AI)」应看到 `[Send] -> [OK]`，Editor 不再 busy。
2. 错误 baseUrl：应在超时窗口内 `[Fail]` 并退出 waiting。
3. 错误 API Key：应显示 HTTP 401，日志带完整诊断。
4. 返回非 JSON：应显示解析失败并保留 raw dump（开启时）。
5. 超时场景：60 秒后退出并显示具体诊断，而非仅 `SingleCallTimeout`。
