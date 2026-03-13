# 05 - 改进需求 Backlog (Improvement Backlog)

> 审计日期：2026-03-13

---

## 优先级说明

| 标记 | 含义 | 行动时间 |
|------|------|----------|
| **P0** | 紧急 — 阻塞核心功能或存在严重风险 | 立即处理 |
| **P1** | 重要 — 显著提升产品质量 | 当前迭代处理 |
| **P2** | 优化 — 锦上添花，改善体验 | 规划到后续迭代 |

---

## P0 - 紧急改进

### 1. 修复异常吞没问题
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P0 |
| 用户影响度 | 高 |
| 实现难度 | 低 |
| 建议 | `USD_LearningContext.ReadJson()` 中的裸 `catch` 块会隐藏所有错误，导致数据丢失无法追踪。应替换为 `catch (JsonException ex)` 等具体异常捕获，并添加 `Debug.LogWarning` 日志输出。 |
| 相关文件 | `Assets/_Tools/URPSceneDoctor/Editor/USD_LearningContext.cs` |

### 2. 添加 Player Transform 空引用保护
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P0 |
| 用户影响度 | 高 |
| 实现难度 | 低 |
| 建议 | `AnimalSpawnManager` 中 player Transform 为 null 时直接访问 `.position` 将导致运行时崩溃。应在 `ManagedTick` 入口添加 null check，并在 `Start` 中添加自动查找 Camera.main.transform 降级逻辑。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/AnimalSpawnManager.cs` |

### 3. 补充 Unity 项目配置文件
| 属性 | 值 |
|------|-----|
| 所属维度 | 落地性 |
| 优先级 | P0 |
| 用户影响度 | 高 |
| 实现难度 | 低 |
| 建议 | 当前项目缺少 `ProjectSettings/`、`Packages/manifest.json`、`.gitignore` 等核心文件，克隆后无法作为 Unity 项目直接打开。应创建标准 Unity 项目结构并声明 URP 包依赖。 |
| 相关文件 | 项目根目录 |

### 4. 解决文件扫描主线程阻塞
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P0 |
| 用户影响度 | 高 |
| 实现难度 | 中 |
| 建议 | `RenderAuditScanner.ScriptKeywordScan()` 同步读取所有 .cs 文件会冻结编辑器。应使用 `EditorCoroutine` 或 `Task.Run` + `EditorApplication.update` 回调实现异步扫描，并添加进度条反馈。 |
| 相关文件 | `Assets/_Tools/TA_Toolchain/Editor/RenderAuditScanner.cs` |

---

## P1 - 重要改进

### 5. 添加单元测试基础设施
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P1 |
| 用户影响度 | 中 |
| 实现难度 | 中 |
| 建议 | 项目无任何测试。应创建 `Tests/Editor/` 和 `Tests/Runtime/` 目录及 Assembly Definition，优先为 `AnimClassifier.Classify()`、`AnimClassifier.PickBest()`、`AnimalPool.Rent/Return` 添加单元测试。 |
| 相关文件 | `Assets/Editor/AnimTools/AnimClassifier.cs`, `Assets/_Game/Animals/Scripts/AnimalPool.cs` |

### 6. 完善 LOD 优化系统
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P1 |
| 用户影响度 | 高 |
| 实现难度 | 中 |
| 建议 | 当前 LOD 仅禁用 Animator，远距离动物的 Renderer 和 NavMeshAgent 仍在运行。应在 `stopAnimatorDist` 距离禁用 Renderer，在 `sleepDist` 停止 NavMeshAgent，并添加距离缓冲区防止频繁切换。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Agents/AnimalAgentBase.cs` |

### 7. 优化 FoxAgent 猎物查找性能
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P1 |
| 用户影响度 | 中 |
| 实现难度 | 中 |
| 建议 | `FoxAgent` 在 Chase 状态每 Tick 使用 `Physics.OverlapSphere` 查找猎物，大量狐狸实例时开销显著。建议由 `AnimalSpawnManager` 维护全局猎物空间索引，或为猎物查找添加冷却间隔（如每 0.5s 一次）。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Agents/FoxAgent.cs` |

### 8. 编辑器工具添加进度反馈
| 属性 | 值 |
|------|-----|
| 所属维度 | UX |
| 优先级 | P1 |
| 用户影响度 | 中 |
| 实现难度 | 低 |
| 建议 | `RenderAuditWindow` 和 `AnimAuditWindow` 的扫描操作缺少进度指示。应在扫描循环中插入 `EditorUtility.DisplayProgressBar` 调用，并在完成时弹出结果摘要对话框。 |
| 相关文件 | `Assets/_Tools/TA_Toolchain/Editor/RenderAuditWindow.cs`, `Assets/Editor/AnimTools/AnimAuditWindow.cs` |

### 9. 添加动物出生/消失视觉效果
| 属性 | 值 |
|------|-----|
| 所属维度 | UX |
| 优先级 | P1 |
| 用户影响度 | 高 |
| 实现难度 | 中 |
| 建议 | 动物目前直接弹出/消失，体验突兀。应为 `AnimalPool.Rent()` 添加缩放动画（从 0 到 1），为 `Return()` 添加渐隐或下沉效果。可使用 DOTween 或协程实现。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/AnimalPool.cs` |

### 10. 创建项目 README 和开发者文档
| 属性 | 值 |
|------|-----|
| 所属维度 | 落地性 |
| 优先级 | P1 |
| 用户影响度 | 中 |
| 实现难度 | 低 |
| 建议 | 项目完全没有文档，新维护者无法快速上手。应创建 README.md 包含项目介绍、技术栈、安装步骤、模块说明；在每个模块目录添加简要 DESIGN.md 说明设计意图。 |
| 相关文件 | 项目根目录, `Assets/_Game/Animals/`, `Assets/_Tools/` |

### 11. 为所有类添加命名空间
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P1 |
| 用户影响度 | 低 |
| 实现难度 | 低 |
| 建议 | 所有类均在全局命名空间中，大型项目中容易产生命名冲突。建议按模块组织命名空间：`Doctor.Game.Animals`、`Doctor.Tools.RenderAudit`、`Doctor.Tools.AnimTools`、`Doctor.Tools.URPSceneDoctor`。 |
| 相关文件 | 全部 24 个 .cs 文件 |

### 12. 添加 ScriptableObject 配置验证
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P1 |
| 用户影响度 | 中 |
| 实现难度 | 低 |
| 建议 | `AnimalSpeciesConfig.prefab` 和 `RenderAuditConfig.outputDir` 等关键字段无验证。应添加 `OnValidate()` 方法检查 null 引用和无效路径，在 Inspector 中给出警告提示。 |
| 相关文件 | `Assets/_Game/Animals/SO/AnimalSpeciesConfig.cs`, `Assets/_Tools/TA_Toolchain/Configs/RenderAuditConfig.cs` |

---

## P2 - 优化改进

### 13. 添加动物间交互系统
| 属性 | 值 |
|------|-----|
| 所属维度 | 功能 |
| 优先级 | P2 |
| 用户影响度 | 高 |
| 实现难度 | 高 |
| 建议 | 当前动物之间缺少可见交互（狐狸追兔子无视觉效果）。建议实现事件系统：当 Fox 进入 Attack Range 时广播事件，触发附近 Rabbit 集体逃跑动画和粒子特效。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Agents/FoxAgent.cs`, `Assets/_Game/Animals/Scripts/Agents/RabbitAgent.cs` |

### 14. 实现玩家交互机制
| 属性 | 值 |
|------|-----|
| 所属维度 | 功能 |
| 优先级 | P2 |
| 用户影响度 | 高 |
| 实现难度 | 高 |
| 建议 | 产品目前缺少任何玩家互动能力。建议分阶段实现：Phase 1 观察模式（点击查看物种信息卡）→ Phase 2 影响模式（喂食、驱赶）→ Phase 3 收集模式（图鉴系统）。 |
| 相关文件 | 需新建文件 |

### 15. 添加音效系统
| 属性 | 值 |
|------|-----|
| 所属维度 | UX |
| 优先级 | P2 |
| 用户影响度 | 中 |
| 实现难度 | 中 |
| 建议 | 完全无音效体验扁平。建议在 `AnimalAgentBase` 添加 AudioSource 组件引用，为 Idle/Flee/Attack 状态配置不同音效剪辑，并添加距离衰减。环境音可通过独立 AudioManager 管理。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Agents/AnimalAgentBase.cs` |

### 16. 提取魔法数字为配置常量
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P2 |
| 用户影响度 | 低 |
| 实现难度 | 低 |
| 建议 | `RabbitAgent` 中 `Random.Range(1f, 2.5f)` 等硬编码值应移至 `AnimalSpeciesConfig` 或提取为类常量。`ButterflyAgent` 的 Perlin 频率 `0.4f, 0.35f, 0.25f` 同理。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Agents/RabbitAgent.cs`, `Assets/_Game/Animals/Scripts/Agents/ButterflyAgent.cs`, `Assets/_Game/Animals/Scripts/Agents/CrawlerAgent.cs` |

### 17. 添加对象池重复归还保护
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P2 |
| 用户影响度 | 低 |
| 实现难度 | 低 |
| 建议 | `AnimalPool.Return()` 未检查对象是否已归还，重复归还会导致池中出现重复引用。应添加 HashSet 追踪已借出对象，归还时验证并输出警告。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/AnimalPool.cs` |

### 18. 审计结果添加跳转到对象功能
| 属性 | 值 |
|------|-----|
| 所属维度 | UX |
| 优先级 | P2 |
| 用户影响度 | 中 |
| 实现难度 | 中 |
| 建议 | 渲染审计窗口列出的问题无法直接定位到场景对象。应在问题列表中存储 `instanceID`，点击时调用 `EditorGUIUtility.PingObject()` + `Selection.activeObject` 实现跳转。 |
| 相关文件 | `Assets/_Tools/TA_Toolchain/Editor/RenderAuditWindow.cs`, `Assets/_Tools/TA_Toolchain/Editor/ReportModels.cs` |

### 19. 统一编辑器工具入口
| 属性 | 值 |
|------|-----|
| 所属维度 | UX |
| 优先级 | P2 |
| 用户影响度 | 中 |
| 实现难度 | 中 |
| 建议 | 三个编辑器工具窗口分散独立，缺少统一入口。建议创建 "Doctor Toolkit" 顶级菜单，注册所有工具窗口；或创建统一仪表盘窗口，以 Tab 形式集成所有功能。 |
| 相关文件 | `Assets/_Tools/TA_Toolchain/Editor/RenderAuditWindow.cs`, `Assets/Editor/AnimTools/AnimAuditWindow.cs`, `Assets/_Game/Animals/Editor/AnimalSystemSetupWizard.cs` |

### 20. 添加 AnimalDebugHUD 条件编译
| 属性 | 值 |
|------|-----|
| 所属维度 | 代码质量 |
| 优先级 | P2 |
| 用户影响度 | 低 |
| 实现难度 | 低 |
| 建议 | `AnimalDebugHUD` 在所有构建中都会渲染 OnGUI，正式版本中应隐藏。应添加 `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` 条件编译包裹 OnGUI 内容，或改为 Inspector 开关控制。 |
| 相关文件 | `Assets/_Game/Animals/Scripts/Debug/AnimalDebugHUD.cs` |

---

## 改进点汇总矩阵

| # | 改进点 | 维度 | 优先级 | 影响度 | 难度 |
|---|--------|------|--------|--------|------|
| 1 | 修复异常吞没 | 代码 | P0 | 高 | 低 |
| 2 | Player 空引用保护 | 代码 | P0 | 高 | 低 |
| 3 | 补充项目配置文件 | 落地性 | P0 | 高 | 低 |
| 4 | 文件扫描异步化 | 代码 | P0 | 高 | 中 |
| 5 | 添加单元测试 | 代码 | P1 | 中 | 中 |
| 6 | 完善 LOD 系统 | 代码 | P1 | 高 | 中 |
| 7 | 猎物查找性能优化 | 代码 | P1 | 中 | 中 |
| 8 | 编辑器进度反馈 | UX | P1 | 中 | 低 |
| 9 | 动物出生/消失动效 | UX | P1 | 高 | 中 |
| 10 | 项目文档 | 落地性 | P1 | 中 | 低 |
| 11 | 添加命名空间 | 代码 | P1 | 低 | 低 |
| 12 | 配置字段验证 | 代码 | P1 | 中 | 低 |
| 13 | 动物间交互 | 功能 | P2 | 高 | 高 |
| 14 | 玩家交互机制 | 功能 | P2 | 高 | 高 |
| 15 | 音效系统 | UX | P2 | 中 | 中 |
| 16 | 提取魔法数字 | 代码 | P2 | 低 | 低 |
| 17 | 池重复归还保护 | 代码 | P2 | 低 | 低 |
| 18 | 审计结果跳转 | UX | P2 | 中 | 中 |
| 19 | 统一工具入口 | UX | P2 | 中 | 中 |
| 20 | DebugHUD 条件编译 | 代码 | P2 | 低 | 低 |
