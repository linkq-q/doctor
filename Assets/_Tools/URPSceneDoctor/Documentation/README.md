# URP Scene Doctor v0.2（unitypackage 用户手册）

## 1) 导入 unitypackage
1. 在 Unity 中打开目标 URP 项目（建议 Unity 2022.3 + URP 14）。
2. 菜单 `Assets > Import Package > Custom Package...`。
3. 选择导出的 `URPSceneDoctor_v0.2.unitypackage` 并全部导入。

## 2) 打开工具
- 菜单：`Tools/URP Scene Doctor`。
- Hub 顶部会显示版本号 `v0.2`、当前场景、URP 资产、Renderer、Global Volume 状态。

## 3) Demo 场景如何打开
- 在 Hub 顶部点击 `Open Demo Scene`。
- 若当前场景有未保存改动，会弹出保存确认。
- 工具会打开（或自动创建）`Assets/_Tools/URPSceneDoctor/Samples/Scenes/USD_DemoScene.unity`。

## 4) Quick Verify 怎么用
- 在 Hub 顶部点击 `Quick Verify`。
- 工具会顺序执行：`Atmos Scan -> Export Report`（不会自动 Apply）。
- 完成后弹窗显示：报告路径、命中规则数量、warnings 数量。

## 5) Scan / Dry Run / Apply
在 Atmosphere Doctor 或 Render Doctor 页面：
- `Scan`：只读扫描，输出 snapshot + 报告。
- `Dry Run`：生成工单，不执行资产修改。
- `Apply`：执行允许动作。

### Apply 资产行为（安全默认）
- 仅创建新资产，不覆盖现有资产：
  - 若无 Global Volume，会创建 `USD_GlobalVolume`。
  - 始终创建新的 Volume Profile 到 `Assets/_Tools/URPSceneDoctor/Patches/{SceneName}`。
- 复选框：`Assign new profile to existing Global Volume (default OFF)`
  - OFF：已有 Global Volume 时只生成 profile，不替换绑定。
  - ON：允许替换绑定（已记录 Undo）。

## 6) Undo 与可回滚
- 所有创建/修改均通过 Undo 记录，可使用 `Edit > Undo` 回滚。

## 7) 输出路径
- Reports：`Assets/_Tools/URPSceneDoctor/Reports/{SceneName}/`
- Snapshots：`Assets/_Tools/URPSceneDoctor/Snapshots/{SceneName}/`
- Patches：`Assets/_Tools/URPSceneDoctor/Patches/{SceneName}/`

## 8) Tuning（你的审美沉淀）
1. `Capture BEFORE`
2. 手动调整场景
3. `Capture AFTER`
4. `Extract Delta Patch`

UI 会展示：
- Before/After 路径
- DeltaPatch 路径
- changed fields 统计
- `Open Snapshot Folder` / `Open Patch Folder` 快捷按钮

Atmos 工单会在处方范围末尾追加 `(Personal delta hint) ...` 文本。
