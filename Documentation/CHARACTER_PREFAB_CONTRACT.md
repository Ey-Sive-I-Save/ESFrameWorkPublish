# 角色 Prefab 身份、组件边界与验收契约

状态：现行约束。  
最后核对：2026-07-31。  
适用范围：`Assets/ESNormalAssets/CharacterTemplates` 及其正式角色 Variant。

本文件定义角色 Prefab 的制作与运行时边界。当前源码是最终事实；本文件不授权为角色再增加 `Composition`、`DefinitionBinding`、`CharacterActor` 或其他“桥接根”组件。

## 核心结论

`Entity` 是角色的运行时核心和唯一 DataInfo 绑定执行者；`EntityCharacterProfile` 是同根、唯一的静态身份声明。Profile 只声明 Prefab 身份、阵营和正式 Variant 的唯一 DataInfo，不能接管 AI、Buff、战斗、装备、相机或运行时控制权。

```text
Entity 生命周期
  -> 同根 EntityCharacterProfile
  -> Entity.BindDefinition(唯一 Actor / Monster / Npc DataInfo)
```

`EntityCharacterProfile` 不是“大脚本管理全部内容”，它是一个很小的 Prefab 元数据入口。四个 Domain 和各自 Module 仍负责运行能力。

## 三种 Prefab 身份

| 身份 | Profile 定义 | 出生/复用语义 | 发布资格 |
| --- | --- | --- | --- |
| `BuildInput` | 无阵营、无 DataInfo | `Entity` 清除定义 | 禁止直接作为场景内容、池预热项或 Bundle 根发布 |
| `RuntimePoolTemplate` | 无阵营、无 DataInfo | 租出方按本次用途直接调用 `Entity.BindDefinition(...)`；回池由 Entity 清除本轮定义 | 只能作为通用运行时池底座，不是具体角色内容 |
| `CharacterVariant` | 必须声明阵营，并且只能指定一个 Actor、Monster 或 Npc DataInfo | Entity 从同根 Profile 自动绑定 | 可作为正式角色，但须完成正式角色验收 |

这里没有“运行时生成器”概念。对象池租出方就是绑定调用者；它决定本次租出的通用身体代表哪一个定义。

## 组件职责与数量

角色结构以职责收口，而不是凑“每个角色固定 N 个脚本”。

| 层次 | 固定/按需组件 | 职责 |
| --- | --- | --- |
| 根角色底盘 | `Entity`、`KinematicCharacterMotor`、`CapsuleCollider`、`EntityCharacterProfile`、`EntityTransformMapping` | 运行入口、KCC 身体、Prefab 身份和稳定挂点缓存；每个根各一份 |
| 模型表现 | `Animator` | 模型动画承载；由根 `Entity.animator` 指向，运行时 Controller 仍由 StateMachine/Playable 链路处理 |
| IK 表现桥 | `StateFinalIKDriver` | 模板可保留唯一 Driver 作为无 Solver 基线；正式 Variant 只在所需 Solver 齐全时显式启用能力 |
| 武器内容 | 每个实际 `weaponRoot` 按需挂 `EntityWeaponBinding` | 武器手持、收纳、枪口、瞄准、副手握点、状态覆盖和武器 Tag；无武器角色不挂空组件 |
| 命中与交互 | 标准 Collider 子节点 | `HurtBox`、`HitBox`、`InteractionProbe` 等由具体角色配置；不另造业务 MonoBehaviour |
| 运行能力 | Entity 的 Domain / Module | AI、Buff、战斗、交互、状态等运行逻辑；不得为了表现层级而再堆根 MonoBehaviour |

Pool 生命周期组件由运行时补充和调用；它不属于角色内容组件清单。阵营是 Profile 的业务身份，不能拿 Unity Layer 或 GameTag 冒充。Unity Layer、HurtBox/HitBox 与交互 Trigger 是正式角色的碰撞/查询配置，仍需分别验收。

## DataInfo 与 Profile 边界

- DataInfo 是角色定义和固有 Tag 的唯一权威；Prefab 不复制第二份 Tag 或定义字段。
- `Entity` 直接读取同根 Profile 并调用自身 `BindDefinition`；禁止增加 `EntityCharacterDefinitionBinding`、`EntityCharacterComposition` 或等价中转组件。
- BuildInput 必须保持空定义；RuntimePoolTemplate 绝不能在激活时覆盖租出方已经绑定的定义；CharacterVariant 必须只有一个匹配的 DataInfo。
- 编辑器构建工具只负责创建、剥离和验证模板资产；它不是角色出生时的业务“生成器”。

## FinalIK 约束

- 不允许“Driver 存在、功能已打开、Solver 缺失”的静默退化。
- 基础模板和通用池模板使用无 Solver 基线：全部 FinalIK 功能和自动加组件开关关闭，保留缺失提示。
- 正式 Variant 开启 Biped、Grounder、LookAt、Aim、FullBody、HitReaction 或 Recoil 前，必须满足对应 Solver 和前置依赖。
- 工具只能调用 Driver 的公开语义 API，例如 `ConfigureSolverFreeTemplateBaseline`、`ValidateEnabledSolverContract`、`IsSolverFreeTemplateBaseline`、`ConfigureHumanoidBinding`、`MatchesHumanoidBinding`。禁止跨程序集读取私有/`internal` 字段，禁止用反射或 `SerializedObject` 作为运行时 API 替代品。

## 挂点与武器

`EntityTransformMapping` 是运行时缓存挂点服务：固定键读取连续缓存，动态键只可用于初始化或事件边界。装备、相机、特效和战斗热路径禁止重新 `Find` 层级。

`WeaponSocket` 是武器业务挂点；Humanoid `RightHand` 只保留骨骼语义。武器手持优先级固定为：

```text
武器显式 handMount
  -> 角色 WeaponSocket
    -> Combat 回退挂点
```

双手武器的副手目标和局部偏移都写入该武器根的 `EntityWeaponBinding`，不得写回 Humanoid 手骨。

## 发布门禁与验收

P0 发布门禁必须阻止 `ES基础角色模板.prefab`、全局预览模型和其依赖闭包进入正式场景内容、池预热或发布资源。模板验证通过不等于正式角色验收通过。

基础模板验收：根组件唯一性、无定义 Profile、无武器 Binding、Solver-free Driver、全量 Mapping、层级与运行时容器、EditorOnly 剥离和禁止直接发布。

正式角色验收：唯一 DataInfo、阵营、模型/Animator、所启用 IK 的 Solver、Layer、主 Collider、HurtBox/HitBox、InteractionProbe、实际装备与武器 Binding。至少执行一次 PlayMode 烟雾测试：移动、状态切换、对象池复用、武器挂载、命中检测和已启用 IK。

当前 `ESBasicCharacterTemplateBuilder` 的静态验证和预览场景自检只能证明模板结构；不能替代正式角色的 PlayMode 验收或发布门禁证据。
