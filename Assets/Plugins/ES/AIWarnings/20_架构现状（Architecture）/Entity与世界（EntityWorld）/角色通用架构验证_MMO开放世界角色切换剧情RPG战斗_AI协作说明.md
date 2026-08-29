# 角色通用架构验证：MMO / 开放世界 / 角色切换 / 剧情 / RPG 战斗

Status: current
StableId: es.aiwarnings.architecture.character-general-validation.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, architecture, entity, character, control-authority, mmo, rpg, acceptance
Applicability: Entity 角色模型、输入/AI/剧情/网络控制、装备与战斗扩展
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-character-general-architecture-validation.md`
StaleWhen: Entity/Identity、AIDomain、RuntimeMode、Equipment/Combat 或 SourceRefs 变化
Knowledge: `es.aiwarning.character-general-architecture-validation.v1`

## 长期约束

- 统一角色入口是 `Entity + EntityCharacterIdentity -> Entity.BindDefinition(DataInfo)` 与 Basic/AI/Buff/Equipment/State 五域；禁止新增 CharacterActor、PlayerActor、第二套控制器或恢复四域旧事实。
- 角色控制权只有一条执行链：Input/AI/Network/Cutscene/Replay 经来源授权和请求写入 `EntityAIDomain`，再进入正式 Domain/Module；不得直接写 KCC、Transform、Combat 或 StateMachine，也不得各自持有常驻 Controller。
- Entity 负责身体与生命周期，Basic/KCC 负责运动，Equipment 负责装备事实，Buff 负责效果/限制，State/IK 负责状态与表现；存档、网络、世界注册使用各自稳定实例合同，不塞进 KCC。
- 角色切换、剧情接管和网络同步必须有代际控制令牌、抢占/恢复和 RuntimeMode 门禁；切换要释放旧角色控制权、授予新角色并刷新 Camera/UI/Combat 上下文，不能只改输入引用或 Main Camera target。
- MMO/开放世界查询必须由正式 World/Registry 模块提供稳定 instanceId/configId/team/区域索引；禁止技能、剧情、UI 通过 `FindObjectsOfType<Entity>()` 自建扫描。
- Prefab 基础能力必须显式保存并经制作期/发布门禁复验；不得运行时自动补齐输入、移动、Mount/Climb 或战斗组件，也不得由模板扩张未授权能力。
- RPG 战斗沿 `EntityAIDomain -> Skill/Attack intent -> Equipment/Combat -> State/Buff/TargetPack`；BuffDomain、EquipmentDomain 和 Operation 各自保持权威，不复制第二套属性、目标或效果系统。
- 当前结论只冻结架构边界；多实体 LOD、流式场景、切换、剧情恢复、网络预测、完整战斗、池重置、存档/网络恢复和性能仍需分层运行证据，静态/编辑器结果不得写成可玩或发布。

## 证据边界

详细压力测试矩阵、当前事实、历史纠偏、World Registry/Party/Cutscene/RPG 设计、模块显式接线和未完成项已迁移至 Knowledge；执行前仍须回读当前源码。Unity/PlayMode/Profiler/Player/IL2CPP/发布均未由本 Warning 静态检查证明。
