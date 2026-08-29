# ES 角色 Prefab 与 DataInfo 入口边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.arch.character-prefab-datainfo-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Entity/Prefab 实现  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `prefab`, `datainfo`, `identity`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `1f42a1d6c161353b8b99febe74f5ce771e47845b2eb3e94e4ed46f9889bc41bd`  
`SourceSetHash`: `1f42a1d6c161353b8b99febe74f5ce771e47845b2eb3e94e4ed46f9889bc41bd`  
`EntryBodyHash`: `1015344cc9057c323bf5565d6803880780de105949758a618576d839f1902746`  
`StaleWhen`: Entity 绑定、角色 Profile/Variant、挂点映射、FinalIK 或装备域实现变化。

## 迁移范围

Warning 保留定义绑定唯一性、Prefab 根组成、挂点与 IK 装配、回池和发布证据边界；本条目保存完整设计、废止方案、入口文件和验收要求。

## 当前设计与禁止事项

Entity 生命周期→同根 `EntityCharacterIdentity`→`Entity.BindDefinition(唯一 DataInfo)`。Profile 保存 Prefab 静态身份、阵营和唯一 DataInfo；BuildInput 无定义不可发布；RuntimePoolTemplate 无定义，由租出方绑定；CharacterVariant 绑定唯一 Actor/Monster/Npc DataInfo。Entity 根固定 `Entity`、`KinematicCharacterMotor`、`CapsuleCollider`、`EntityCharacterIdentity`、`EntityTransformMapping`，模型一个 Animator。EntityEquipmentDomain 聚合 Inventory/Slot/Attachment/Effect，EntityBuffDomain 承载 Buff/叠层/持续时间/ValueChange/Permit/Op/Tag Lease；源码存在不等于玩法验收。

禁止新增 EntityCharacterComposition、DefinitionBinding、CharacterActor 等桥接组件；不把运行时生成器当通用池定义所有者；不使用 SerializedObject/反射读取 Driver 私有或 internal 序列化字段；不以 Layer、GameTag、Humanoid 骨骼冒充阵营、定义或武器偏移。

`StateFinalIKDriver` 是状态到 IK 的表现桥，模板无 Solver/能力关闭，Variant 仅在 Solver 与前置依赖齐全后启用。`EntityWeaponBinding` 只挂实际武器根；`EntityTransformMapping` 缓存 MainHand/OffHand/PrimaryBack/SecondaryBack/Hip/TemporaryHand 等作者化挂点，武器根提供 GripPivot/OffHandGrip/Muzzle/AimReference/PresentationRoot。缺失正式 Socket 必须拒绝，禁止 Find 回退或运行时自动造点。

## 风险与验收

结构可验证不代表可发布。Formal Variant 的 DataInfo、阵营、Layer、Collider、HurtBox/HitBox、InteractionProbe、装备和 IK 各自验收；Driver 缺 Solver 必须报错，不悄悄降级；回池清除定义、Buff、ValueChange、Tag 生命周期，旧 Lease 不得影响下一租户。入口文件包括 Entity.cs、EntityCharacterIdentity.cs、EntityTransformMapping.cs、EntityWeaponBinding.cs、EntityEquipmentDomain.cs、StateFinalIKDriver.AuthoringContract.cs、ESBasicCharacterTemplateBuilder.cs 和两个 CharacterTemplates Prefab。

验收分级：P0 阻止基础模板/预览模型进入正式内容并检查 FinalIK Solver；P1 补齐 Variant 阵营、Layer、碰撞、命中、交互、装备和唯一 DataInfo，并跑移动/状态/池复用/武器/IK PlayMode；P2 补 Equip/Holster/Switch、双持、最终 IK Pose 的 PlayMode/Profiler/Player 证据。本轮未运行 Unity/Runtime。

## 原文快照

迁移前原始文件为 65 行、4903 UTF-8 字节，原始 SHA-256 为 `3ffbcc8b1030f7c47e82eff496f0a22cc892d65d04e02e97ff1116a6aba31d83`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md` (`4e1a75e52b673a57f10f8a53c2b566c44e60246b9f5bcb03cc8e9bf05d9bb306`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`0324adf143742707ee9005df5bdb6ab8a8c229beb3bd1b3ef81966bb993eebd9`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-architecture-character-prefab-datainfo-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md`
