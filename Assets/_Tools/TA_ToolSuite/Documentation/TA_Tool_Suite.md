# TA Tool Suite（统一入口）

## 目标
将仓库中的 Editor 工具统一到一个 Hub：
- Scene Doctor（URPSceneDoctor）
- Render Audit（TA_Toolchain）
- Anim Tools（AnimTools）

并统一输出目录到：
- `Assets/_Tools/TA_ToolSuite/Reports/<ToolName>/<SceneName>/<timestamp>/`
- `Assets/_Tools/TA_ToolSuite/Snapshots/...`
- `Assets/_Tools/TA_ToolSuite/Patches/...`

## 如何打开
1. Unity 菜单：`Tools/TA Tool Suite`
2. Hub 顶部点击 `Quick Check`，查看各模块是否可用。

## 每个模块如何跑一次
### Scene Doctor
- 在 Hub 里进入 `Scene Doctor` 标签。
- 点击 `Open Scene Doctor`，然后在 Scene Doctor 里运行 Scan / DryRun / Apply。
- 若缺失，会显示未安装提示。

### Render Audit
- 在 Hub 里进入 `Render Audit` 标签。
- 点击 `Open Render Audit Window`。
- 执行 `Run Audit`。
- 报告默认路由到 TA_ToolSuite 统一目录（按工具/场景/时间）。

### Anim Tools
- 在 Hub 里进入 `Anim Tools` 标签。
- 点击 `Open Anim Audit & Generator`。
- 默认报告路径与控制器生成路径会指向 TA_ToolSuite 统一目录。
- Controller 生成支持 Undo 回滚。

## Reports 页
- Hub `Reports` 页会扫描 `Assets/_Tools/TA_ToolSuite/Reports` 最近报告。
- 点击条目可直接在系统文件管理器打开。

## 安全说明
- 不修改 `Packages/`、`ProjectSettings/`。
- 不提交 `Library/Temp/.vs/Logs/Obj`。
- 整合采用适配器方案，尽量不侵入原工具逻辑。

## 最小自测清单
1. 打开 `Tools/TA Tool Suite`。
2. 点击 `Quick Check`，确认模块状态。
3. 分别打开三个模块窗口。
4. 在 Render Audit 运行一次并导出报告。
5. 在 Anim Tools 扫描并生成一次报告（可选生成 controller），检查输出目录。
6. 在 Reports 页查看索引并打开报告。
