# URP Scene Doctor v0.63.1（Force Diversity + CA + Curves/SMH）

## 本版重点
- AI Propose 增加 **Force Diversity**：A/B 不够分化会自动重试，仍不够则程序性拉开 Variant B。
- 白名单扩展到 `CA.postExposure / CA.contrast / CA.saturation`。
- Apply 扩展到：Bloom / Vignette / FilmGrain(Thin1+0.6) / WhiteBalance / ColorAdjustments / ColorCurves / ShadowsMidtonesHighlights。
- 方法论硬规则本地兜底：
  - Vignette 固定 0.2/0.2
  - Grain 固定 Thin1 + 0.6
  - Bloom.scatter 固定 0.5
  - Bloom.intensity 按亮度桶并受 ceiling 限制

## 快速流程
1. AI Tuning 页面点击 **Capture Base**。
2. 点击 **Propose Two Variants (AI)**。
3. 查看顶部 Force Diversity 状态（OK / Applied / Failed）。
4. 点击 **Run Both** 生成 A/B 证据。
5. 在 Human Judge 保存 A/B annotation 与 pairwise。

## 输出目录
`Assets/_Tools/URPSceneDoctor/AITuningRuns/<RunId>/`
- `proposal/ai_param_proposal.json`
- `variant_A|B/profile.asset`
- `variant_A|B/snapshot_after.json`
- `variant_A|B/image_metrics_after.json`
- `variant_A|B/image_metrics_diff.json`
- `variant_A|B/deltaPatch.json`
- `compare/pairwise_pref.json`

## 验证建议
- 检查 `proposal` 中 A/B 至少 3 个字段不同。
- 检查 `deltaPatch` 是否包含 CA + Bloom/WB/Vig/Grain 变更。
- 检查 A/B after metrics 至少 2 个关键指标存在差异。
