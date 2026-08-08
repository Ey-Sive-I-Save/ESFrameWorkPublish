# 角色 Prefab 身份、组件边界与验收契约

状态：现行约束。  
最后核对：2026-08-02（CameraDirector 首个运行时切片；Unity 编译与 PlayMode 待验收）。
适用范围：`Assets/ESNormalAssets/CharacterTemplates` 及其正式角色 Variant。

本文件定义角色 Prefab 的制作与运行时边界。当前源码是最终事实；本文件不授权为角色再增加 `Composition`、`DefinitionBinding`、`CharacterActor` 或其他“桥接根”组件。

## 核心结论

`Entity` 是角色的运行时核心和唯一 DataInfo 绑定执行者；`EntityCharacterIdentity` 是同根、唯一的静态身份声明。身份声明负责 Prefab 身份、阵营、正式 Variant 的唯一 DataInfo，及可选的默认 `Camera DefinitionKey`；它不能接管 AI、Buff、战斗、装备、Cinemachine 或相机运行时控制权。

```text
Entity 生命周期
  -> 同根 EntityCharacterIdentity
  -> Entity.BindDefinition(唯一 Actor / Monster / Npc DataInfo)
  -> 若 Profile 配置 Camera DefinitionKey：Entity.Push(Base CameraRequest)
```

`EntityCharacterIdentity` 不是“大脚本管理全部内容”，它是一个很小的 Prefab 元数据入口。默认 Camera DefinitionKey 只是内容身份键；`Entity` 产生纯 Request，场景 `ESCameraDirector` 仲裁并由 CM2 Adapter 执行。四个 Domain 和各自 Module 仍负责运行能力。

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
| 根角色底盘 | `Entity`、`KinematicCharacterMotor`、`CapsuleCollider`、`EntityCharacterIdentity`、`EntityTransformMapping` | 运行入口、KCC 身体、Prefab 身份和稳定挂点缓存；每个根各一份 |
| 模型表现 | `Animator` | 模型动画承载；由根 `Entity.animator` 指向，运行时 Controller 仍由 StateMachine/Playable 链路处理 |
| IK 表现桥 | `StateFinalIKDriver` | 模板可保留唯一 Driver 作为无 Solver 基线；正式 Variant 只在所需 Solver 齐全时显式启用能力 |
| 武器内容 | 每个实际 `weaponRoot` 按需挂 `EntityWeaponBinding` | 武器手持、收纳、枪口、瞄准、副手握点、状态覆盖和武器 Tag；无武器角色不挂空组件 |
| 命中与交互 | 标准 Collider 子节点 | `HurtBox`、`HitBox`、`InteractionProbe` 等由具体角色配置；不另造业务 MonoBehaviour |
| 运行能力 | Entity 的 Domain / Module | AI、Buff、战斗、交互、状态等运行逻辑；不得为了表现层级而再堆根 MonoBehaviour |

Pool 生命周期组件由运行时补充和调用；它不属于角色内容组件清单。阵营是 Profile 的业务身份，不能拿 Unity Layer 或 GameTag 冒充。Unity Layer、HurtBox/HitBox 与交互 Trigger 是正式角色的碰撞/查询配置，仍需分别验收。

### 基础 Module 契约

所有三种身份的角色 Prefab 都必须在 Entity 的序列化 Module 表中显式保存以下各一份：

```text
EntityBasicMoveRotateModule
```

输入执行器不再是独立 Module：`EntityAIDomain` 自身持有输入执行配置，并在域更新阶段统一消费 `inputState`，再驱动 Basic/战斗/技能/交互模块。`inputState` 是 Awake 时创建的纯运行态，不进入 Prefab 或 Scene 序列化。

运行时自动补玩家输入模块的入口已经删除。玩家 Writer 必须由正式 Player Variant 在 Prefab 制作期显式保存，使输入意图的写入与唯一消费入口始终可见、可审计。

`EntityPlayerInputWriteModule` 不属于通用底盘：`BuildInput`、`RuntimePoolTemplate`、以及非玩家阵营的正式 Variant 必须为零；阵营为 `Player` 的正式 Variant 必须显式配置唯一一份。

本项目的正式 `Player` Variant 还固定具备唯一且启用的 `EntityBasicMountModule` 与 `EntityBasicClimbModule`。骑乘状态必须通过 `Mounted` 配置契约；攀爬、攀爬翻上、翻越三段状态必须存在并通过 `Climbing` 配置契约。高翻越填写状态名后同样使用 `Climbing`；攀爬跳跃会离墙进入空中 KCC 分支，填写状态名后必须通过 `Grounded` 配置契约。战斗、技能、相机、游泳、飞行和武器仍按具体玩法添加，不能为了“完整”而塞入通用模板。

## DataInfo 与 Profile 边界

- DataInfo 是角色定义和固有 Tag 的唯一权威；Prefab 不复制第二份 Tag 或定义字段。
- `Entity` 直接读取同根 Profile 并调用自身 `BindDefinition`；禁止增加 `EntityCharacterDefinitionBinding`、`EntityCharacterComposition` 或等价中转组件。
- DataInfo 的运动共享参数在 `Entity.BindDefinition` 时写入 KCC 作者默认值；`ClearDefinition`/回池恢复 Prefab 基线，Character Attribute/ValueChange 可在运行时覆盖。未被 KCC 实际消费的作者字段不得被当作已接通的运行参数。
- Player Variant 的可选 `defaultCameraDefinitionKey/defaultCameraViewKey/defaultCameraPriority` 仅表达默认镜头内容意图；不保存 VCam、Brain、Rig Prefab 或运行时 Lease。没有该键的正式 NPC/怪物不会申请本地相机。
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

`【ES】/内容制作/角色模板/审计项目角色基础模块` 会扫描项目内所有带 `EntityCharacterIdentity` 的 Prefab；发布门禁则会对实际进入场景或 AssetBundle 依赖闭包的正式 Variant 重复执行同一基础 Module 契约。两者都不新增运行时组件。

基础模板验收：根组件唯一性、唯一移动 Module、AI 域输入执行器、无玩家输入写入、无定义 Profile、无武器 Binding、Solver-free Driver、全量 Mapping、层级与运行时容器、EditorOnly 剥离和禁止直接发布。

正式角色验收：唯一 DataInfo、阵营、唯一移动 Module、AI 域输入执行器、玩家阵营唯一输入写入/骑乘/攀爬 Module 与其状态契约、模型/Animator、所启用 IK 的 Solver、Layer、主 Collider、HurtBox/HitBox、InteractionProbe、实际装备与武器 Binding。至少执行一次 PlayMode 烟雾测试：移动、攀爬、上/下车、状态切换、对象池复用、武器挂载、命中检测和已启用 IK。

当前 `ESBasicCharacterTemplateBuilder` 的静态验证和预览场景自检只能证明模板结构；不能替代正式角色的 PlayMode 验收或发布门禁证据。

## 大黑塔正式玩家 Variant 迁移

`Assets/ESNormalAssets/EditorTools/大黑塔.prefab` 是旧预览样例，只能作为模型和 Animator 的迁移来源，禁止成为场景、对象池预热或发布资源的依赖。

在 Unity 脚本编译通过后，可只重建正式角色，执行：

```text
【ES】/内容制作/角色模板/重建正式玩家 Variant/大黑塔（新版通用模板）
```

该工具以 `ES通用角色完整架构.prefab` 为底盘，输出：

```text
Assets/ESNormalAssets/CharacterVariants/大黑塔.prefab
Assets/ESNormalAssets/Data/CharacterVariants/大黑塔_ActorData.asset
```

迁移只保留旧样例的模型、Animator 和纯表现组件；旧 `Entity`、KCC、Collider、运行模块及其他业务脚本一律剥离。输出角色固定为 `CharacterVariant + Player + ActorDataInfo`，显式配置移动、输入、骑乘、攀爬、HurtBox、Humanoid Mapping 与 Solver-free FinalIK 基线，并在保存后立即运行正式角色门禁。

方块载具原型首次配套使用前，可单独执行：

```text
【ES】/内容制作/角色模板/升级方块载具骑乘探针
```

它将车体规范为 `WorldDynamic`，并在 `DriverSeat` 下建立 `Interaction` Trigger 探针，使玩家骑乘查询只命中语义 Layer。

### 玩家控制器测试场景

首次搭建玩家控制器测试内容时，执行一个明确的完整流程：

```text
【ES】/示例与测试/角色/一键准备正式资产并创建 玩家控制器测试场景
```

它依次重建正式大黑塔 Variant、升级三台方块载具的骑乘探针、再创建：

```text
Assets/Scenes/Tests/ESPlayerControllerTest.unity
```

这是唯一会同时写入角色、载具和测试场景的入口；其菜单名称明确表达了这些资产迁移。也可由 Unity 命令行执行：

```text
-executeMethod ES.ESPlayerControllerTestSceneBuilder.PrepareAssetsAndCreateOrRefreshBatch
```

不需要人工依次打开角色、载具或测试场景。

只需刷新已准备资产而不允许改动正式角色或载具时，执行：

```text
【ES】/示例与测试/角色/创建或刷新 玩家控制器测试场景
```

这是纯场景装配工具；不会生成角色、不会升级载具，也不会在打开场景时执行任何迁移。它会只读验证此前已经准备好的资产；如缺失，会指出可单独执行的制作操作：

```text
【ES】/内容制作/角色模板/重建正式玩家 Variant/大黑塔（新版通用模板）
【ES】/内容制作/角色模板/升级方块载具骑乘探针
【ES】/内容制作/相机/创建或刷新默认玩家相机内容
```

随后测试场景工具只读验证正式 Variant 和三台载具的 Probe 约定；前置不合格会直接报错并指出所需操作。验证通过后创建：

```text
Assets/Scenes/Tests/ESPlayerControllerTest.unity
```

场景包含独立的 `ESGameManager`（默认 ES 输入服务）、场景拥有的 `ESCameraSceneBinding + Camera + CinemachineBrain + Director Owned RigRoot`、Ground、攀爬墙、可翻越矮墙，以及汽车/自行车/直升机。相机内容由 `DefinitionCatalog/RigCatalog` 资产提供；角色 Prefab 只保留 Mapping 与 Camera DefinitionKey，不进入 Main Camera、Brain 或 VCam。环境和载具使用 `Ground`、`Wall`、`WorldDynamic`、`Interaction` 的既有 Layer 职责。
