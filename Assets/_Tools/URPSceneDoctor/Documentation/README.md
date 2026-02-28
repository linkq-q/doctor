# URP Scene Doctor v0.6（Vision-lite + DeepSeek + AI Assist）

## v0.6 新增
1. **Vision-lite 指标**：Evidence Pack 与 Batch Sampler 会输出 `image_metrics_before.json / image_metrics_after.json / image_metrics_diff.json`。
2. **AI Assist 模块**：
   - Draft Labeling（`ai_label_draft.json`）
   - Pairwise Preference（`pairwise_pref.json`）
   - Explainable Summary（`report_summary_ai.md`）
   - Rule Authoring Assist（`Drafts/rule_draft.json`）
3. **DeepSeek(OpenAI-compatible) 接入**：Settings 可配置 provider/base_url/model/timeout/maxTokens/temperature。
4. **离线降级**：未配置 key 或 Provider=Off 时，AI 功能自动回退模板，不影响 Scan/Apply/Evidence/Batch 核心流程。

## DeepSeek 设置
1. 打开 `Tools/URP Scene Doctor` -> `Settings`。
2. 在 `LLM Provider` 区域设置：
   - `llmProvider`: `DeepSeek` 或 `Custom(OpenAI-compatible)`
   - `llmBaseUrl`: 默认 `https://api.deepseek.com`
   - `llmModel`: 默认 `deepseek-chat`
   - `api_key`: 仅存 `EditorPrefs`（不写入资产）
3. `promptLanguage` 可选 `zh/en`。

## Vision-lite 指标定义（本地）
- brightness_bucket（由 luma_mean 分桶）
- luma_mean / luma_std
- overexposure_ratio（luma > 0.95）
- center_contrast_ratio（中心 ROI std / 全局 std）
- saturation_mean（HSV.S）
- warm_cool_balance（R-B 均值，[-1,1]）

## AI Assist 使用
1. 进入 `AI Assist` 页。
2. 指定一个 Evidence/Sample 目录（包含 snapshot/delta/metrics）。
3. 按需点击：
   - `Draft Labeling`
   - `Explainable Summary`
   - `Rule Authoring Assist`
   - `Save Pairwise Preference`
4. 所有 AI 输出均落盘，附 source/timestamp/error（可审计）。

## Batch + AI
- Batch 运行后每个样本会生成 AI draft（有 key 调模型，无 key 使用 fallback）。
- `batch_summary.json/csv` 增加 `aiDraftStatus` / `userFinalLabel`。
- 批处理列表支持 `Accept AI Draft` / `Reject AI Draft` 再 `Save Annotation`。

## 输出目录
- Evidence: `Assets/_Tools/URPSceneDoctor/EvidencePacks/<Scene>/<ts>/`
- Batch: `Assets/_Tools/URPSceneDoctor/BatchRuns/<RunId>/Samples/<Scene>/<ts>/`
- AI 典型产物：
  - `ai_label_draft.json`
  - `pairwise_pref.json`
  - `report_summary_ai.md`
  - `Drafts/rule_draft.json`

## 安全说明
- 默认不改现有资产（仅创建新 profile / 新报告输出）。
- 不修改 `Packages/` 与 `ProjectSettings/`。
- 所有输出均在 `Assets/_Tools/URPSceneDoctor/**`。

## 已知限制
- v0.6 不做真正视觉模型推理，仅基于本地 Vision-lite 指标 + 文本模型。
- Rule Authoring Assist 仅生成草案，不直接改默认 RulePack。
