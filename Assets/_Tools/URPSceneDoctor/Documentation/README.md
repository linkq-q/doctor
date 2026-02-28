# URP Scene Doctor v0.63（AI Propose → Apply → Evidence → Judge）

## 你现在能做什么
在 `Tools/URP Scene Doctor` 中新增 **AI 调参 / AI Tuning** 页面：
1. **Capture Base**：固定 BEFORE 基准（snapshot + 截图 + metrics）。
2. **Propose Two Variants (AI)**：生成 A/B 两套参数方案（白名单字段）。
3. **Run Variant A/B 或 Run Both**：自动克隆并应用新 profile，产出 AFTER 证据与 delta。
4. **Human Judge**：给 A/B 打分、选问题标签、保存 annotation，并保存 A/B 偏好。

---

## 目录结构（每次运行）
`Assets/_Tools/URPSceneDoctor/AITuningRuns/<RunId>/`

- `base/`
  - `snapshot_before.json`
  - `image_metrics_before.json`
  - `screenshots_before/*`
- `proposal/`
  - `ai_param_proposal.json`
- `variant_A/`、`variant_B/`
  - `profile.asset`
  - `snapshot_after.json`
  - `image_metrics_after.json`
  - `image_metrics_diff.json`
  - `deltaPatch.json`
  - `screenshots_after/*`
  - `annotation.json`（你保存后生成）
- `compare/`
  - `pairwise_pref.json`（你保存偏好后生成）

---

## 参数白名单（v0.63）
- `Bloom.intensity`
- `Bloom.scatter`
- `WB.temperature`
- `WB.tint`
- `Vig.intensity`
- `Vig.smoothness`
- `Grain.intensity`

系统会自动 clamp 到安全范围，超出值会被压回合法区间。

---

## 新手建议（通俗版）
- **先固定 Base，再跑 A/B**：这样你比较的是“同一起跑线”，不是随机结果。
- **AI 是提案，不是裁判**：最后以你的 `annotation.json` 和 `pairwise_pref.json` 为准。
- **优先看三件事**：过曝比例（overexposure_ratio）、中心对比（center_contrast_ratio）、冷暖倾向（warm_cool_balance）。
- **不要直接覆盖旧资产**：本工具会创建新 profile，方便回滚和复查。

---

## 快速上手（3 分钟）
1. 打开要调的场景。
2. 切到 `AI Tuning`。
3. 选目标风格（clean / warm / moody）。
4. 点 **Capture Base**。
5. 点 **Propose Two Variants (AI)**。
6. 点 **Run Both**。
7. 在 Human Judge 区：
   - 给 A、B 各打分；
   - 给每个方案选 1~3 个问题标签；
   - 保存 annotation；
   - 选择 A better / B better / Tie 并保存。

完成后就形成了可学习的闭环数据。
