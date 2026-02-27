# URP Scene Doctor v0.4（unitypackage 用户手册）

## 导入 unitypackage
1. 打开 Unity URP 项目（推荐 Unity 2022.3 + URP 14）。
2. 菜单 `Assets > Import Package > Custom Package...`。
3. 选择 `URPSceneDoctor_v0.4.unitypackage` 导入。

## 打开工具
- 菜单：`Tools/URP Scene Doctor`。
- 顶部显示版本、场景状态与 URP/Volume 概况。

## Apply Mode（Safe vs VisibleDemo）
Atmosphere Doctor 面板新增 **Apply Options**：
- **Apply Mode**
  - `SafeNeutral`（默认）：生成新 Global Volume + 新 Profile（中性参数），视觉变化极小。
  - `VisibleDemo`：仍创建新 Profile，但会按风格注入可见变化，便于演示/测试证据包。
- **Style Profile**
  - Neutral Baseline
  - Clean Stylized
  - Warm Dusk
  - Moody Cool
- **Bind Policy**
  - `Assign new profile to existing Global Volume` 默认 OFF。
  - 默认不会覆盖现有 Global Volume 绑定；仅用户主动开启才绑定。

## Style Profiles（风格倾向）
- 风格参数由 `visibleIntensity` + 参数范围插值生成，不是写死单点值。
- 仅作用于 Scene Doctor 新建/复制出来的 profile，不直接改用户原 profile。
- 推荐用途：Demo 场景和评审演示，真实项目默认使用 SafeNeutral。

## Evidence Pack（可分享证据包）
### 如何生成
- 在 `Atmosphere Doctor` 点击 `Create Evidence Pack`，或进入 `Evidence Pack` 模块点击同名按钮。
- 证据包会按当前 Apply Mode + Style Profile 进行 Before/After。

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

### summary.md 会包含
- Apply Mode / Style Profile / Bind Policy
- Key visual knobs（如 WB temperature、contrast、saturation）
- Key diff summary（Top 5）
- 每张 shot 的相机参数（pos / yaw / pitch / fov）用于复现

## Taste Policy（偏好策略）
- 作用：不存固定参数值，只控制排序、禁忌与处方措辞。
- 在 `Settings` 选择 `DefaultTastePolicy`。

## Delta Library（多样本学习）
- `Add Last Tuning Result` 或 `Import Folder...` 导入样本。
- `Recompute Stats` 生成：
  - `Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats/delta_stats.json`
  - `Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats/delta_stats.md`
- 启用 learning 后，Atmos 工单会追加 `(Learned)` 文本。

## 推荐工作流（v0.4）
1. 打开 Demo Scene。
2. Atmos `Scan`。
3. `Apply`: 选择 `VisibleDemo + Warm Dusk`（或其它风格）。
4. 点击 `Create Evidence Pack`，检查 before/after 与 summary。
5. 真实项目切回 `SafeNeutral`，以建议与安全 apply 为主。

## 已知限制 / 性能注意
- Hub 顶部状态栏使用缓存快照 + 节流刷新，避免 OnGUI 每帧全量扫描。
- 透明物体统计按 Renderer 计数（每个 renderer 最多计 1 次）。
- 快照与补丁文件名包含毫秒级时间戳，并有路径冲突回避策略。
- 内置 Style Profile 资产会在首次打开 Hub 时自动创建到 `Config/StyleProfiles`。
