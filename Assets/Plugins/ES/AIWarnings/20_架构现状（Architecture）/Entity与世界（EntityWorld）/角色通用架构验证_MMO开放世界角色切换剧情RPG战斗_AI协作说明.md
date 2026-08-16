# 角色通用架构验证：MMO / 开放世界 / 角色切换 / 剧情 / RPG 战斗

> 记录时间：2026-07-17  
> 职责：给后续 AI 说明“玩家对象模型重构”必须按通用角色体系验证，不要只围绕单机本地主角做窄实现。  
> 性质：基于当前代码的架构验证笔记，不是最终设计定稿。改代码前仍需回读源码和编译验证。
>
> 2026-08-16 现行纠偏：角色 Prefab 不新增 `CharacterActor`、`PlayerActor`、`EntityCharacterComposition`、`EntityCharacterDefinitionBinding` 或同义桥接组件。当前有效入口是 `Entity + 同根 EntityCharacterIdentity -> Entity.BindDefinition(DataInfo)`；正式契约见 `Documentation/CHARACTER_PREFAB_CONTRACT.md` 与同目录 `角色Prefab职责与DataInfo入口_AI协作警告.md`。
>
> 2026-08-01 模块契约补充：角色基础能力不靠运行时自动补齐，自动补玩家输入模块的入口已删除。三种 `EntityCharacterIdentity` 身份的 Prefab 都必须显式保存唯一 `EntityBasicMoveRotateModule`，并由 `EntityAIDomain` 持有输入执行配置和统一执行入口；只有阵营为 `Player` 的正式 `CharacterVariant` 才可且必须保存唯一 `EntityPlayerInputWriteModule`、`EntityBasicMountModule`、`EntityBasicClimbModule`。玩家的骑乘状态必须满足 `Mounted` 契约，攀爬、攀上和翻越状态必须满足 `Climbing` 契约；攀爬跳跃离墙后进入空中 KCC 分支，必须满足 `Grounded` 契约。使用 `【ES】/内容制作/角色模板/审计项目角色基础模块` 做全项目制作期检查，发布门禁会复验实际进入内容的正式 Variant。此规则不授权向模板增加战斗、相机、武器或高级运动组件。
>
> 2026-08-16 装备域纠偏：`EntityEquipmentDomain` 已接线为背包、装备、饰品、挂载过渡和装备效果来源句柄的正式第五域；Inventory / Slot / Attachment / Effect 的最小事务与角色 Prefab 已进入 Verifying。后续实现与验收以同目录 `装备定义与装配推进路线_AI协作说明.md` 为现行合同，不得据本文恢复四域旧事实，也不得把已接线冒充已可玩或已发布。

## 验证目标

本轮验证把角色体系按以下玩法压力测试：

- MMO：同屏多实体、远近 LOD、服务器权威/客户端预测、网络同步、目标选择。
- 开放世界：场景流式加载、出生/回收、远处实体降频、世界注册、存档恢复。
- 角色可切换：队伍成员、本地操控权转移、镜头/输入/AI 接管、非当前角色后台行为。
- 剧情：Cutscene/Dialogue 临时接管输入、相机、角色动作、状态锁定、剧情结束恢复。
- RPG 战斗：技能、Buff、属性资源、目标包、多目标、受击/死亡、战斗状态标签。

结论：当前工程以 `Entity + EntityCharacterIdentity + 五 Domain` 作为统一角色模型，不再补一层 `CharacterActor` 或第二套控制器。后续应沿既有输入、Equipment、Combat、State/KCC 边界逐个补齐能力和证据。

## 当前可复用底座

- `Entity`：当前通用实体运行体，继承 `Core`，持有 Basic/AI/Buff/Equipment/State 五个 Domain，并直接接入 KCC。
- `ESGameManager`：三域入口，系统/流程/世界边界已经明确。
- `ESRuntimeModeService`：已有 Gameplay、Cutscene、Dialogue、Inventory、Map、Pause 等模式和 Combat/Aiming/Mounted/Climbing/Dead/Stunned/NetworkBusy 等标签。
- `ESInputModule / ESInputService`：全局输入服务，已按 RuntimeMode Policy 过滤输入。
- `ESCommand`：命令播放器可驱动 RuntimeMode，适合作为剧情/流程命令底座。
- `ESRuntimeTargetPack`：技能/Operation 目标包，已支持 user、main target、多目标和少量运行时槽位。
- `SkillDefinitionDataInfo`：完整技能体已有标签、释放条件、目标表达式、倍率、次数、联动等字段。
- `Movement` 抽象：`ESMotionIntent / IESMotionDriver / EntityMotionDriverAdapter` 已存在，适合作为剧情移动、AI移动、开放世界运动代理的通用接口。
- `EntityTransformMapping`：可承接玩家层级模板里的稳定挂点，避免运行时深层 Find。
- `ESGameSave`：已有分区存档 Archive，可保存角色、世界、队伍等分区快照。
- `EntityBuffDomain / ESActiveBuffRuntime`：已有 Buff 添加、合并、层数/时长、Tick、移除、查询、Op、ValueChange EffectLease、Tag Lease 和实例回池底座；不再是空域。

## 现行角色与控制边界

### 1. Entity / Identity 是唯一角色模型入口

不在 `Entity` 之上再建 `ESCharacter / ESActor / PlayerActor` 聚合层。当前角色范围统一由以下结构表达：

```text
Entity
  + EntityCharacterIdentity -> Entity.BindDefinition(DataInfo)
  + Basic / AI / Buff / Equipment / State Domains
  + EntityTransformMapping / KCC / StateMachine / 表现驱动
```

这套结构同时覆盖：

- 本地玩家当前操控角色。
- 队伍中非当前操控角色。
- NPC / 怪物 / 友方单位。
- 剧情临时控制角色。
- 网络同步角色。

职责边界：

```text
定义身份与阵营：EntityCharacterIdentity / DataInfo
运行身体与生命周期：Entity / GenericLife / Pool
运动与交互执行：BasicDomain / KCC / MotionDriver
输入与控制请求：EntityAIDomain
装备事实：EntityEquipmentDomain
效果与限制：EntityBuffDomain
状态和表现：EntityStateDomain / StateMachine / IK Driver
```

存档、网络和世界注册需要的稳定实例身份仍应由各自正式实例/世界合同承载，不能塞进 `EntityKCCData`，也不能据此新增第二个角色根。

### 2. Control Authority 协议

角色可切换、剧情接管、网络同步、本地输入都在争夺同一个问题：谁有权给角色写意图。

当前已经落地的本地玩家链路是：

```text
ESInputService
  -> EntityPlayerInputWriteModule
  -> EntityAIDomain.inputState
  -> Basic / Equipment / Combat 的正式执行入口
```

AI、Network、Cutscene、Replay 后续接入时，必须先通过明确的来源授权/优先级写入 `EntityAIDomain` 可消费的请求，再复用相同执行入口；不得各自新增常驻 Controller 或直接写 KCC、Transform、Combat、StateMachine。

```text
Input / AI / Network / Cutscene / Replay
    -> 来源授权与请求写入
    -> EntityAIDomain
    -> Entity 正式 Domain/Module 执行入口
```

完整的多来源优先级、抢占、恢复和网络权威协议仍需单独实现与验收；本文只冻结入口边界，不把未来能力写成现成事实。

### 3. World Character Registry

开放世界和 MMO 都需要世界级角色索引。当前没有看到正式 `EntityRegistry`。

建议归属：

```text
ESGameManager.WorldDomain
    -> CharacterWorldModule / EntityRegistryModule
```

职责：

- 注册/注销角色实例。
- 按 instanceId、configId、team、距离、区域、场景块查询。
- 维护当前本地队伍、当前操控角色、关注目标。
- 给技能、AI、剧情、存档提供稳定查询入口。
- 处理场景流式加载和实体回收。

不要让技能、剧情、UI 各自 `FindObjectsOfType<Entity>()`。

### 4. Party / Switch Character 层

角色切换不是简单启用另一个实体的输入执行；必须通过 `EntityAIDomain` 的控制门禁和输入状态边界完成切换。

需要显式处理：

```text
旧角色：释放输入控制权，进入 AI/Follow/Idle 控制，保留战斗/状态/位置。
新角色：获取输入控制权，绑定镜头 Follow/Aim，刷新 UI，刷新输入上下文。
队伍：统一保存成员、当前索引、队伍共享资源、后台冷却。
```

切换流程建议：

```text
RequestSwitch(targetCharacterId)
    -> 检查 RuntimeMode 是否允许
    -> 当前角色 ExitPlayerControl
    -> 目标角色 EnterPlayerControl
    -> CameraBinding 切换
    -> UI/Target/CombatContext 刷新
```

不要把角色切换写成直接改 Main Camera target + 输入模块引用。

### 5. Story / Cutscene 接管层

`ESRuntimeMode.Cutscene` 和 `ESRuntimeMode.Dialogue` 已有，Command 可以驱动 RuntimeMode。这是正确底座。

缺少的是角色级受信接管协议：

```text
Cutscene 受信 Bridge 获取有代际的控制权令牌
通过 EntityAIDomain 写入受控请求，或经正式 Motion/State API 播放受控动作
锁定/覆盖战斗输入和交互输入
剧情结束后释放令牌并恢复原控制来源
```

剧情控制不要直接乱改 KCC/Transform。优先使用：

- `IESMotionDriver.Teleport / ApplyMotionIntent`
- StateMachine 临时状态
- RuntimeMode 输入过滤
- CameraBinding 临时镜头

### 6. RPG Combat 通用层

当前技能运行已有 `SkillDefinitionDataInfo`、`EntityState_Skill`、`ESRuntimeTargetPack`、Operation。需要补的是角色战斗模型：

```text
CharacterStats：属性、等级、资源、抗性、成长
CharacterCombatState：阵营、战斗锁定、当前目标、威胁/仇恨、受击窗口
CharacterSkillRuntime：技能槽、冷却、充能、资源消耗、连携
CharacterBuffRuntime：Buff 实例、标签、叠层、属性修饰
```

BuffDomain 已有可运行的 Buff 生命周期底座，禁止再按“空域”重新设计一套。仍需补齐或实跑验收的是角色战斗上层编排、对象池 Host 重置、固有 Tag、装备/区域 Lease、存档/网络恢复和大规模性能证据。

RPG 战斗统一链路建议：

```text
EntityAIDomain Attack/Skill intent
    -> EntityBasicCombatModule.TryExecutePrimaryAttack / Skill 提交入口
    -> 当前 WeaponDefinition、Action 与相关系统检查自身条件
    -> SkillDefinitionDataInfo 准备 RuntimeTargetPack
    -> StateMachine 激活 EntityState_Skill
    -> Skill Track 执行 Operation
    -> Damage/Buff/Reaction/Camera/VFX/Audio
```

不要把伤害、属性、Buff 逻辑塞进动画状态机层；StateMachine 负责表现和时序，不负责完整数值系统。

## 对现有层级模板的校准

`【必须】玩家_大黑塔_工业级层级模板` 方向正确，但它应升级为“角色通用层级模板”，不要只服务大黑塔或本地玩家。

模板里的节点应对应到代码绑定：

```text
运行时_逻辑与碰撞
    Entity / MotionDriver / 既有 Domain 与 Module
模型表现
    Animator / Armature / Mesh / ModelOffset
动画辅助_IK与挂点
    EntityTransformMapping / IK targets / MatchTarget / HitVFX points
装备
    WeaponSlots / Equipment visual roots
相机参考点
    CameraPivot / AimPivot，不放 Main Camera
RuntimeGenerated
    所有运行时临时对象统一挂载并在 Despawn 清理
```

需要明确：

- 哪些节点是真组件载体。
- 哪些节点只是容器。
- 哪些节点是可选。
- 哪些节点由代码运行时生成。
- 哪些引用必须进入 `EntityTransformMapping` 或既有组件的明确序列化字段。

## P0 冻结：基础角色模板与派生边界

项目统一采用“双模板”构建契约：

```text
ES基础角色模板.prefab
    第一次构建前的可编辑源模板
    保留 EditorOnly 调试区、全局占位模型和完整结构
    禁止作为场景内容、对象池预热项或 AssetBundle 根资源发布

ES通用角色完整架构.prefab
    从基础模板生成的运行时通用产物
    只解包最外层角色模板，保留模型 Prefab/FBX 依赖复用
    自动删除 Editor/Debug 节点
    仍是通用池模板；带 GlobalPreview 时也禁止进入正式内容
```

编辑器生成与验证入口：

```text
【ES】/内容制作/角色模板/创建或重建首次构建基础模板
【ES】/内容制作/角色模板/从基础模板生成完整通用角色
【ES】/内容制作/角色模板/创建并验证全部角色模板
【ES】/内容制作/角色模板/验证全部角色模板
【ES】/内容制作/角色模板/审计项目角色基础模块
【ES】/内容制作/角色模板/运行完整角色运行态烟雾测试
```

两套模板都只表达玩家、NPC、Monster 共用的“生命体身体底座”，固定包含：

```text
根节点：Entity + KinematicCharacterMotor + CapsuleCollider + EntityTransformMapping
Animator：Controller 必须为空，由 Entity StateMachine/Playable 驱动
占位模型与 Avatar：读取全局 StateMachineConfig.previewModel / previewAvatar
EntityStateDomain + 默认状态包/状态机配置
EntityBasicMoveRotateModule
EntityAIDomain（纯运行时输入状态、控制门禁与统一输入执行）
StateFinalIKDriver + Humanoid 骨骼绑定
IKTargets + MatchTargets + 稳定挂点
检测碰撞、装备、特效音频、相机参考和 RuntimeGenerated 容器
```

FinalIK 的规则补充：基础/通用池模板没有 Solver 时必须明确关闭对应功能开关；正式 Variant 开启 Biped、Aim、LookAt、Grounder、FullBody、HitReaction 或 Recoil 前，必须挂齐对应 Solver。禁止“Driver 存在、功能开关开启、Solver 缺失”的静默退化。

基础模板禁止直接包含：

- `EntityPlayerInputWriteModule`，因为本地玩家输入不是所有生命体的共性。
- 相机控制、武器逻辑、战斗和技能；允许并要求保留对应的稳定引用容器。
- 飞行、游泳、攀爬、骑乘和 RootMotion 等可选运动能力。
- 只服务某个具体角色或玩法的模块。
- 根节点 `Rigidbody`。复杂环境影响通过 KCC 能力扩展；布娃娃 Rigidbody 只能存在于子骨骼体系，并与 KCC 运行状态互斥。

标准派生关系：

```text
ES基础角色模板
├─ 玩家 Variant：增加 PlayerInput、Camera、Interaction 和所需高级运动能力
├─ NPC Variant：增加 NPC 控制源、交互/剧情领域能力
└─ Monster Variant：增加 Monster AI、感知、战斗领域能力
```

硬规则：

- 新角色优先制作 Prefab Variant；不得复制“大黑塔”作为通用角色起点。
- 玩家/NPC/Monster 可以共享相同业务枚举值，但各自领域配置和控制来源必须独立。
- 派生模板可以增加能力，不能绕过 `Entity → Domain → Module → KCC/StateMachine` 权威链路。
- KCC Motor 始终是根位姿最终执行权威；派生能力不得在普通 `Update` 直接写角色根 Transform。
- Player/AI/Network/Cutscene 只在 `EntityAIDomain` 侧预留控制来源，最终统一写入 InputState/Intent，再由 AI 域级执行入口消费。
- 基础模板调整后必须重新生成完整模板并运行双模板验证，确保唯一基础移动 Module 和 AI 域输入执行器、玩家输入为零、高级运动模块为零、Animator Controller 为空、IK/MatchTarget、状态引用与 Mapping 完整。
- 角色 Prefab 的基础能力必须显式保存唯一 `EntityBasicMoveRotateModule`，输入执行配置由 `EntityAIDomain` 持有；运行时自动补玩家输入模块的入口已删除。正式 Player Variant 再额外挂唯一且启用的 `EntityPlayerInputWriteModule`、`EntityBasicMountModule`、`EntityBasicClimbModule`，并配置通过状态契约的骑乘、攀爬、攀上和翻越状态；非玩家 Variant 不得误挂玩家输入写入。全项目制作期使用“审计项目角色基础模块”，实际发布内容再由发布门禁复验。
- 基础模板和 GlobalPreview 的发布门禁必须开启：Player 场景与 AssetBundle 依赖闭包中一旦出现它们，构建直接失败。
- `EntityTransformMapping` 是运行时缓存挂点服务：固定键走缓存，动态键只用于初始化/事件边界；装备、相机、特效不得在热路径重新 `Find` 层级。
- 正式角色 Variant 必须在根 `EntityCharacterIdentity` 绑定唯一的 Actor / Monster / NPC DataInfo，声明阵营，配置 EntityBody 主 Collider、EntityHurtbox Trigger 与需要时的 Interaction Trigger；通用池模板保持无定义，由租出方直接调用 `Entity.BindDefinition(...)`。
- MainHand / OffHand / PrimaryBack / SecondaryBack / Hip / TemporaryHand 是角色作者化业务 Socket；Humanoid RightHand 只保留骨骼语义。武器根通过 `EntityWeaponBinding` 明确 GripPivot、OffHandGrip、Muzzle、AimReference 与 PresentationRoot，不得恢复单一 `WeaponSocket` 或手骨回退。
- 完整模板不负责 AssetBundle 标记、AssetTable 或 AssetLibrary 注册；这些仍属于独立资源构建阶段。
- 不得另造与 `Entity` 并列的 Character/Motion 大根来解决模板派生问题。

## 当前最大阻塞点

- `EntityBasicModules.cs` 过大，混合移动、战斗、武器、相机、技能测试等职责。
- `EntityAIModules.cs` 名称不准确，里面既有输入采集也有输入分发，不适合 MMO/剧情/网络共用。
- 控制权没有统一模型。当前输入、AI、剧情、网络如果同时存在，会直接争写 Entity 或模块。
- 世界实体注册缺失。开放世界和 MMO 需要稳定 registry。
- 角色身份和运行身体耦合。`Entity` 现在既像身体，也像角色实例。
- BuffDomain 已有运行时基础能力，但 RPG 战斗不能把它当作已完成的全功能 Buff 产品；对象池 Host 重置、固有 Tag、装备/区域来源、持久化恢复、事件覆盖和性能必须继续按源码与 PlayMode 验收。
- 存档已有分区 Archive，但角色/队伍/世界快照结构还没有统一。

## 推荐迁移顺序

1. 保持 `Entity + EntityCharacterIdentity + 五 Domain` 为唯一角色模型，不新增平行 Actor/Controller 根。
2. 玩家、AI、剧情、网络和回放的输入意图统一进入 `EntityAIDomain`；新增来源先补控制权门禁，不直写 Basic/KCC。
3. Equipment 继续作为 Inventory / Slot / Attachment / Effect 唯一写权威；Combat 只读活动槽并执行攻击/开火。
4. 世界实体注册、队伍和当前操控角色若落地，应进入 `ESWorldDomain` 的明确模块，不塞入角色 Prefab 外壳。
5. 在现有 BuffDomain 上补对象池、固有 Tag、外部 Lease 与持久化验收，并建立到 StateMachine Buff 表现层的明确桥接；禁止另造第二套 Buff 运行时。
6. 只在职责与测试门禁明确后机械拆分巨型源文件；不得借拆文件重建 Combat/Weapon/Aim 三套控制权。

## 商业级验收场景

重构不是写几个类就算完成。至少要能跑通这些流程：

- 单本地主角：移动、跳跃、交互、技能、镜头。
- 队伍切换：A 角色战斗中切 B，A 转 AI/Follow，B 接输入和镜头。
- 剧情接管：进入 Dialogue/Cutscene，输入被过滤，角色执行剧情动作，结束后恢复原控制者。
- 开放世界回收：远处 NPC 降频/回收，回来后从快照恢复。
- RPG 战斗：技能目标包、多目标、Buff、伤害表现、死亡状态。
- 网络模拟：远端角色不能被本地输入直接控制，只接收 NetworkIntent/MotionSnapshot。

如果某个设计只能跑单机主角，不能跑这些流程，就不是本次“玩家对象模型重构”的合格架构。
