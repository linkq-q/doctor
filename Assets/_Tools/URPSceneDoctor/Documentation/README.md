# URP Scene Doctor v0.61（Full I18N + Newcomer Guide）

## 新增：全量双语文案系统
- 新增 `USD_Loc` 统一入口：`T(key)` / `T(key,args)` / `C(key, tooltipKey)`。
- 支持 Language：`Auto / 中文 / English`，设置保存到 `EditorPrefs`，切换后窗口立即刷新。
- 文案来源优先 `LocTable_Default.asset`（自动创建到 `Assets/_Tools/URPSceneDoctor/Config/Localization/`），缺失时回退内置表。
- 缺 key 会显示 `[[key]]` 并输出 warning，方便补齐。

## 新手引导（HelpBox + Tooltip）
以下页面已增加一句话说明与关键按钮 tooltip：
- Settings（LLM 配置与离线降级说明）
- Evidence Pack（Before/After 与输出目录说明）
- Tuning（BEFORE/AFTER/Delta 的正确步骤）
- AI Assist（AI 草稿需人工确认，不做云端自动训练）
- Batch Sampler（批量场景运行，单场景失败不中断）

## 如何切换语言
1. 打开 `Tools/URP Scene Doctor`。
2. 在 `Settings` 页找到 `Language`。
3. 选择 `Auto / 中文 / English`。
4. UI 会立即刷新。

## 如何扩展文案 key
1. 打开（或首次运行后自动生成）
   `Assets/_Tools/URPSceneDoctor/Config/Localization/LocTable_Default.asset`。
2. 新增 entry：`key / zh / en / note`。
3. 在代码中使用 `USD_Loc.T("your.key")` 或 `USD_Loc.C("your.key", "tooltip.key")`。

## i18n 审计
- 菜单：`Tools/URP Scene Doctor/I18N Audit`
- 输出：`Assets/_Tools/URPSceneDoctor/Reports/i18n_audit_report.txt`
- 用途：检测 UI API 调用中的硬编码文本（建议保持 0）。

## 安全说明
- 不修改 `Packages/`、`ProjectSettings/`。
- 所有输出保持在 `Assets/_Tools/URPSceneDoctor/**`。
