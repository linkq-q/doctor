# URP Scene Doctor v0.45（unitypackage 用户手册）

## 导入 unitypackage
1. 打开 Unity URP 项目（推荐 Unity 2022.3 + URP 14）。
2. 菜单 `Assets > Import Package > Custom Package...`。
3. 选择 `URPSceneDoctor_v0.45.unitypackage` 导入。

## 打开工具
- 菜单：`Tools/URP Scene Doctor`。
- 顶部显示版本、场景状态与 URP/Volume 概况。

## Pipeline Pack（Base Pack v1）
新增 `Pipeline Pack` 页签，提供基础渲染资产包闭环：
- Pack: `Base Pack v1`
- Buttons: `Preflight / Install / Uninstall / Evidence Pack`
- Status: 安装状态、URP版本、红黄绿检查结果

### Base Pack v1 范围
1. Distance Fog（自研）
2. Volumetric Light（第三方 MIT，见 ThirdPartyNotices）

### Preflight 检查项
- URP 是否启用、RendererData 可读性
- Camera Post Processing 提示
- Distance Fog 前置（Opaque/Depth 纹理提示 + 是否已安装 feature）
- Volumetric Light 前置（URP 版本提示 + vendored 目录检查 + 是否已安装 feature）

### Install / Uninstall
- Install：安全添加 RendererFeature，并在 `Assets/_Tools/URPSceneDoctor/PipelinePack/...` 下创建设置资产。
- Uninstall：仅移除工具安装的 RendererFeature，不删除用户原有资源。
- 安装/卸载动作会写出报告到 Reports 目录。

### Pipeline Evidence Pack
- 在 Pipeline Pack 页面点击 `Evidence Pack`：
  1) Capture BEFORE
  2) Install Base Pack v1
  3) Capture AFTER
  4) 输出 `summary.md + diff.json + before/after shots`

## Apply Mode（Safe vs VisibleDemo）
Atmosphere Doctor 面板支持 **Apply Options**：
- `SafeNeutral`（默认）
- `VisibleDemo`
- Style Profile：Neutral Baseline / Clean Stylized / Warm Dusk / Moody Cool
- Bind Policy 默认 OFF（不覆盖已有 Global Volume）

## Style Profiles（风格倾向）
- 风格参数由 `visibleIntensity` + 参数范围插值生成，不是写死单点值。
- 仅作用于 Scene Doctor 新建 profile，不直接改用户原 profile。

## Evidence Pack（可分享证据包）
输出目录：
`Assets/_Tools/URPSceneDoctor/EvidencePacks/{SceneName}/{timestamp}/`
- before/after 截图
- summary.md
- diff.json
- report.md / report.json
- snapshot_before.json / snapshot_after.json

## 第三方许可
- 详见：`Assets/_Tools/URPSceneDoctor/Documentation/ThirdPartyNotices.md`

## 推荐工作流（v0.45）
1. Open Demo Scene
2. Atmos Scan
3. Pipeline Pack -> Preflight
4. Pipeline Pack -> Install（Base Pack v1）
5. Pipeline Pack -> Evidence Pack（比较前后）
6. 真实项目中优先 SafeNeutral 与低强度参数

## 已知限制 / 性能注意
- Base Pack v1 聚焦基础能力（Distance Fog + Volumetric Light），避免一次引入过多效果。
- Volumetric Light 可能带来 GPU 开销，需按平台做预算验证。
- Header 快照为节流缓存，不在 OnGUI 每帧重扫。
