# Documentation 分类总表

状态：现行分类。最后核对：2026-08-07。

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
- [ES_BUFF_RUNTIME_STANDARD.md](ES_BUFF_RUNTIME_STANDARD.md)：Buff 对 GameCore、Tag、ValueChange、Op、Link 与 Pool 的统一边界；Unity Test Runner 与联机/存档验收仍待完成。
- [ES_STAT_RUNTIME_STANDARD.md](ES_STAT_RUNTIME_STANDARD.md)：Float/Permit Stat 的定义、聚合、Lease、Hot/Sparse 读取、调试快照与性能边界；Unity Test Runner、Player/IL2CPP 与 Profiler 待验收。
- [CHARACTER_PREFAB_CONTRACT.md](CHARACTER_PREFAB_CONTRACT.md)：角色 Prefab 身份、组件边界和验收规则。
- [VEHICLE_RUNTIME_CONTRACT.md](VEHICLE_RUNTIME_CONTRACT.md)：载具后端、运动调度与骑乘输入转交边界；全项目 Unity 编译与 PlayMode 仍待验收。
- [ES_CAMERA_RUNTIME_STANDARD.md](ES_CAMERA_RUNTIME_STANDARD.md)：CameraDirector、View/Lease、内容 Catalog、CM2 唯一执行边界与当前验收状态。
- [ES_SCENE_VALIDATION_GUIDE_STANDARD.md](ES_SCENE_VALIDATION_GUIDE_STANDARD.md)：测试场景导视、诊断、路线与输入说明的复用边界；Unity / PlayMode / Profiler 验收待完成。
- [SKILL_OPERATION_LIFECYCLE.md](SKILL_OPERATION_LIFECYCLE.md)：Skill Operation 默认无 Stop、按需清理及运行时所有权规则。
- [ES_EDITOR_TOOL_WORKBENCH_STANDARD.md](ES_EDITOR_TOOL_WORKBENCH_STANDARD.md)：SimpleTools 的目录分组、页面状态、配置目录、按钮层级、安全与迁移验收规范；每个工具迁移仍须补 Unity 视觉和真实操作验证。
- [../ES/Documentation/Guides/ESWorkbench_ContributionAndModuleGuide.md](../ES/Documentation/Guides/ESWorkbench_ContributionAndModuleGuide.md)：ES 工作台贡献注册、模块枚举模板、调整钩子、依赖/冲突诊断与窗口注入指引；当前为源码级实现指引，Unity 实机编译和交互验收待完成。
- [ES_INSTALLER_SIGNED_UNITYPACKAGE_STANDARD.md](ES_INSTALLER_SIGNED_UNITYPACKAGE_STANDARD.md)：`Assets/Plugins/ES + .unitypackage + 旧 ESInstaller` 唯一安装发布主链、AI 快速升级步骤、签名门禁与当前恢复缺口。

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
- [FuturePlans/ES_ITEM_DOMAIN_DESIGN.md](FuturePlans/ES_ITEM_DOMAIN_DESIGN.md)：`Proposed`；物品实例、Container、装备、拾取、存档与 ES 聚合方案，尚未实施。

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

- `ES_RESOURCE_SYSTEM_PRODUCT_BRIEF_DRAFT.md`：资源系统证据分级宣传草案；当前仅允许宣传源码级边界，Unity/Player/真实发布证据完成前不得升级为商业生产结论。
- `ASSET_COLLECTION_GUIDE.md`
- `ESASSETREFER_GUIDE.md`
- `ESASSETREFER_README.md`
- `ESASSETREFER_VS_ADDRESSABLES_COMPARISON.md`
- `ES_REFCOUNT_USAGE_GUIDE.md`
- `LOADTYPE_EXTENSION_GUIDE.md`
- `LOADTYPE_README.md`
- `SHADER_AUTO_WARMUP_GUIDE.md`
- `用户文档/PhysicsAlign_CommercialFeatures.md`

### UI 与动态图集

- `ES_UI_AUTHORING_WORKFLOW.md`：UI 组件选择、资源/输入/生命周期权威链、动态图集最短用法和 UI 风险专项计划；已通过 Unity BatchMode 脚本编译与 EditMode 10/10，仍待 PlayMode、Frame Debugger、Profiler、Player/IL2CPP 与目标平台复验。

### AI 测试与自动化

- [ESAITEST_IMPLEMENTATION_OVERVIEW.md](ESAITEST_IMPLEMENTATION_OVERVIEW.md)：ESAITest 当前已实现能力、优势、创新点、完成度和使用方式的源码级总结；Unity PlayMode、Profiler、Player 与 IL2CPP 结论仍须以当前运行证据复验。

### 运动与其他运行时主题

- `MATCHTARGET_USAGE_GUIDE.md`
- `SPECIAL_MOVEMENT_MODES.md`
- `WALK_RUN_LOCOMOTION_SYSTEM.md`

## 维护门禁

新增或更新文档时，首屏必须写：`状态`、`最后验证`、`适用源码入口`。涉及性能必须写明 Profiler/Player 证据；涉及完成度必须区分“代码存在”“程序集编译”“Unity Test Runner”“Player/IL2CPP”。

除 `历史归档` 和 `未来方案` 外，任何文档若连续两个版本没有验证记录，应自动降级到 `待源码复验`。
