# 01 - 产品概览 (Product Overview)

> 审计日期：2026-03-13
> 项目名称：Doctor（Unity 动物生态模拟 + 技术美术工具链）

---

## 1. 项目定位

**Doctor** 是一个基于 Unity 引擎的项目，包含两大核心部分：

1. **动物生态模拟系统（Animal System）**：一套程序化动物生成与 AI 行为系统，支持多物种（兔子、爬虫、狐狸、蝴蝶）的自动孵化、对象池管理、LOD 优化和状态机驱动的 AI 行为。
2. **技术美术工具链（TA Toolchain）**：面向 Unity 编辑器的专业工具套件，包含 URP 渲染审计、动画资产分类与控制器生成、场景学习上下文系统。

---

## 2. 技术栈

| 层面 | 技术 |
|------|------|
| 引擎 | Unity（URP 渲染管线） |
| 语言 | C# |
| AI 导航 | Unity NavMesh |
| 渲染 | Universal Render Pipeline (URP) |
| 数据配置 | ScriptableObject |
| 序列化 | Unity JsonUtility / System.IO |
| 编辑器扩展 | EditorWindow, CustomEditor |

---

## 3. 代码统计

### 总量
- **源码文件**：24 个 .cs 文件
- **总代码行数**：约 4,055 行
- **模块数量**：4 个核心模块

### 模块分布

| 模块 | 路径 | 文件数 | 行数 | 占比 |
|------|------|--------|------|------|
| Animal System | `Assets/_Game/Animals/` | 11 | 1,426 | 35.2% |
| TA_Toolchain (渲染审计) | `Assets/_Tools/TA_Toolchain/` | 6 | 1,285 | 31.7% |
| AnimTools (动画工具) | `Assets/Editor/AnimTools/` | 5 | 835 | 20.6% |
| URPSceneDoctor (学习上下文) | `Assets/_Tools/URPSceneDoctor/Editor/` | 2 | 509 | 12.5% |

---

## 4. 核心模块详解

### 4.1 Animal System（动物生态系统）

**职责**：运行时动物生成、行为、生命周期管理

```
AnimalSpeciesConfig (ScriptableObject)
    ↓ 配置注入
AnimalSpawnManager → AnimalPool → AnimalAgentBase (抽象基类)
    ↓                                 ├── RabbitAgent (3 状态机: Idle/Wander/Flee)
AnimalSpawnZone                       ├── CrawlerAgent (移动/逃跑/渐隐消失)
    ↓                                 ├── FoxAgent (巡逻/观察/追击)
AnimalDebugHUD                        └── ButterflyAgent (Perlin噪声漫游)
```

**关键特性**：
- 对象池化，避免 Instantiate/Destroy 开销
- 基于距离的 LOD：动画器禁用、物理休眠
- 管理式 Tick 分发，限制每帧操作数
- 加权物种选择，可配置孵化参数
- NavMesh 导航 + 直接 Transform 移动降级方案

### 4.2 TA_Toolchain（渲染审计工具）

**职责**：扫描 URP 场景，检测性能问题，生成报告

```
RenderAuditConfig → RenderAuditScanner → ReportModels → ReportWriter
                         ↓                                    ↓
                    RenderAuditWindow          JSON 报告 + 建议
                         ↓
                    RecommendationEngine → 能力检测 + 优化建议
```

**扫描维度**：
- 渲染器：材质数、批次数、透明排序
- 灯光：实时光数量、阴影配置
- 纹理：内存估算、超尺寸检测
- 网格：面数/顶点数统计
- 粒子系统、反射探针
- 缺失脚本检测

### 4.3 AnimTools（动画审计工具）

**职责**：自动分类动画资产，生成 AnimatorController

```
AnimScanner → AnimClassifier → ControllerGenerator
    ↓              ↓                    ↓
AnimAuditWindow   分类规则      BlendTree + 状态机
    ↓
AnimReportWriter → Markdown/JSON 报告
```

**支持的动画分类**：Idle、Walk、Trot、Run、Attack、Hit、Death、Swim、Fly 等

### 4.4 URPSceneDoctor（学习上下文）

**职责**：聚合学习数据，生成格式化的上下文提示

- 多阶段学习（Stage0-3 可配置权重）
- JSON 标注、偏好对、品味笔记、增量统计
- 滚动统计计算（均值、标准差、极值）

---

## 5. 数据流向

```
[Editor 工具层]
  AnimAuditWindow ──扫描──→ AnimationClip 资产 ──分类──→ AnimatorController
  RenderAuditWindow ──扫描──→ 场景 Renderer/Light/Material ──分析──→ JSON 报告
  AnimalSystemSetupWizard ──创建──→ AnimalSpeciesConfig + 场景 Manager

[Runtime 游戏层]
  AnimalSpawnManager ──读取──→ AnimalSpeciesConfig
       ↓
  AnimalSpawnZone ──触发──→ AnimalPool.Rent()
       ↓
  AnimalAgentBase.Init() ──驱动──→ NavMesh / Transform 移动
       ↓
  AnimalDebugHUD ──读取──→ 实时状态显示
```

---

## 6. 继承体系

```
MonoBehaviour
├── AnimalAgentBase (abstract)
│   ├── RabbitAgent
│   ├── CrawlerAgent
│   ├── FoxAgent
│   └── ButterflyAgent
├── AnimalPool
├── AnimalSpawnZone
├── AnimalSpawnManager
└── AnimalDebugHUD

ScriptableObject
├── AnimNamingRules
├── AnimalSpeciesConfig
└── RenderAuditConfig

EditorWindow
├── RenderAuditWindow
├── AnimAuditWindow
└── AnimalSystemSetupWizard
```

---

## 7. 设计模式使用

| 模式 | 应用位置 | 说明 |
|------|----------|------|
| 对象池 | AnimalPool | 预分配 + 队列复用 |
| 状态机 | RabbitAgent, FoxAgent, CrawlerAgent | 枚举状态 + switch 驱动 |
| 策略模式 | AnimClassifier.PickBest() | 灵活的剪辑选择策略 |
| 工厂模式 | ControllerGenerator | 动态创建 AnimatorController |
| 构建器模式 | USD_LearningContext.Build() | 多源 JSON 聚合 |
| 配置驱动 | ScriptableObject 全项目使用 | 减少硬编码 |
| 模板方法 | AnimalAgentBase.Tick() | 抽象方法由子类实现 |

---

## 8. 项目成熟度评估

| 指标 | 评分 | 说明 |
|------|------|------|
| 代码完整性 | ★★★☆☆ | 核心功能完成，但缺少运行时 UI、存储、网络 |
| 架构清晰度 | ★★★★☆ | 分层合理，职责划分清晰 |
| 可维护性 | ★★★★☆ | ScriptableObject 配置驱动，扩展友好 |
| 文档程度 | ★★☆☆☆ | 无 README、无 XML doc、中文 UI 标签有限 |
| 测试覆盖 | ★☆☆☆☆ | 无单元测试、无集成测试 |
| 生产就绪 | ★★☆☆☆ | 需要补充错误处理、日志、配置验证 |
