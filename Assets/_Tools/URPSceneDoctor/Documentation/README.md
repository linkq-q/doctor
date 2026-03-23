# URP Scene Doctor v0.64（SMH 上限 + 中文建议 + Metrics 面板 + LLM 状态）

## 本版改进
- SMH（Shadows/Midtones/Highlights）偏移加入上限控制（默认 `0.1`，可在 Settings 配置）。
- 建议/工单文案加入中文规范化器：统一“建议/原因/如何验证”句式，并替换常见英文缩写为中文全称（保留括号缩写）。
- 新增 **指标面板 / Metrics**：可加载证据包 before/after 指标，支持可视化条形和 delta 对比。
- LLM 调用状态可见化：AI 页面会显示“已发送 / 等待 / 成功 / 失败”状态，不再只看 Console。

## Metrics 面板
入口：Hub 左侧 `指标面板 / Metrics`
- `读取最新证据包`：自动读取当前场景最新 Evidence Pack。
- `选择文件夹`：手动指定含 `image_metrics_before/after.json` 的目录。
- `使用当前场景快照`：快速查看当前场景近似指标。

支持 provider 注册扩展：
- 在 `Assets/_Tools/URPSceneDoctor/Editor/Metrics/Providers/` 新增 `IUsdMetricProvider` 实现类后，面板会自动出现该指标，无需修改面板代码。

## LLM 状态反馈
以下入口都有窗口内状态提示：
- AI Assist: Draft Labeling / Explainable Summary / Rule Authoring Assist
- AI Tuning: Propose Two Variants

## 说明
这是 Unity Editor 工具更新，建议在 Unity 中进行完整编译与页面验证。
