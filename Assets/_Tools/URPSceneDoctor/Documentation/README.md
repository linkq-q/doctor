# URP Scene Doctor v0.3（unitypackage 用户手册）

## 导入 unitypackage
1. 打开 Unity URP 项目（推荐 Unity 2022.3 + URP 14）。
2. 菜单 `Assets > Import Package > Custom Package...`。
3. 选择 `URPSceneDoctor_v0.3.unitypackage` 导入。

## 打开工具
- 菜单：`Tools/URP Scene Doctor`。
- 顶部显示版本、场景状态与 URP/Volume 概况。

## Evidence Pack（可分享证据包）
### 如何生成
- 在 `Atmosphere Doctor` 点击 `Create Evidence Pack`，或进入 `Evidence Pack` 模块点击同名按钮。

### 生成内容
输出目录：
`Assets/_Tools/URPSceneDoctor/EvidencePacks/{SceneName}/{timestamp}/`
- `before/shot_01..06.png`
- `after/shot_01..06.png`
- `summary.md`
- `diff.json`
- `report.md` / `report.json`
- `snapshot_before.json` / `snapshot_after.json`
- `deltaPatch.json`（若当次可用）

### 如何分享
- 直接把整包文件夹打包给 TA/美术/程序即可复现“证据 + 建议 + 差异”。

## Taste Policy（偏好策略）
- 作用：不存固定参数值，只控制排序、禁忌与处方措辞。
- 在 `Settings` 选择 `DefaultTastePolicy`。
- 会影响：
  1. 工单排序（severityWeight * categoryWeight + 轻学习加成）
  2. recommendedRange 末尾追加 `(TastePolicy) prefer/avoid`
  3. 报告中显示 policy 名、priorityOrder、forbiddenActions

## Delta Library（多样本学习）
### 添加样本
- `Add Last Tuning Result`：把最近 Tuning 结果登记到样本库。
- `Import Folder...`：导入已有样本目录（含 `deltaPatch.json`）。

### 统计
- 点击 `Recompute Stats` 生成：
  - `Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats/delta_stats.json`
  - `Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats/delta_stats.md`
- 输出内容：样本数、Top changed fields、Top5 hints、数值字段区间。

### 如何让建议“像你”
- 启用 learning 后，Atmos 工单处方会追加 `(Learned) ...`。
- 报告 Summary 会显示学习状态与 top hint。

## 推荐工作流（你的计划）
1. 下载场景 → `Scan/DryRun`
2. `Apply`（安全生成 Profile）
3. 手调到满意
4. Tuning：`BEFORE/AFTER`
5. `Extract Delta Patch`
6. Delta Library：`Add Last Tuning Result` → `Recompute Stats`
7. 下一次项目建议自动包含 learned hints

## 自测步骤（从导入到 Evidence Pack）
1. 导入 unitypackage。
2. 打开 `Tools/URP Scene Doctor`。
3. 点击 `Open Demo Scene`。
4. Atmos 页点击 `Scan`，确认产出 report。
5. 点击 `Create Evidence Pack`，检查 evidence pack 文件夹完整。
6. 进入 Tuning 生成 delta patch。
7. 进入 Delta Library 添加样本并 `Recompute Stats`。
8. 回到 Atmos 再扫一遍，确认处方出现 `(Learned)` 文本。
