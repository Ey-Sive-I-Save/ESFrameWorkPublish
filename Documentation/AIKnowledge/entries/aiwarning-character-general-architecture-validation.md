# 角色通用架构、控制权与 RPG 扩展边界

`KnowledgeId`: `es.aiwarning.character-general-architecture-validation.v1`  
`Authority`: `AIWarnings + current Entity/Identity/AIDomain source`  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `character`, `control-authority`, `mmo`, `rpg`, `acceptance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `487e611de821b6cbba9b09b73af1a3745cc0d74785cbe36e62bd0e3724ea5e01`  
`SourceSetHash`: `487e611de821b6cbba9b09b73af1a3745cc0d74785cbe36e62bd0e3724ea5e01`  
`EntryBodyHash`: `69ed1c79fc2ebd5f5e2e788bb83e7a9172677dd107bc7e17e11d7432f5c97652`  
`StaleWhen`: `Entity/Identity、AIDomain、RuntimeMode、Equipment/Combat 或 SourceRefs 变化。`

## 迁移说明

原 Warning 328 行、18,947 UTF-8 字节；现 Warning 仅保留角色模型、控制权、权限和证据边界。MMO/开放世界/切换/剧情/RPG 的详细架构与压力测试矩阵迁移到本条目，原文和源码仍可由 SourceRefs 回溯。

## 统一角色模型

- 角色入口固定为 `Entity + EntityCharacterIdentity -> Entity.BindDefinition(DataInfo)` 与 Basic、AI、Buff、Equipment、State 五域；不新增 CharacterActor、PlayerActor、EntityCharacterComposition 或第二套控制器。
- Entity/GenericLife 负责身体与生命周期；Basic/KCC 负责运动；AIDomain 负责控制请求；Equipment 负责装备事实；Buff 负责效果/限制；StateMachine/IK 负责状态和表现。稳定实例、世界注册、存档和网络身份由各自正式合同承载。
- Prefab 的基础能力必须显式保存并由制作期审计、发布门禁复验；运行时不得自动补移动/输入/Mount/Climb/战斗组件。正式 Player Variant 才能按契约保存玩家输入与玩家专属模块。

## 控制权与世界

- Input、AI、Network、Cutscene、Replay 先经来源授权/优先级、代际令牌与 RuntimeMode 门禁，写入 `EntityAIDomain`，再复用同一正式执行入口；禁止直接写 KCC、Transform、Combat、StateMachine 或各建常驻 Controller。
- 角色切换必须释放旧角色控制权、授予新角色、绑定 Camera Follow/Aim、刷新 UI/Target/CombatContext，并保留后台状态；剧情接管需可恢复、可取消且不绕过 Motion/State API。
- MMO/开放世界的注册/注销、instanceId/configId/team/距离/区域查询应由正式 World/Registry 模块承担；技能、剧情、UI 禁止 `FindObjectsOfType<Entity>()` 扫描。LOD、流式加载、回收和存档恢复必须绑定实例代际。

## RPG 与证据

- 战斗链为 `EntityAIDomain -> Attack/Skill intent -> Equipment/Combat -> State/Buff/TargetPack`。SkillDefinition、Operation、BuffDomain、EquipmentDomain 保持单一事实与生命周期，不复制属性、目标、效果或资源系统。
- 需要逐项验证多实体、LOD、切换、剧情恢复、网络预测/回滚、Skill/Buff/Target、池重置、固有 Tag、装备 Lease、存档/网络恢复和大规模性能；当前 Warning 只提供架构冻结，不能把静态或 EditMode 证据升格为 PlayMode/Player/发布可玩。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色通用架构验证_MMO开放世界角色切换剧情RPG战斗_AI协作说明.md` (`0e8fac01764793306187df82ed6ca6a18e6c8ebbbd6a7bd55ba8dde2c06d98be`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs` (`11c0b7b888ca34faa87cee7afc2dc87db5452781ca5222f6111f9e0822b03304`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs` (`9a05fd7f643fc9dfc2d9e359a178cb133e9e0ef3cb3de9e9ab7901b6078b8d76`)
