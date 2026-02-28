# URP Scene Doctor v0.62（AI Draft Form + Visible Persistence）

## v0.62 重点
- AI Assist 从“弹窗 JSON”升级为“窗口内表单”：
  - Draft Panel（AI 草稿）
  - Final Annotation Panel（最终标注）
- 每次关键操作（Generate/Accept/Reject/Save/Regenerate）都会：
  - 在 UI 顶部显示结果（成功/失败）
  - 写入可审计文件
  - 支持一键打开输出目录/文件
- 修复 Accept/Reject 无反应：增加点击日志、异常捕获、busy guard 与落盘反馈。

## AI Assist 输出文件（样本目录）
1. `ai_label_draft.json`
   - 保存 AI 草稿（timestamp/provider/model/raw/parsed/status/error）。
2. `annotation.json`
   - 保存最终确认标注（style/score/tags/next_steps/user_note/source/timestamp）。
3. `ai_action_log.jsonl`
   - 每次动作追加审计行（action/result/error/timestamp）。

## AI Assist 使用流程（推荐）
1. 选择 Sample/Evidence Folder。
2. 点击 Generate（或 Regenerate）。
3. 在 Draft Panel 里检查并可编辑：style/score/tags/next steps。
4. 点击 Accept（写入最终标注）或 Reject（仅记录状态，不删除草稿）。
5. 在 Final Annotation Panel 进一步编辑，点击 Save Annotation。

## Batch 模式同步
- 若样本位于 `BatchRuns` 且检测到 `batch_summary.json/csv`，Accept/Save 会同步：
  - `aiDraftStatus`
  - `userFinalLabel`
  - `lastUpdated`

## 新手提示
- AI 只是草稿，不会自动替你做最终结论。
- 不配置 API Key 也能工作（离线 fallback），但 AI 生成质量会下降。
- 保存后的 `annotation.json` 才是后续学习统计使用的最终结果。
