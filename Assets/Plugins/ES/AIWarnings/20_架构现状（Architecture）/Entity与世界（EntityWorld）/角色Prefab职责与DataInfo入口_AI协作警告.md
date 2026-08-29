# 角色 Prefab 职责与 DataInfo 入口：AI 协作警告

Status: current
StableId: es.aiwarning.arch.character-prefab-datainfo-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, architecture, entity, prefab, datainfo, identity
Applicability: Entity 根、EntityCharacterIdentity、角色 Prefab、挂点、FinalIK、武器根与制作验证
Owner: ESFramework EntityWorld 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-architecture-character-prefab-datainfo-boundary.md
StaleWhen: Entity 绑定、角色 Profile/Variant、挂点映射、FinalIK 或装备域实现变化。

## 长期约束

- Entity 是唯一定义绑定执行者，经同根 `EntityCharacterIdentity` 调用 `Entity.BindDefinition(唯一 DataInfo)`；Profile 只保存静态身份、阵营与唯一 DataInfo。BuildInput 不可发布，RuntimePoolTemplate 由租出方绑定，Variant 自动绑定唯一 Actor/Monster/Npc DataInfo。
- 角色根固定 `Entity + KinematicCharacterMotor + CapsuleCollider + EntityCharacterIdentity + EntityTransformMapping`，模型承载一个 Animator；AI/Buff/战斗/状态/装备留在 Domain/Module，不新增桥接 MonoBehaviour。
- `EntityTransformMapping` 是挂点缓存服务，缺失正式 Socket 必须拒绝装配，不回退 Humanoid 骨骼/根节点/自动造点；WeaponBinding 仅挂在实际武器根。FinalIK 模板默认轻量，Variant 只有 Solver/前置依赖齐全才启用。
- 禁止用 SerializedObject、反射或跨程序集私有字段配置 Driver；禁止用 Layer、GameTag、手骨冒充阵营/定义/武器偏移；基础模板、预览模型或未配置 Solver 的 Variant 不得发布。
- 回池清除定义、Buff、ValueChange、Tag 生命周期，旧 Lease 不得影响下一租户。静态结构或验证器通过不等于角色运行时、IK、武器与发布验收通过。

## Knowledge 导航

完整当前设计、废止方案、高风险误区、入口文件和分级验收矩阵见 `es.aiwarning.arch.character-prefab-datainfo-boundary.v1`。
