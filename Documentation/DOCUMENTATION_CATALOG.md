# Documentation 分类总表

状态：现行分类。最后核对：2026-07-31。

本文是 `Documentation` 的唯一阅读入口。它不替代源码、`AIWarnings` 或 Unity 验证；分类的目标是避免历史资料、提案和生成报告被误用为现行架构规则。

## 分类规则

| 类别 | 可否直接作为实现依据 | 规则 |
| --- | --- | --- |
| `现行规范` | 可以 | 必须与源码、AIWarnings 和验证证据同步维护 |
| `生成证据` | 仅限生成时刻 | 必须标明生成时间、输入 Schema 和失效条件 |
| `实施记录` | 仅供迁移背景 | 完成后不替代正式规范 |
| `未来方案` | 不可以 | 未经批准不得据此改生产代码 |
| `历史归档` | 不可以 | 只保留链接和决策背景 |
| `待源码复验` | 不可以 | API、性能数字、完成状态均须以当前源码和 Unity 验证复核 |

## 现行规范

- [KEY_GOVERNANCE.md](KEY_GOVERNANCE.md)：稳定 Key、Catalog、RuntimeKey、Tag 与属性的项目级规则。
- [ESTAG_FULL_LIFECYCLE_STANDARD.md](ESTAG_FULL_LIFECYCLE_STANDARD.md)：ESTag 当前全流程标准；其中明确标出的 Unity Bake/Test Runner 项仍待验收。
- [ES_GENERIC_LIFE.md](ES_GENERIC_LIFE.md)：根对象生命周期组织器及其 Pool 分部；Unity Test Runner 仍待验收。

修改这些主题时，必须同时检查 `Assets/Plugins/ES/AIWarnings` 的对应 P0 规则。

## 生成证据

- [KEY_AUDIT_REPORT.md](KEY_AUDIT_REPORT.md)：稳定 Key 审计输出。当前文件明确早于 ESTag BakeTable v6，不能作为当前 Tag Catalog 证据；必须重新执行 Unity 审计生成。

## 实施记录与迁移候选

- [TAG_IDENTITY_STORAGE_RECTIFICATION_PROPOSAL.md](TAG_IDENTITY_STORAGE_RECTIFICATION_PROPOSAL.md)：GameTag 身份与存储策略整改记录。实现规则已经收口到 `ESTAG_FULL_LIFECYCLE_STANDARD.md` 与 `KEY_GOVERNANCE.md`；本文件只保留迁移原因和验收背景。
- [ASSET_LIBRARY_DEPLOY_MODE_PROPOSAL.md](ASSET_LIBRARY_DEPLOY_MODE_PROPOSAL.md)：资产库分发提案。尚非现行运行时契约；后续应迁入 `FuturePlans` 或在批准后转为正式规范。

## 未来方案

- [FuturePlans/README.md](FuturePlans/README.md)：未来方案的状态定义和使用边界。
- [FuturePlans/ASSET_RUNTIME_GENERATION.md](FuturePlans/ASSET_RUNTIME_GENERATION.md)：`Reserved`。
- [FuturePlans/ESFRAMEWORK_RESPONSIBILITY_CONSOLIDATION.md](FuturePlans/ESFRAMEWORK_RESPONSIBILITY_CONSOLIDATION.md)：`Reserved`。

## 历史归档

以下文档属于已经废止的 ESVMCP 命令系统。对应源码位于 `Assets/Plugins/ES/Obsolete/ESVMCP`，不得作为 ESFramework 当前功能、API 或 AI 工作流依据。

- [README.md](README.md)
- [COMMAND_LIST.md](COMMAND_LIST.md)
- [MEMORY_USAGE.md](MEMORY_USAGE.md)

这些文件暂不物理移动，以保留既有链接；文件顶部已经标记为历史资料。

## 待源码复验的技术资料

这些资料可能仍有可复用的设计和示例，但没有统一的状态、负责人或近期 Unity 验证记录。它们不能直接声明“已完成”“商业级”或作为改生产代码的唯一依据。

### State、Playable 与动画

- `2D_BLEND_TREE_USAGE_GUIDE.md`
- `ANIMATION_BLEND_MECHANISMS_GUIDE.md`
- `ANIMATION_EVENTS_QUICKSTART.md`
- `BLEND_TREE_2D_DIRECTIONAL_3D_MOVEMENT_GUIDE.md`
- `CALCULATOR_UNIFIED_INITIALIZATION_GUIDE.md`
- `PLAYABLE_HOTPLUG_PERFORMANCE.md`
- `PLAYABLE_STATE_MACHINE_ARCHITECTURE.md`
- `PLAYABLE_STATE_MACHINE_GUIDE.md`
- `PLAYABLE_STATE_MACHINE_README.md`
- `STATE_COST_AND_PLAYABLE_GUIDE.md`
- `STATE_REUSE_AND_CUSTOM_KEYS_GUIDE.md`
- `STATE_RUNTIME_MECHANISM.md`
- `STATE_SHARED_DATA_USAGE_GUIDE.md`
- `STATE_TRANSITION_SYSTEM_GUIDE.md`

### 资源、加载与编辑器工具

- `ASSET_COLLECTION_GUIDE.md`
- `ESASSETREFER_GUIDE.md`
- `ESASSETREFER_README.md`
- `ESASSETREFER_VS_ADDRESSABLES_COMPARISON.md`
- `ES_REFCOUNT_USAGE_GUIDE.md`
- `LOADTYPE_EXTENSION_GUIDE.md`
- `LOADTYPE_README.md`
- `SHADER_AUTO_WARMUP_GUIDE.md`
- `用户文档/PhysicsAlign_CommercialFeatures.md`

### 运动与其他运行时主题

- `MATCHTARGET_USAGE_GUIDE.md`
- `SPECIAL_MOVEMENT_MODES.md`
- `WALK_RUN_LOCOMOTION_SYSTEM.md`

## 维护门禁

新增或更新文档时，首屏必须写：`状态`、`最后验证`、`适用源码入口`。涉及性能必须写明 Profiler/Player 证据；涉及完成度必须区分“代码存在”“程序集编译”“Unity Test Runner”“Player/IL2CPP”。

除 `历史归档` 和 `未来方案` 外，任何文档若连续两个版本没有验证记录，应自动降级到 `待源码复验`。
