# URP Scene Doctor v0.5（Learning MVD + 方法论落地）

## 导入 unitypackage
1. 打开 Unity URP 项目（推荐 Unity 2022.3 + URP 14）。
2. 菜单 `Assets > Import Package > Custom Package...`。
3. 选择 `URPSceneDoctor_v0.5.unitypackage` 导入。

## v0.5 核心能力
1. Learning MVD：Snapshot 记录关键后处理数值（volumeKeyValues）。
2. Delta Patch：输出 VolumeKey 的 before/after 数值变化。
3. 方法论 v1.0：TastePolicy + Policy Checker（Vig/Bloom/Grain + Bloom brightness bucket）。
4. Evidence Pack：自动生成 `taste_note.json` 模板，并从 deltaPatch 预填 actions。

## Learning MVD（Snapshot/Delta）
Snapshot 会记录以下关键键值（若 override active）：
- `CA.postExposure`, `CA.contrast`, `CA.saturation`
- `WB.temperature`, `WB.tint`
- `Vig.intensity`, `Vig.smoothness`
- `Bloom.intensity`, `Bloom.scatter`
- `Grain.intensity`

在 Tuning 页执行 `Capture BEFORE/AFTER -> Extract Delta Patch` 后，
`deltaPatch.changedFields` 会包含 `VolumeKey.*` 的具体数值变化。

## 方法论 v1.0（TastePolicy + Checker）
默认策略：`YourTAStyle_v1`（配置于 `Config/TastePolicies`）
- 优先级顺序：明暗分区 → 主视觉 → 基调统一 → 冷暖对比 → 收口 → 精修
- 硬规则（至少）：
  - Vignette intensity/smoothness 约 0.2
  - FilmGrain intensity 约 0.6
  - Bloom scatter 约 0.5
- Bloom 亮度桶（Settings 的 `policyBrightnessBucket`）：
  - High / Mid / Low
  - 各自检查 Bloom intensity 目标区间，并受 ceiling 限制

Atmosphere Doctor / Report / Evidence Summary 会显示 Policy Checklist（Pass/Warnings）。

## Evidence Pack（含 taste_note）
输出目录：
`Assets/_Tools/URPSceneDoctor/EvidencePacks/{SceneName}/{timestamp}/`

包含：
- before/after 截图
- `snapshot_before.json` / `snapshot_after.json`
- `deltaPatch.json`（自动从 before/after 提取）
- `summary.md`, `diff.json`, `report.md`, `report.json`
- `taste_note.json`（模板 + actions 预填）

## 安全原则
- 默认不修改现有资产（除用户主动 Apply/Install）。
- 输出文件统一在 `Assets/_Tools/URPSceneDoctor/**` 下。
- Apply/安装流程保持 Undo 记录。

## 自测步骤（v0.5）
1. 打开 Demo 场景。
2. Tuning：Capture BEFORE。
3. 手动改 `CA.contrast / WB.temperature / Bloom.intensity / Vig.intensity / Grain.intensity`。
4. Capture AFTER。
5. Extract Delta Patch，确认 `VolumeKey.*` 至少 4 条变化。
6. 生成 Evidence Pack，确认 summary 出现 Policy Checklist，且输出 `taste_note.json`。

## 说明
Learning v0.5 只学习“参数范围/顺序”，不做图像审美判断。
