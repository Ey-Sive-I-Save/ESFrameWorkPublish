---
name: es-game-core-loop-validation
description: Validate an ESFramework game core loop across structure, implementation, presentation, and performance with separated evidence gates and ABCD/ABCC orchestration. Use when checking playable-loop readiness, runtime acceptance, regression evidence, or designing a bounded core-loop validation plan.
---

# ES 游戏核心循环验证

本 Skill 将核心循环验证拆成四个独立层级，并通过 `ABCD.Dynamic + ABCC.Core` 统一编排。它是工程级验证工作流，不把静态文件、Unity 回执、PlayMode、Profiler 或 Player 证据互相替代。

状态生命周期使用 `state-machine.binding.json` 的十阶段投影，实际任务状态唯一归 `TaskContextRuntime`，验证阶段唯一归 `ESABCInnovationRun`；多 Agent 只产生子任务回执，不建立第二套状态机。状态转移、冲突回退、迟到结果隔离和最终决策门禁见 `references/state-machine.md`。

本 Skill 复用 ES 的跨进程 CAS、平台证据归一化和 ABCD Authority 入口，具体路径登记在 `integration.binding.json`。上游夹具或合同缺失必须保留为对象级 `unverifiable`/`stale`，不得替换来源、跳过验证或压平为 Skill 通过。

TaskContext Evaluation Worker 只能产出候选评估；`scripts/Convert-ESGameCoreLoopWorkerEvidence.ps1` 将其 `result.json` 与 `EvaluationRecord` 在任务、范围和输入快照哈希一致时转换为本 Skill 的 EvidenceReceipt，绝不携带完成裁决。转换失败、任务不一致或 Worker 注入完成字段时，该层保持 `unverifiable`，最终裁决仍归 `ABCD-final-decision`。

Worker 子进程回执必须同时具备 `attempt`、`leaseId`、`processId`、`processStartedUtc`、`processExitCode`；迟到或取消结果按 lease/process 隔离。稳定证据入口 `scripts/Test-ESSkillEvidence.ps1` 委托中央 Strict Evidence Receipt 验证器，只有中央合同、项目内 sourceRefs 与实时哈希均通过，才可把候选回执纳入 Evidence Join。

状态到 TaskContextRuntime 的操作映射登记在 `execution.binding.json`。默认仅生成计划和读取状态；提交证据、完成任务、交付接受、Unity/PlayMode/Profiler/Release 均要求当前用户显式授权，并且必须携带期望版本与幂等键。

## 权威与边界

- 每次运行必须先读取 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`Documentation/AIKnowledge/KnowledgeIndex.yaml`，再按绑定读取 `Documentation/AIKnowledge/entries/aiwarning-p0-playable-loop-runtime-evidence.md`；同时完整读取 AIWarnings Start、CurrentStatus、RuleIndex 和 P0 实际可玩闭环原文。不得以摘要或上次回执替代本轮来源检查。
- 本 Skill 的 Knowledge 绑定要求：KnowledgeId `es.aiwarning.p0.playable-loop-runtime-evidence.v1`、routeKeys 至少命中 `playable-loop`、`runtime-evidence`、`playmode`、`profiler`，且 `relatedSkills` 必须包含本 Skill。SourceRef/ContentHash 漂移时立即标记 `stale` 并回读权威原文。
- 涉及 Buff/Modifier/属性变化时，额外读取并绑定 `aiwarning-runtime-buff-passive-lifecycle-boundary.md` 与 `aiwarning-runtime-attribute-valuechange-boundary.md`：实现层检查 Buff 生命周期所有权、触发边界、Lease/Generation；表现层检查 Tag/ValueChange 反馈一致性；性能层检查 Tick 分配、池复用与重入安全。不得把 Buff 轮询、输入、施放或全局扫描当作核心循环职责。
- 使用 `ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json`、`es-ai-abc-core-v1.json`、`es-ai-abc-innovation-run-v1.schema.json` 与 `ES/Automation/ABCD/ESABCInnovationRun.psm1`；不得用报告模板冒充 ABCD 执行。
- 默认只读。Unity、PlayMode、Profiler、Player、发布、Git 和写入动作必须由当前用户单独授权。
- `runtime-not-run` 是证据缺失，不是静态失败；它阻断需要 Runtime 的验收层，但不阻断结构层静态判断。

## 四层验证模型

### 1. 结构验证（S0-S1）

确认唯一权威和闭环拓扑：输入意图 → Request/State/Command → 唯一执行入口 → 业务结果 → 动画/IK/VFX/音频/UI 反馈 → 可观察终态 → 清理。检查类型、接口、注册、配置、场景入口、失败码、取消/打断/超时/回池契约及 SourceHash。只有声明存在，不证明已运行。

### 2. 实现验证（S1-S2）

在用户授权的范围内验证编译、域重载、EditMode/单元测试和状态机转移。覆盖成功、失败、取消、打断、超时、拒绝、禁用、重入、资源释放和重复执行。每条结果绑定入口、环境、退出状态、产物和哈希；失败走 ABCC `failure-recovery`，不得重试越界。

### 3. 表现验证（S3）

验证输入到结果的可感知反馈：控制权、相机/移动响应、动画过渡、IK、VFX、音频、UI、失败提示和重置。必须使用当前场景与 PlayMode 观察回执，记录设备/焦点切换、抖动/同时输入及首次/重复使用；旧截图或日志不能替代当前操作证据。

### 4. 性能验证（S4-S6）

先建立目标平台和输入规模的基线，再测首帧/稳态/峰值、CPU、GC、内存、延迟、加载、并发和资源压力。使用 Profiler/Player/IL2CPP 或发布回执；没有实测只能输出预算与缺口，不得声称 0 GC、商业级性能或发布就绪。

## ABCD/ABCC 工作流

1. 冻结 `GoalRevision`、授权、Unity 环境、Branch/HEAD、工作树和源集合哈希。
2. 生成 A Intent，声明四层范围、验收层级、证据预期和禁止扩展项。
3. 协商 B 能力，必须检查六项 ABCD parity：`bounded-tool-action`、`failure-recovery`、`branch-evaluation`、`state-transition-guard`、`environment-trust-gate`、`audit-evidence-chain`。
4. 运行 task-scoped InnovationRun：`requirement-facts`、`player-outcomes`、`lexical-deanchor`、`seed-divergence`、`tree-expansion`、`global-convergence`、`interaction-graph`、`adaptive-weighting`、`player-replay`、`counterplay-audit`、`complexity-prune`、`candidate-tournament`、`final-decision`。每阶段必须有非空证据、预算使用和可重放状态转换。
5. 按层级执行验证；结构证据先行，Runtime 证据按授权逐层升级。任何哈希/工作树漂移都标记 `stale` 并重新采证。
6. 输出 `aligned / partial / unverifiable / misaligned`，同时列出 `claimsNotProven`、阻断、失败码、回滚/恢复动作和下一步。

## 证据矩阵与门禁

每个验证项至少包含：`taskId`、`layer`、`object`、`precondition`、`owner`、`entryPoint`、`expected`、`observed`、`status`、`evidenceRef`、`sourceHash`、`runtimeStatus`、`nonClaims`。四层分别设置独立通过条件；不得用总体绿色掩盖缺失层。

静态回放必须覆盖：正常输入、非法输入、拒绝扩权、重复幂等、哈希变化失效、中断恢复、确定性输出。Runtime 行必须明确授权、平台、超时、停止条件和回执位置。

## 硬性真实验证步骤

以下步骤是验收门禁，不得用“文件存在”替代：

1. 结构：锁定场景/对象/入口清单，逐项证明输入到终态及清理路径；缺唯一消费者、失败码或回池重置即结构失败。
2. 实现：在授权的 Unity 实例中执行导入/域重载、编译和 EditMode/PlayMode 测试；记录命令、测试名、退出码、日志路径、环境指纹和产物哈希。任何必需测试未执行即 `Blocked`。
3. 表现：在目标场景真实操作成功、失败、取消、打断、超时、重入和焦点切换；记录输入设备、步骤时间线、可观察反馈和当前截图/录像引用。静态代码和旧截图不得通过此层。
4. 性能：在声明平台和固定输入规模下采集首帧、稳态、峰值、CPU、GC、内存、延迟和并发数据；至少一次基线与一次回放对比。无 Profiler/Player 回执只能输出预算，不能判定性能通过。

每一步都必须产生可重读 receipt；receipt 缺入口、环境、退出状态、失败项或哈希时，该层失败并沿 ABCC `audit-evidence-chain` 降级，不得继续升级结论。

## 交付格式

报告顺序：目标与范围 → 已验证事实 → 四层矩阵 → ABCD/ABCC 回执 → 失败/阻断与恢复 → 未证实项 → 分层结论 → 最小下一步。禁止声称 Unity/PlayMode/Profiler/Player/Release 已通过，除非存在对应新鲜回执。

## Engineering controls

- 采用 Engineering tier；要求风险边界、静态回放、证据合同、可恢复执行和分层验收。
- 不自动启动 Unity、Player、Profiler、网络、Git 或发布流程；这些动作必须由当前用户单独授权。
- 记录身份、权限、输入规模、平台、超时、取消、失败恢复和证据保留策略。

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。实际使用本 Skill 时，首次进度更新和最终答复必须说明其职责；披露不等于授权、执行或验收证据。
