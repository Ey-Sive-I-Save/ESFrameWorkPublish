# ES 协作 AI 职责卡：StateMachine / FinalIK / Buff

给后续协作 AI 阅读。本文不是用户文档，而是工程边界说明。重点职责：**维护 IK 与 StateMachine 的上层架构关系、性能判断、编辑器可用性和职责边界**。不要冒充 Final IK 源码作者，也不要把不确定内容写成已实现事实。

## 我的职责所在

优先关注这些问题：

- `StateMachine` 与 `StateFinalIKDriver` 的调用链是否清晰。
- 状态层、IK 层、Buff 层是否职责混乱。
- Final IK 是否被封装成业务可用 API，而不是暴露底层 solver 细节。
- 热路径是否产生 GC、重复查找、字符串构造或无意义 solver 更新。
- 编辑器是否中文清晰、分区合理、开关可见、配置不吓人。

不应该越界做这些事：

- 不要把 `StateMachine` 改成 Final IK 配置器。
- 不要把 `StateFinalIKDriver` 改成业务状态机。
- 不要把 `StateLayerType.Buff` 当成完整 Buff 数值系统。
- 不要因为“降噪”删除或隐藏用户需要配置的序列化字段。
- 不要为旧 API 保留兼容包装，除非用户明确要求。

## 当前实体结构

`Entity` 当前持有 4 个 Domain：

- `EntityBasicDomain`：身体基础能力。移动、战斗、技能、相机、RootMotion、攀爬、游泳、飞行、交互、脚贴合等。
- `EntityAIDomain`：意识/输入/调度。AI 输入采集和输入调度属于这里。
- `EntityBuffDomain`：刚加入的空域。当前只有 Domain 和 ModuleBase，没有 Buff 运行时逻辑。
- `EntityStateDomain`：状态机、动画状态数据包注册、状态预览、状态调试、StateMachine 与 FinalIK Driver 关系链。

`EntityKCCData kcc` 不是 Domain。它是 `Entity` 上的高频运动核心，不走模块体系。

## StateMachine 上层链路

推荐链路：

```text
Entity / Basic / AI / Buff
    -> EntityStateDomain
    -> StateMachine
    -> Animator / PlayableGraph
    -> stateGeneralFinalIKDriverPose
    -> StateFinalIKDriver.LateUpdate()
    -> FinalIK Solver
```

初始化顺序要保持：

1. 初始化 `StateMachine`，绑定 `Animator` 和 `StateFinalIKDriver`。
2. 注册 `StateAniDataPack` 内的状态。
3. 数据注册完成后再启动 `StateMachine`。

不要在状态数据包注册前启动默认状态。默认状态依赖数据包时会抢跑失败。

职责边界：

- `StateMachine`：状态语义、状态生命周期、动画混合、IK Pose 汇总。
- `StateFinalIKDriver`：消费最终 IK Pose 和实时 IK 请求，统一调度 Final IK。
- `EntityBasicDomain / EntityAIDomain / EntityBuffDomain`：发起行为、输入、效果意图，不直接操作 Final IK solver。

## IK 与状态的边界

状态 IK 适合表达：

- 四肢定位
- LookAt
- 动画状态附带的姿态修正
- 淡入淡出过程中的 IK 贡献

实时 IK API 适合表达：

- AimIK 瞄准
- Peek 探头
- Grounder 开关
- Recoil 后坐力
- HitReaction 受击反馈

不要让同一个意图双写。例如“瞄准”不能同时由状态 Pose 和 `IKAimAt()` 无规则地争夺主导权。需要明确：状态负责动画姿态贡献，Driver API 负责实时程序化目标。

## FinalIK Driver 规则

`StateFinalIKDriver` 是 Final IK 的产品化封装。业务代码不应直接碰：

- `AimIK.solver`
- `BipedIK.references`
- `LookAtIK.solver`
- `FullBodyBipedIK.solver`

公共 API 必须使用统一 `IK` 前缀。当前方向示例：

```csharp
ik.IKSetAimTargetTransform(target);
ik.IKAimAt(target, 1f);
ik.IKSetAimTargetPosition(worldPosition);
ik.IKStopAim();

ik.IKPeekLeft();
ik.IKPeekRight();
ik.IKClearPeek();

ik.IKSetFootGrounding(true);
ik.IKPlayRecoil();
ik.IKHit(collider, force, point);
```

不要移动用户传入的目标 Transform。Aim 偏移、探头、world-position 目标只允许写内部代理 Transform。

多 Final IK solver 改同一套骨骼时，必须明确顺序和阻断规则。尽量由 Driver 手动统一调度，不要让多个 FinalIK 组件各自自动 `LateUpdate`。

## 性能判断

当前主要结构成本通常是 CPU，不是 GC：

- 每实体一个 `PlayableGraph.Evaluate(deltaTime)`。
- Final IK solver 求解，尤其 `FullBodyBipedIK`。
- 运行状态遍历和 IK Pose 汇总。

`GetRunningStatesSnapshot()` 是安全遍历设计。它复用 `_runningStatesSnapshot`，成本是清引用和复制当前运行状态引用，不是每帧分配。不要轻易删除。它防止 `OnStateUpdate()`、自动退出、强/弱打断、淡入淡出期间修改运行集合导致遍历错乱。

`runningStates` 使用 `SwapBackSet<StateBase>`，适合快速增删。不要随便换成普通 `List.Remove`。

压测优先方向：

- 远处/低重要性实体降频 Update。
- IK 求解按权重、可见性、距离分档。
- 关闭 IK 贡献诊断和 `STATEMACHINEDEBUG` 日志。
- 避免热路径 LINQ、字符串拼接、组件扫描、临时集合分配。

## BuffDomain 边界

`EntityBuffDomain` 当前是空域，只表示架构预留。不要误认为已经有完整 Buff 系统。

`StateLayerType.Buff` 是状态机动画/表现层，不是 Buff 逻辑系统。它适合：

- 中毒姿态
- 眩晕循环
- 霸体姿态
- 蓄力表现
- 持续状态的动画或 IK 表现

未来 Buff 系统应该分层：

```text
BuffDomain       管逻辑、时间、叠层、标签、来源、驱散、查询
ModifierSystem   管属性修饰聚合和脏标记重算
EventRouter      管攻击/受击/击杀/死亡/状态切换等触发
VisualBridge     桥接 StateMachine Buff层、VFX、UI
```

不要把 Buff 数值逻辑塞进 StateMachine Buff 层。不要把动画表现逻辑塞进 Buff 数值核心。

高性能 Buff 系统应避免：

- 每个 Buff 一个 MonoBehaviour。
- 每个 Buff 每帧虚函数 `Update()`。
- 每帧全量重算属性。
- 字符串作为热路径 Key。
- `List.Remove` 大量元素位移。

## 编辑器规则

用户偏好中文界面。Inspector 要做到：

- 中文主标题。
- 配置、诊断、测试、高级分开。
- 每类 IK 独立清楚，但不要堆满 Box。
- 开关必须可见，不能为了降噪撤掉序列化字段。
- 复杂项用折叠、分组、预设、关键参数摘要解决。

Odin Group 路径必须严格一致。不要让同名 group 混用 `TitleGroup`、`FoldoutGroup`、`HorizontalGroup` 等不同 Group 类型。

推荐 IK 分区名：

- 【四肢定位】
- 【脚步贴地】
- 【头眼注视】
- 【武器瞄准】
- 【全身反应】
- 【受击反馈】
- 【后坐力】

## 修改原则

- 先读现有链路，再改。
- 运行时热路径改动必须说明 GC、CPU、调度成本分别是什么。
- 不要把第三方插件复杂度暴露给业务层。
- 不要引入大量兼容旧名 API。
- 不要因为排版问题改变运行语义。
- 文档要区分“已实现”和“建议方向”。

## 2026-07-18 状态动画缓存复用修正警告

本节是今天状态机动画缓存复用问题后的新增警告，后续 AI 必须优先遵守。旧思路“状态退出就销毁 Playable、下次进入天然重新初始化”已经不再成立。

### 已纠正的陈旧思想

- 不要再默认 `HotUnplugStateFromPlayable()` 会销毁状态 Playable。
- 不要再认为“第二次进入状态”等价于“重新创建完整 Playable 树”。
- 不要再依赖 `InitializeRuntimeInternal()` 的初始化副作用来清理阶段索引、权重缓存、速度、时间、事件缓存。
- 不要在普通退出路径里随手 `Destroy()` 运行中缓存 Playable。缓存复用后，错误销毁会导致后续进入拿到无效节点或卡住。
- 不要把 `ResetRuntimeForReuse()` 当作可选优化。对于有内部状态的动画计算器，它是生命周期契约。

### 今天发现的真实问题

缓存复用后，多个状态出现“第一次进入正确，后续进入卡住或不动”。根因不是 Unity Playable 本身，而是复用进入时没有完整恢复运行时语义：

- Playable 时间没有回到 0。
- 被暂停的子 Playable 没有恢复速度。
- 阶段计算器停在旧阶段，如 Released、最后阶段、完成态。
- Mixer 权重缓存仍认为上次权重已经写入，导致首帧没有重新写权重。
- SmoothDamp 速度缓存、输入缓存、DirectBlend 事件上一帧权重缓存沿用旧状态。
- 嵌套计算器只复位外层，内层 Phase/Sequence 仍保持旧运行态。

典型表现：

- 单采样 BlendTree 第一次正常，第二次像冻结。
- SequentialStates / Phase4 / SequentialClipMixer 第二次进入卡在旧阶段。
- 技能或攀爬类状态复用后表现不从头播。
- OverrideClip 后退出再进，节点还在但时间/速度/权重不符合预期。

### 当前已补的契约

主要动画计算器已经补 `ResetRuntimeForReuse()` 或等价复位：

- `SimpleClip`
- `BlendTree1D`
- `BlendTree2D`
- `DirectBlend`
- `MixerWrapper`
- `SequentialClipMixer`
- `SequentialStates`
- `Phase4`

基础运行时 `AnimationCalculatorRuntime.RewindCachedPlayableTreeForEnter()` 应负责通用复位：

- invalidate mixer 输入权重缓存。
- 重置输入平滑缓存。
- 重置 SmoothDamp 速度缓存。
- 重置 sequence/phase 基础字段。
- 重置 Playable 时间。
- 恢复通用 Playable speed 为 1。
- 递归 rewind 子 runtime。

复杂计算器必须在自己的 `ResetRuntimeForReuse()` 里补语义复位：

- 当前阶段应从哪一段开始。
- 哪些阶段可以推进，哪些阶段必须停住。
- 当前/上一阶段过渡权重。
- 子计算器是否需要递归 `ResetRuntimeForReuse()`。
- 自定义播放速度，如 SimpleClip speed、SequentialClipMixer entry/main/exit speed。

### 后续修改状态动画系统的硬规则

1. 新增任何 `StateAnimationMixCalculator`，如果有数组缓存、阶段索引、权重缓存、SmoothDamp、事件缓存、子 runtime、暂停/播放状态，必须实现 `ResetRuntimeForReuse()`。
2. `InitializeRuntimeInternal()` 只负责首次创建图结构，不负责每次进入状态的语义复位。
3. `HotUnplugStateFromPlayable()` 普通退出只断开上层连接并暂停缓存，不应销毁运行时。
4. 真正销毁只应发生在状态机释放、状态对象回池、运行时 calculator 结构不兼容、显式要求销毁旧 runtime 的路径。
5. 槽位满、激活失败、回滚失败时不能随手 `statePlayable.Destroy()`，因为它可能是缓存节点。
6. 运行时切换 calculator 时，如果结构可能不兼容，必须先销毁旧 runtime 或明确拒绝复用。
7. OverrideClip 替换节点后，要保留或恢复合理的 speed/time/连接关系，并确保复用进入时还能重新归零。

### 性能判断修正

缓存复用不是性能风险，前提是复位契约正确。和重建整套 Playable 相比，复用进入时多做的 `SetTime(0)`、`SetSpeed()`、清缓存、写初始权重成本很小。

不要为了减少几次复位写操作恢复“退出即销毁”。那会重新引入：

- Mixer/ClipPlayable 重建成本。
- 数组和 override slot 初始化成本。
- PlayableGraph connect/disconnect 图结构成本。
- 更高的 GC 和 CPU 抖动。

真正的大头仍然是：

- 每实体 `PlayableGraph.Evaluate(deltaTime)`。
- Final IK solver。
- 大量实体的状态逻辑/动画权重更新。
- 调试日志、诊断字符串、编辑器预览。

### 跳跃与阶段动画判断

跳跃动画表现推荐使用 `SequentialStates`，因为跳跃天然是顺序阶段：起跳、上升、下落、落地。它适合做表现层，不应独自承担跳跃物理。

正确分工：

- 运动模块负责是否离地、垂直速度、落地检测、二段跳次数、土狼时间、跳跃缓冲。
- 状态机负责进入/退出/打断规则。
- `SequentialStates` 负责阶段动画表现。

二段跳不要硬塞进普通跳跃阶段作为唯一方案。更推荐作为空中短表现状态或临时覆盖状态，播放后回到空中跳跃/下落表现。

### 后续 AI 自检清单

改状态动画生命周期前必须自问：

- 这个改动是否假设状态退出后 runtime 会被销毁？
- 第二次进入同一个 State 时，所有时间、速度、权重、阶段字段是否和第一次进入等价？
- 子计算器是否也复位了，而不是只复位外层？
- 是否误把表现层 Buff 状态当成完整 Buff 逻辑？
- 是否为了编辑器排版或降噪改动了运行时语义？
- 是否引入了每帧字符串、LINQ、临时 List/数组、组件扫描？

如果答案不清楚，不要直接改运行逻辑。先读 `StateBase.Animation.cs`、`StateMachine.HotPlug.cs`、`AnimationCalculatorRuntime.cs` 和对应 Calculator。
