# 角色通用架构验证：MMO / 开放世界 / 角色切换 / 剧情 / RPG 战斗

> 记录时间：2026-07-17  
> 职责：给后续 AI 说明“玩家对象模型重构”必须按通用角色体系验证，不要只围绕单机本地主角做窄实现。  
> 性质：基于当前代码的架构验证笔记，不是最终设计定稿。改代码前仍需回读源码和编译验证。

## 验证目标

本轮验证把角色体系按以下玩法压力测试：

- MMO：同屏多实体、远近 LOD、服务器权威/客户端预测、网络同步、目标选择。
- 开放世界：场景流式加载、出生/回收、远处实体降频、世界注册、存档恢复。
- 角色可切换：队伍成员、本地操控权转移、镜头/输入/AI 接管、非当前角色后台行为。
- 剧情：Cutscene/Dialogue 临时接管输入、相机、角色动作、状态锁定、剧情结束恢复。
- RPG 战斗：技能、Buff、属性资源、目标包、多目标、受击/死亡、战斗状态标签。

结论：当前工程已有多个底座，但缺少统一的“角色模型层”和“控制权协议”。重构应优先补这层，而不是继续扩大 `EntityBasicModules.cs`。

## 当前可复用底座

- `Entity`：当前通用实体运行体，继承 `Core`，持有 Basic/AI/Buff/State 四个 Domain，并直接接入 KCC。
- `ESGameManager`：三域入口，系统/流程/世界边界已经明确。
- `ESRuntimeModeService`：已有 Gameplay、Cutscene、Dialogue、Inventory、Map、Pause 等模式和 Combat/Aiming/Mounted/Climbing/Dead/Stunned/NetworkBusy 等标签。
- `ESInputModule / ESInputService`：全局输入服务，已按 RuntimeMode Policy 过滤输入。
- `ESCommand`：命令播放器可驱动 RuntimeMode，适合作为剧情/流程命令底座。
- `ESRuntimeTargetPack`：技能/Operation 目标包，已支持 user、main target、多目标和少量运行时槽位。
- `SkillDefinitionDataInfo`：完整技能体已有标签、释放条件、目标表达式、倍率、次数、联动等字段。
- `Movement` 抽象：`ESMotionIntent / IESMotionDriver / EntityMotionDriverAdapter` 已存在，适合作为剧情移动、AI移动、开放世界运动代理的通用接口。
- `EntityTransformMapping`：可承接玩家层级模板里的稳定挂点，避免运行时深层 Find。
- `ESGameSave`：已有分区存档 Archive，可保存角色、世界、队伍等分区快照。

## 必须补的核心层

### 1. Character Model 层

需要在 `Entity` 之上建立通用角色模型，建议命名可以是：

```text
ESCharacter / ESActor / PlayerActor
```

它不应只代表本地玩家，而应覆盖：

- 本地玩家当前操控角色。
- 队伍中非当前操控角色。
- NPC / 怪物 / 友方单位。
- 剧情临时控制角色。
- 网络同步角色。

建议最小职责：

```text
身份：characterId、configId、instanceId、ownerKind、faction/team
引用：Entity、MotionDriver、TransformMapping、StateMachine、Animator
生命周期：Spawn、Activate、Deactivate、Despawn、SaveSnapshot、LoadSnapshot
控制权：当前由 Player / AI / Network / Cutscene / Replay / None 控制
能力面：Locomotion、Combat、Interaction、Inventory/Equipment、Stats、Buff
```

不要把这些身份/控制权字段塞进 `EntityKCCData`。KCC 是身体运动核心，不是角色身份模型。

### 2. Control Authority 协议

角色可切换、剧情接管、网络同步、本地输入都在争夺同一个问题：谁有权给角色写意图。

需要统一抽象：

```text
ICharacterControllerSource
PlayerInputController
AIController
NetworkController
CutsceneController
ReplayController
```

输出应统一为：

```text
CharacterIntent / PlayerIntent
```

而不是让每个系统直接调用：

```text
Entity.SetMoveInput
EntityBasicCombatModule.TriggerAttack
EntityBasicInteractionModule.RequestInteract
StateMachine.TryActivateState
```

推荐链路：

```text
Input / AI / Network / Cutscene / Replay
    -> CharacterIntent
    -> CharacterControllerFacade
    -> Locomotion / Combat / Interaction / Camera / State
    -> Entity / MotionDriver / StateMachine / Operation
```

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

角色切换不是简单启用另一个 `EntityAIInputDispatchModule`。

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

缺少的是角色级接管协议：

```text
CutsceneControllerSource 获取角色控制权
写入 CharacterIntent 或直接播放受控动作
锁定/覆盖战斗输入和交互输入
剧情结束后按原控制源恢复
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

BuffDomain 当前仍是空域，不能假设已有完整 Buff 系统。

RPG 战斗统一链路建议：

```text
CharacterIntent.Attack/Skill
    -> CombatController 检查资源/冷却/状态/目标
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
    Entity / CharacterActor / MotionDriver / ControllerFacade
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
- 哪些引用必须进入 `EntityTransformMapping` 或 `CharacterActor` 序列化字段。

## 当前最大阻塞点

- `EntityBasicModules.cs` 过大，混合移动、战斗、武器、相机、技能测试等职责。
- `EntityAIModules.cs` 名称不准确，里面既有输入采集也有输入分发，不适合 MMO/剧情/网络共用。
- 控制权没有统一模型。当前输入、AI、剧情、网络如果同时存在，会直接争写 Entity 或模块。
- 世界实体注册缺失。开放世界和 MMO 需要稳定 registry。
- 角色身份和运行身体耦合。`Entity` 现在既像身体，也像角色实例。
- BuffDomain 未成体系，RPG 战斗不能基于空域假设。
- 存档已有分区 Archive，但角色/队伍/世界快照结构还没有统一。

## 推荐迁移顺序

1. 先加角色模型和控制权协议，不急着删除旧模块。
2. 增加 `CharacterIntent`，让本地输入、AI、剧情都能输出同一种意图。
3. 增加 `CharacterActor`，引用现有 `Entity`，承接身份、控制权、挂点、控制器集合。
4. 增加 `CharacterWorldModule`，放在 `ESWorldDomain`，管理实体注册、队伍、当前操控角色。
5. 让旧 `EntityAIInputDispatchModule` 逐步变成一种 `PlayerInputControllerSource` 或废弃适配器。
6. 把 `EntityBasicCombatModule` 里的武器、瞄准、开火拆成 Combat/Weapon/Aim 三条控制器。
7. BuffDomain 单独补逻辑系统，和 StateMachine Buff 表现层桥接。
8. 最后再拆 `EntityBasicModules.cs` 文件和旧模块职责，避免一开始破坏现有场景。

## 商业级验收场景

重构不是写几个类就算完成。至少要能跑通这些流程：

- 单本地主角：移动、跳跃、交互、技能、镜头。
- 队伍切换：A 角色战斗中切 B，A 转 AI/Follow，B 接输入和镜头。
- 剧情接管：进入 Dialogue/Cutscene，输入被过滤，角色执行剧情动作，结束后恢复原控制者。
- 开放世界回收：远处 NPC 降频/回收，回来后从快照恢复。
- RPG 战斗：技能目标包、多目标、Buff、伤害表现、死亡状态。
- 网络模拟：远端角色不能被本地输入直接控制，只接收 NetworkIntent/MotionSnapshot。

如果某个设计只能跑单机主角，不能跑这些流程，就不是本次“玩家对象模型重构”的合格架构。

