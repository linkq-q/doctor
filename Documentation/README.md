# URP Scene Doctor (Atmosphere-first) v0.1

## 功能概览
- 菜单入口：`Tools/URP Scene Doctor`。
- Hub 模块：Atmosphere Doctor / Render Doctor / Tuning (Before/After) / Reports / Settings。
- 每个诊断模块支持：Scan、Dry Run、Apply、Export Report。

## 工作流
1. 打开场景后进入 `Tools/URP Scene Doctor`。
2. 在 **Atmosphere Doctor** 点击 `Scan` 或 `Dry Run`，会生成 snapshot + work orders 报告（MD + JSON）。
3. 需要落盘时点击 `Apply`：
   - 若无 Global Volume，创建 `USD_GlobalVolume`。
   - 创建新 Volume Profile 到 `Assets/_Tools/URPSceneDoctor/Patches/{SceneName}/...`。
   - 默认不覆盖旧 Profile（仅生成并可选择绑定）。
4. 在 **Tuning**：
   - 点击 `Capture BEFORE`。
   - 手调场景参数。
   - 点击 `Capture AFTER`。
   - 点击 `Extract Delta Patch` 输出 `deltaPatch.json`，并自动作为 Atmos 建议的附加经验文本来源。
5. 在 **Render Doctor** 执行轻量性能建议扫描。

## 报告与产物
- Snapshots：`Assets/_Tools/URPSceneDoctor/Snapshots/{SceneName}/`。
- Reports：`Assets/_Tools/URPSceneDoctor/Reports/{SceneName}/`。
- Patches：`Assets/_Tools/URPSceneDoctor/Patches/{SceneName}/`。
- 默认规则包：`Assets/_Tools/URPSceneDoctor/Config/RulePacks/DefaultRulePack.json`（12 条氛围规则）。

## 安全策略
- 默认只读扫描，Apply 仅做安全最小闭环（创建新资产 + 绑定）。
- 写操作均通过 Undo 注册，支持撤销。
- 对现有资产修改应通过 `requiresUserOptIn` 标记，并在 UI 显式放开。

## 已知限制（v0.1）
- 规则触发仅基于工程证据，不做图像内容分析。
- Delta Patch 仅文本注入推荐范围，不自动改规则阈值。
- Render Doctor 为启发式建议，不替代 Profiler。

## 后续建议
- 为 RulePack 增加 UI 编辑器与版本管理。
- 将 Apply 动作扩展到可控的 URP 参数 patch（带 opt-in 双确认）。
- 引入多样本统计生成更稳定的 recommendedRanges。
