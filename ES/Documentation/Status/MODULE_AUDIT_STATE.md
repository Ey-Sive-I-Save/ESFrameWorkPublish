# ES 模块审计续接状态

本文件是项目内模块审计续接检查点的唯一固定入口。它只保存跨窗口重新定位所需的导航状态，不是模块实现权威、持续授权或 Unity/发布验收证据。

## 使用规则

- 用户说“审计”时默认只读；审计完成后，AI 最多询问一次是否记录。
- 用户说“审计并记录”时，AI 可直接更新本文件中目标模块的管理块，无需再次询问路径。
- 用户说“继续审计”时，AI 从本文件定位模块，但必须重新核对最新规则、Git、源码、激活、依赖、消费者和证据。
- 每个模块使用唯一、稳定、路径安全的 `stable-module-key`。键一经写入不得仅因目录移动或显示名变化而更换。
- 本文件中的旧结论在相关事实变化后必须视为 `stale`；不得据此直接实施、提交、运行 Unity 或发布。

## 模块块格式

在本节下方按模块追加，不要创建第二份审计状态文件：

```text
<!-- ES-AUDIT-STATE:BEGIN module=<stable-module-key> -->
### Audit continuation state

...按审计状态契约填写全部字段...
<!-- ES-AUDIT-STATE:END module=<stable-module-key> -->
```

<!-- ES-AUDIT-STATE:BEGIN module=story-non-player-quest -->
### Audit continuation state

- Snapshot ID: `story-non-player-quest-20260803-143612`
- Updated at: `2026-08-03 14:36:12 +08:00` (`Asia/Shanghai`)
- Module and committed scope: `story-non-player-quest`；审计范围仅为 NPC、阵营、队伍、聚落或世界主体独立持有 Quest 进度的底层能力。当前承诺仅是评估和冻结候选边界，不包含实现。
- Maturity state: `Proposed`
- Blocked reason: 非玩家 Quest 尚无稳定 `QuestOwnerScope / StableOwnerId / QuestRecordKey` 权威；当前 QuestRecord 和活动唯一性均按 DefinitionId 全局索引，正式启动入口要求有效 InteractionBinding，条件与 SetTag 直接作用于单一 Actor，也没有无 UI 的后台调度入口或存档迁移协议。
- Authority entry: 非玩家 Quest 当前没有已冻结的专用权威契约。相邻现行入口为 `Documentation/Story/ES_STORY_DEFINITION_RUNTIME_CONTRACT.md`、`Documentation/Story/ES_STORY_RUNTIME_SESSION_CONTRACT.md`、`Documentation/Story/ES_STORY_SAVE_EFFECT_CONTRACT.md`；源码事实入口为 `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESStoryModule.cs`、`Assets/Scripts/ESLogic/Runtime/Story/Instances/ESStoryRuntimeTypes.cs` 和 `Assets/Scripts/ESLogic/Runtime/Story/Persistence/ESStorySaveSection.cs`。
- Activation mode: `none`；没有非玩家 Quest 注册、自动初始化、后台队列或正式 API。现有 Story 入口仅通过 `ESStoryInteractable -> TryStartFromInteraction` 启动玩家交互链。
- Upstream dependencies: 现有 Story Definition/Snapshot、QuestRecord、StoryModule、ESGameSave、Entity/Tag/Interaction；进入实现前还需要稳定 Owner 身份、Owner 解析生命周期及事件/后台调度契约。
- Downstream consumers: `none`；未发现 NPC AI、Faction、Party、Settlement、World Event 或网络模块依赖非玩家 Quest 能力。
- Unfinished-code leakage: 未发现非玩家 Quest 空接口、占位注册或默认启用。现有全局 DefinitionId 唯一性会明确阻止多个主体独立持有同一 Quest，而不是静默提供半成品支持。
- Evidence present: 源码只读审计确认：`ESQuestRecord` 无 Owner 字段；`questRecords` 为 `Dictionary<string, ESQuestRecord>`；`HasActiveQuest` 只比较 DefinitionId；`TryStartFromInteraction` 要求 `binding.Owner == actor`；Condition 和 SetTag 均读取或修改 `instance.Actor.Tags`；Story Save v2 只保存 QuestRecords 与 Metadata。
- Evidence missing: 非玩家 Quest 专用契约、Owner 复合键、后台启动/推进 API、前后台实例策略、稳定 Owner 解析、Save Schema 迁移、重复复合键校验、EditMode/PlayMode/Profiler/IL2CPP/网络权威测试均不存在或未运行。
- Branch / HEAD: `main` / `33a2862571d3fd2a18562f51034524a00846c29e`
- Relevant worktree state: 审计时 Story Runtime、Story Tests 与 `Documentation/Story` 在 scoped `git status --short` 中无输出；`ES/Documentation/Status/MODULE_AUDIT_STATE.md` 为既有未跟踪文件，本次只追加当前管理块，未修改其他区域。
- Last completed action: 完成非玩家独立 Quest 支持的只读源码审计，结论为“架构可扩展、当前正式能力不存在”；未修改源码、资产、Story 契约或测试，未运行 Unity。
- Smallest next action: 先冻结 `QuestOwnerScope + StableOwnerId + QuestRecordKey`、同一 Owner 的实例策略、`QuestOwner/Initiator/Actor/Target` 职责、后台启动入口及 Save v2 到下一 Schema 的迁移规则；契约通过前不创建运行时脚手架。
- Resume read list: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`；`当前状态（CurrentStatus）.md`；`规则索引（RuleIndex）.md`；模块成熟度现行警告；`Assets/Plugins/ES/AICommands/检查_模块成熟度与半成品影响_AI命令.md`；本管理块；三份 Story 契约；`MODULE_ESStoryModule.cs`；`ESStoryRuntimeTypes.cs`；`ESStorySaveSection.cs`；当前 branch/HEAD 与上述相关工作树。
- Allowed next write scope: `none`。本检查点只记录此前讨论的设计边界，不授予未来窗口修改契约、源码、资产、测试、Git、Unity 或发布的权限；后续必须重新取得用户与匹配 AICommand 的授权。
- Invalidation triggers: Story/QuestRecord/Save DTO、StoryModule 启动与唯一性、Entity 稳定身份、Interaction/后台调度、三份 Story 契约、相关测试、AIWarnings、branch/HEAD 或证据层任一变化后，本检查点立即视为 `stale`，恢复时必须重新复核。
<!-- ES-AUDIT-STATE:END module=story-non-player-quest -->

<!-- ES-AUDIT-STATE:BEGIN module=es-graph-authoring-bake -->
### Audit continuation state

- Snapshot ID: `es-graph-authoring-bake-20260805-122901`
- Updated at: `2026-08-05 12:29:01 +08:00` (`Asia/Shanghai`)
- Module and committed scope: `es-graph-authoring-bake`；Graph V2 的稳定图资产、稳定 Node/Port/Edge 身份、端口与连线校验、Undo/Redo 编辑器投影、通用 Baked Snapshot、DomainId 与领域 Bake Guard。范围不包含任何领域 Runtime。
- Maturity state: `Implementing`
- Blocked reason: Unity Editor 域重载、Test Runner、真实窗口交互、Profiler、Player、IL2CPP 和发布证据缺失；旧 `ESGraphViewWindow` 及硬编码工具责任登记尚未完成迁移清理。本窗口已重跑 Design、Editor、Tests 隔离静态编译，均为 0 warning / 0 error，但该证据不能替代 Unity 验收。
- Authority entry: `Assets/Plugins/ES/1_Design/Graph/ESGraphAsset.cs`、`ESGraphDomain.cs`、`ESGraphSnapshot.cs`；编辑器入口为 `Assets/Plugins/ES/Editor/ESGraphViewV2/ESStableGraphViewWindow.cs`、`ESStableGraphAssetEditor.cs`、`ESStableGraphInspector.cs`、`ESGraphAuthoringProfiles.cs`。
- Activation mode: 编辑器显式打开/资产编辑入口；未发现 Player 运行时自动初始化或领域 Runtime 注册。旧 GraphView 仍存在历史入口，已加 Obsolete 但尚未证明无消费者。
- Upstream dependencies: UnityEditor/GraphView 编辑器 API、现有 ESSO/Unity 序列化、ES 设计程序集、GraphView 领域规则；不应让运行时 Story/BehaviorTree/Command 反向依赖 Editor。
- Downstream consumers: 当前为 Graph V2 编辑器、Graph Asset Tests 和未来 Story/BehaviorTree/AICommand-Skills 专用 Profile/Baker；尚未发现稳定 Player Runtime 消费者。
- Unfinished-code leakage: 新 Graph V2 未见默认运行时注册；DomainId/Profile/Bake Guard 属于编辑器或设计层。风险在于旧窗口、旧 NodeRunner 和硬编码责任登记仍可能被误选为正式入口，且通用 `payloadJson` 不能直接成为运行时权威。
- Evidence present: 源码与 `.meta` 存在；Graph 和 Agent Authoring 测试源码存在；本窗口已重跑包含 Design、Editor、Tests 的隔离静态编译，均为 0 warning / 0 error；UTF-8 Guard 已通过核心源码与记录文件。
- Evidence missing: Unity Editor/域重载、真实 GraphView 交互、Test Runner、PlayMode、Profiler、Player、IL2CPP、资源发布均未验证。已有 Test Runner 无法运行的无关错误为 `ESAssetGameCoreFlowTestDataInfo.cs:199` 调用不存在的 `ESAssetReferScene.Release()`。
- Branch / HEAD: `main` / `bc4d755e248b0e6106e2c9313bc559da88a70c28`
- Relevant worktree state: Graph V2 目录、Graph Tests、V2 Editor 目录为未跟踪新增；旧 `ESGraphViewWindow.cs` 已修改；工作树还有大量其他窗口改动，不能据此判断本模块独占差异。
- Last completed action: 完成 Agent Authoring 领域的类型化端口、Schema 锁定/修复、DAG 拓扑规则、完整思路图预设、关系烘焙、自动布局、节点主题和按需资产扫描；Design、Editor、Tests 隔离静态编译均通过。
- Smallest next action: 在 Unity Editor 中执行域重载、打开完整需求思路图预设，实测拖线拒绝、Undo/Redo、自动布局、Inspector 资产选择和大图交互，再运行 EditMode Test Runner。
- Resume read list: Start README、CurrentStatus、RuleIndex、模块成熟度规则、AICommand 审计命令、AgentSkills/AICommands 边界、GraphView 现行规则、`ESGraphAsset.cs`、`ESGraphDomain.cs`、`ESGraphSnapshot.cs`、V2 Editor 文件、Graph Tests、当前 branch/HEAD 与工作树。
- Allowed next write scope: 用户本轮已授权继续推进 Agent Authoring GraphView；允许继续修改 Graph V2 编辑器、Agent Authoring Profile/Payload/Validator/Baker/DTO、EditMode 测试与必要编辑器注册。仍禁止第二套 Graph Runtime、Command/Skill Runner、Story/BehaviorTree Runtime 和生成 `.csproj`。
- Invalidation triggers: Graph Asset/Snapshot/Domain/Profile/Baker、旧 GraphView/NodeRunner 注册、asmdef、相关测试、AIWarnings、branch/HEAD 或证据层变化后立即 `stale`。
<!-- ES-AUDIT-STATE:END module=es-graph-authoring-bake -->

<!-- ES-AUDIT-STATE:BEGIN module=es-command-skills-graph-integration -->
### Audit continuation state

- Snapshot ID: `es-command-skills-graph-integration-20260805-122901`
- Updated at: `2026-08-05 12:29:01 +08:00` (`Asia/Shanghai`)
- Module and committed scope: `es-command-skills-graph-integration`；实际职责已纠正为 `AI Artifact Generation / Agent Authoring`：Graph 表达 Goal、Reference、Constraint、AICommand/Agent Skill Output 与 Validation，烘焙强类型 `ESAgentArtifactGenerationSpec` 并生成可审查 AI Prompt/Candidate，不负责运行时执行。
- Maturity state: `Implementing`
- Blocked reason: 专用 Profile、Payload Inspector、Validator、Baker、强类型 Spec、完整关系、中文往返测试、Codex 发送、Candidate/Diff/Approve 工作流和纵向测试源码已存在；仍缺 Unity 域重载、真实 GraphView 交互、EditMode Test Runner、真实 Codex 候选生成、Diff/Review/人工批准端到端证据。
- Authority entry: Graph 通用入口为 `Assets/Plugins/ES/1_Design/Graph/ESGraphDomain.cs`；Command 运行时权威为 `Assets/Plugins/ES/0_Stand/BaseDefine_Command/ABSTRACT_ESCommand.cs`、`Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs`、`SERVICE_ESCommandPlayerRunner.cs`；Skill 定义权威为 `SkillDefinitionDataInfo.cs`、`ESSkillConfigKeyData.cs`、`SkillDefinitionDataGroup.cs`。
- Activation mode: 仅编辑器显式创建/打开 Agent Authoring Graph 后激活；资产扫描由用户选择或刷新触发并缓存。没有 Player 自动初始化、运行时注册、Graph Runner 或 Command/Skill Runtime 注入。
- Upstream dependencies: Graph V2 Asset/Snapshot/DomainId、现有 TypeRegistry/ESCommand 类型发现、Skill ConfigKey/GameCore Table、编辑器 tooling 规则。
- Downstream consumers: 当前为编辑器中的 `ESCmdAgentWindow.OpenAndSendPrompt`、候选目录和 Candidate Review；它们只生成/审查作者产物，不进入 Player。未来若接入游戏领域，只能交给既有稳定 Command/Skill 入口，不能创建第二套 Runner。
- Unfinished-code leakage: 已有 Agent Authoring Graph 源码，但无默认运行时注册或 Player 注入。风险仍是未来把 `payloadJson` 或 CLR 类型名直接带入 Player、重复注册 Command Runner，或把 RuntimeKey 写入内容。
- Evidence present: Agent Authoring 的专用 Profile、Payload Inspector、Validator、Baker、强类型 Spec、中文往返测试、Codex 发送、Candidate/Diff/Approve 源码存在；静态编译与 UTF-8 Guard 已通过。现有 ESCommand 同步/跨帧边界和唯一 `ESCommandPlayerRunner.TickAll()` 驱动约束有源码与 `ESCommand_STANDARD.md`；Skill 有稳定 `ESSkillConfigKey` 和 GameCore Table。
- Evidence missing: EditMode Test Runner、Unity 域重载、真实 GraphView 交互、真实 Codex 候选生成、Diff/Review/人工批准端到端、PlayMode、Profiler、Player、IL2CPP 和真实 Command/Skill 场景均未验证。
- Branch / HEAD: `main` / `bc4d755e248b0e6106e2c9313bc559da88a70c28`
- Relevant worktree state: 尚未发现 `Runtime/AI/CommandSkills` 或 `Editor/AI/CommandSkills` 原型目录；相关既有 ESCommand/Skill 文件为当前仓库事实，工作树存在其他窗口改动。
- Last completed action: 完成 Agent Authoring 完整需求思路图：10 节点/16 关系预设、Context/Requirement/Artifact 类型化端口、Payload/Inspector、Topology/Schema 校验、强类型 Spec、AI 可读 Prompt 与 Mermaid、Codex Candidate/Diff/Approve 工作流，以及静态编译和 UTF-8 检查。
- Smallest next action: 在 Unity 中创建 `Assets/Create/【ES】/图与流程/Agent Authoring/完整需求思路图`，验证资产默认保存到 `Assets/ESNormalAssets/Data/AgentAuthoring/Graphs`，再跑 EditMode 测试和真实 Codex 候选生成/批准流程。
- Resume read list: Start README、CurrentStatus、RuleIndex、模块成熟度规则、AICommand 审计命令、AgentSkills/AICommands 边界、ESCommand 标准、GraphView 规则、上述 Command/Skill 源码与当前工作树。
- Allowed next write scope: 用户已授权继续实现 Agent Authoring Graph 作者工具、候选生成和审查流程；不授权修改 Command/Skill Runtime 权威或新增 Runner。
- Invalidation triggers: Graph Domain/Profile、ESCommand Player/Runner、Skill ConfigKey/Table、编辑器注册、相关测试、AIWarnings、branch/HEAD 或证据层变化后立即 `stale`。
<!-- ES-AUDIT-STATE:END module=es-command-skills-graph-integration -->

<!-- ES-AUDIT-STATE:BEGIN module=es-command-runtime -->
### Audit continuation state

- Snapshot ID: `es-command-runtime-20260804-222731`
- Updated at: `2026-08-04 22:27:31 +08:00` (`Asia/Shanghai`)
- Module and committed scope: `es-command-runtime`；现有 ESCommand、ESCommandPlayer、IESCommandPlayable、PlayerRunner 以及 MODULE_ESCommandModule 驱动边界。
- Maturity state: `Integrating`
- Blocked reason: 本窗口未执行编译或 Unity 运行证据；取消、失败、跨帧和退出路径虽有实现，但尚未用完整场景和性能证据确认。
- Authority entry: `ABSTRACT_ESCommand.cs`、`ESCommandPlayer.cs`、`SERVICE_ESCommandPlayerRunner.cs`、`INTER_IESCommandPlayable.cs`、`ESCommand_STANDARD.md`。
- Activation mode: 由现有 ESCommandModule/Player 显式注册；`TickAll()` 不得由 Story、Graph 或其他模块直接驱动。
- Upstream dependencies: ESCommand 定义、Time/Unity 生命周期、MODULE_ESCommandModule。
- Downstream consumers: 现有 Command 组件与未来 AICommand/Skills 适配器；不得新增并行 Runner。
- Unfinished-code leakage: 未发现 Graph 或 Story 直接驱动 Runner；旧命令和 RuntimeMode 命令需继续按现行边界审计。
- Evidence present: 源码和标准文档存在；Runner 具有 Register/Unregister/TickAll/TickPlayerNow/Clear；本窗口未重新编译。
- Evidence missing: Unity 域重载、Test Runner、PlayMode、Profiler、Player、IL2CPP、网络和正式发布证据。
- Branch / HEAD: `main` / `bc4d755e248b0e6106e2c9313bc559da88a70c28`
- Relevant worktree state: 相关运行时文件未由本窗口修改；工作树存在其他未提交变更。
- Last completed action: 只读核对 Runner 和 Player 入口，确认 Command/Skill 接入必须复用现有执行边界。
- Smallest next action: 在独立编译和场景测试授权下验证 MODULE 驱动、跨帧取消/失败/退出路径；在此之前不改 Runner。
- Resume read list: ESCommand 标准、Player、Runner、MODULE_ESCommandModule、相关命令、生命周期规则和当前工作树。
- Allowed next write scope: `none`。
- Invalidation triggers: Runner、Player、Module、命令协议、asmdef、测试或证据层变化后立即 `stale`。
<!-- ES-AUDIT-STATE:END module=es-command-runtime -->

<!-- ES-AUDIT-STATE:BEGIN module=es-skill-definition-runtime -->
### Audit continuation state

- Snapshot ID: `es-skill-definition-runtime-20260804-222731`
- Updated at: `2026-08-04 22:27:31 +08:00` (`Asia/Shanghai`)
- Module and committed scope: `es-skill-definition-runtime`；SkillDefinitionDataInfo、ESSkillConfigKey、ESSkillRuntimeData、ESSkillConfigKeyTable、SkillDefinitionDataGroup 及现有 Skill Runtime 消费边界。
- Maturity state: `Integrating`
- Blocked reason: 内容发布、热更新/重建、跨会话恢复、网络权威、Unity/PlayMode/Profiler/IL2CPP 证据缺失；Skill Graph 接入尚未存在。
- Authority entry: `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/SkillDefinitionDataInfo.cs`、`Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Skill/ESSkillConfigKeyData.cs`、`Assets/Scripts/ESLogic/Runtime/Data/For_Info/GroupType/SkillDefinitionDataGroup.cs`。
- Activation mode: 通过现有 GameCore Skill Table 注入和 Skill Runtime 显式消费；本窗口未发现 Command/Skill Graph 自动注入。
- Upstream dependencies: SoDataInfo/Group、GameCore ConfigKey/Table、TrackProcess/State/Tag/Expression 等既有 Skill 领域配置。
- Downstream consumers: `EntityState_Skill`、SkillSequence Runtime Cache、Track/Operation 相关运行时；具体消费者需在实现前再次枚举。
- Unfinished-code leakage: 未发现 RuntimeKey 持久化入口；风险是编辑器或 Graph 直接保存 RuntimeKey、重复维护 Skill 定义或让 Graph 反向成为 Skill Runtime 权威。
- Evidence present: 稳定 ConfigKey、GameCore 注入、RuntimeData Release 逻辑和 SkillDefinition Group 源码存在；本窗口未重新编译。
- Evidence missing: Skill 发布/资源重建、迁移、跨会话/网络、Unity Test Runner、PlayMode、Profiler、Player、IL2CPP 证据。
- Branch / HEAD: `main` / `bc4d755e248b0e6106e2c9313bc559da88a70c28`
- Relevant worktree state: 相关 Skill 源码未由本窗口修改；工作树存在其他窗口改动。
- Last completed action: 只读核对 Skill 稳定身份和 GameCore 入口，确认首接入必须使用 `ESSkillConfigKey` 而不是 RuntimeKey。
- Smallest next action: 在实现授权下设计最小 Skill Graph Payload 到 ConfigKey 的校验/烘焙适配，并增加稳定身份测试；不创建第二套 Skill Runtime。
- Resume read list: SkillDefinitionDataInfo、ESSkillConfigKeyData、SkillDefinitionDataGroup、Skill Runtime/Track/Operation 规则、Graph 规则、生命周期规则和当前工作树。
- Allowed next write scope: `none`。
- Invalidation triggers: Skill Definition/ConfigKey/Table/Runtime、Graph 接入、相关测试、AIWarnings、branch/HEAD 或证据层变化后立即 `stale`。
<!-- ES-AUDIT-STATE:END module=es-skill-definition-runtime -->
