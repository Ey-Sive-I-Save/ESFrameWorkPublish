# 角色 Prefab 职责与 DataInfo 入口：AI 协作警告

状态：现行约束；角色模板与正式 Variant 的实现/验收按本文件执行。  
最后核对：2026-07-31。

## 负责范围

本规则约束 `Entity` 根、`EntityCharacterProfile`、三种角色 Prefab 身份、挂点、FinalIK、武器根和角色制作验证。它不把 AI、Buff、战斗或对象池变成新的角色 MonoBehaviour 体系。

正式说明见：`Documentation/CHARACTER_PREFAB_CONTRACT.md`。

## 当前有效设计

```text
Entity 生命周期
  -> 同根 EntityCharacterProfile
  -> Entity.BindDefinition(唯一 DataInfo)
```

- `Entity` 是唯一的定义绑定执行者；Profile 只保存 Prefab 静态身份、阵营和正式 Variant 的唯一 DataInfo。
- `BuildInput` 无定义，禁止直接发布；`RuntimePoolTemplate` 无定义，由租出方直接 `Entity.BindDefinition(...)`；`CharacterVariant` 自动绑定 Profile 中唯一的 Actor、Monster 或 Npc DataInfo。
- 角色根固定为 `Entity + KinematicCharacterMotor + CapsuleCollider + EntityCharacterProfile + EntityTransformMapping`；模型固定承载一个 Animator。
- AI、Buff、战斗和状态能力留在 Entity 的 Domain / Module。`EntityBuffDomain` 已有 Buff 实例、叠层、持续时间、ValueChange / Permit、Op 与 Tag Lease 的运行时底座，不是空域，也不等于完整玩法已经验收。
- `StateFinalIKDriver` 是状态到 IK 的表现桥。模板使用无 Solver、全部能力关闭的轻量基线；正式 Variant 仅在对应 Solver/前置依赖齐全后启用能力。
- `EntityWeaponBinding` 只按需挂在每个实际武器根；无武器角色不挂空组件。手持优先级为显式 `handMount -> WeaponSocket -> Combat 回退`，双手副手目标和偏移归武器 Binding。
- `EntityTransformMapping` 是挂点缓存服务。固定挂点读取缓存，热路径禁止重新 Find。

## 已废止或禁止的设计

- 禁止增加 `EntityCharacterComposition`、`EntityCharacterDefinitionBinding`、`CharacterActor` 或同义桥接组件来转发 Profile 到 Entity。
- 禁止使用“运行时生成器”作为通用池角色的定义所有者；只有明确的租出方调用 `Entity.BindDefinition(...)`。
- 禁止通过 `SerializedObject`、反射、跨程序集读取 Driver 私有/`internal` 序列化字段配置模板。Driver 制作工具需要的语义必须走其 `public` API。
- 禁止把 Unity Layer、GameTag 或 Humanoid 手骨滥当阵营、角色定义或武器业务偏移。
- 禁止把基础模板、预览模型或 Solver 未配置的正式 Variant 当成可发布角色。

## 高风险误区

1. BuildInput 和 RuntimePoolTemplate 的结构可验证，不代表它们可进入正式内容。
2. Formal Variant 的 DataInfo、阵营、Layer、Collider、HurtBox/HitBox、InteractionProbe、装备和 IK 都是独立验收项；仅跑“创建并验证全部角色模板”不够。
3. Driver 存在但启用能力缺 Solver 必须报错；不能悄悄降级。
4. 回池时 Entity 清除本轮定义、Buff、ValueChange 和 Tag 生命周期；旧 Lease 不得影响下一位租户。
5. 不要为“固定脚本数量”空挂 WeaponBinding 或另加战斗/AI/Buff MonoBehaviour。

## 入口文件

```text
Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs
Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterProfile.cs
Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityTransformMapping.cs
Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityWeaponBinding.cs
Assets/Scripts/ESLogic/Runtime/State/IK/StateFinalIKDriver_/StateFinalIKDriver.AuthoringContract.cs
Assets/Scripts/ESLogic/Editor/CharacterTemplates/ESBasicCharacterTemplateBuilder.cs
Assets/ESNormalAssets/CharacterTemplates/ES基础角色模板.prefab
Assets/ESNormalAssets/CharacterTemplates/ES通用角色完整架构.prefab
```

## 下一步与验收

1. P0：构建/发布门禁阻止基础模板与预览模型进入正式内容。
2. P0：验证器对启用的 FinalIK 能力执行 Solver 契约检查。
3. P1：正式 Variant 补齐阵营、Layer、Collider、HurtBox/HitBox、InteractionProbe、装备和唯一 DataInfo。
4. P1：正式 Variant 跑移动、状态切换、池复用、武器挂载、命中检测、已启用 IK 的 PlayMode 烟雾测试。
5. P2：继续收口 WeaponSocket、Humanoid 骨骼和双手武器偏移的制作规则。
