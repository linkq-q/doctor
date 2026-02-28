# URP Scene Doctor v0.55（Batch Sampler + LabelCatalog + Auto Camera + i18n）

## 新增能力
1. **Batch Sampler**：对白名单场景批量输出 evidence + delta + taste_note，并生成 batch_summary(json/csv)。
2. **LabelCatalog 可扩展标签**：风格目标/问题标签可在资产中直接编辑，无需改代码。
3. **自动相机绑定**：Auto picker（MainCamera优先）+ SceneView fallback。
4. **中英双语切换**：Settings 支持语言切换（中文/English/Auto）。

## LabelCatalog
- 默认资产：`Assets/_Tools/URPSceneDoctor/Config/LabelCatalogs/DefaultLabelCatalog.asset`
- 在 Settings 中可直接指定/编辑 `labelCatalog`。
- 字段：styles / issues（含 id, zh/en 名称）。
- 要求：id 唯一（Hub 设置页会提示重复 id）。

## Batch Sampler 使用
1. 打开 `Tools/URP Scene Doctor`，切到 **Batch Sampler**。
2. `Refresh Scenes` 扫描 `batchSceneRoot`（默认 `Assets/_Tools/URPSceneDoctor/SamplePacks`）。
3. 勾选场景，点击 `Batch Run`。
4. 每个场景流程：
   - Open Scene
   - Auto camera pick
   - BEFORE snapshot/shots
   - Apply VisibleDemo
   - AFTER snapshot/shots
   - Extract deltaPatch
   - 生成 taste_note 初稿
5. 支持失败继续跑；失败原因写入 summary 和 errors.log。

## 输出目录
`Assets/_Tools/URPSceneDoctor/BatchRuns/{RunId}/`
- `Samples/{SceneName}/{timestamp}/`（每场景样本）
- `batch_summary.json`
- `batch_summary.csv`
- `errors.log`（有失败时）

## 快速标注（Batch Sampler 页）
- Style Goal 下拉（来自 LabelCatalog）
- Rating (1-10)
- Issue tags 多选（来自 LabelCatalog）
- `Save Annotation` 回写：
  - 样本目录下 `taste_note.json`
  - `batch_summary.json/csv`

## 自动相机绑定
- Camera Mode：Auto / Manual / SceneView（Settings）
- Auto 规则：
  1) MainCamera tag
  2) enabled + active 且非 UI-only camera
  3) 无可用相机时回退 SceneView

## 安全说明
- 不修改 Packages/ProjectSettings。
- 所有输出位于 `Assets/_Tools/URPSceneDoctor/**`。
- 批处理单场景失败不阻断后续场景。

## 已知限制
- Batch 仅处理本地白名单场景，不做在线下载。
- i18n 当前覆盖核心 Hub 页签与核心按钮文案，后续可继续扩展。
