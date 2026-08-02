# 项目最高警告：Codex 核心上下文总纲

最后更新：2026-07-22

职责：给后续参与 ESFramework 的 AI 读取。本文只写当前已经确认的核心架构结论、用户偏好、禁止误区和后续方向。不要把旧理解继续传播，也不要把“建议方向”写成“已经完整实现”。

## 最高口径

ESFramework 的目标不是堆功能，而是做一套高性能、数据驱动、中文友好、可商业化扩展的 Unity 游戏框架。

优先级顺序：

1. 热路径 0GC 或可解释的低 GC。
2. 初始化严格，运行时信任初始化结果。
3. 编辑器中文清晰，配置简单但不删用户需要的开关。
4. 核心协议统一，避免每个系统各造一套 Mode、Tag、Key、LOD。
5. 文档必须区分“已实现”“半完成”“建议方向”。

## 绝对禁止

- 不要在 `Update`、KCC 回调、IK 求解、StateMachine Evaluate、Buff Tick 中做字符串拼接、LINQ、临时集合、组件扫描、反射查找。
- 不要用大量判空掩盖初始化失败。核心依赖缺失应在初始化阶段断言、报错或终止初始化。
- 不要为了“降噪”撤掉序列化字段。应该用页面划分、折叠、摘要、预设、诊断页解决。
- 不要为旧 API 保留兼容包装，除非用户明确要求。
- 不要把用户已经否定的设计写回代码或文档，例如 Entity 专属 LOD、LODManager 命名、运行时字符串 Key 热路径。
- 不要把调试、测试、诊断逻辑混入常态运行路径。

## 中文与配置

项目强调中文友好。

Inspector 应该：

- 用中文标题和中文说明。
- 功能分区清晰，例如配置、诊断、测试、高级。
- 多功能块优先折叠，不要到处堆 Box 占满屏幕。
- 开关必须可见，用户有权配置启用/禁用。
- Odin Group 路径必须一致，同一路径不能混用 `TitleGroup`、`FoldoutGroup`、`HorizontalGroup` 等不同 Group 类型。

配置身份推荐“双键”：

- 枚举键：核心高频对象使用，强类型、快、可 InspectorName 分层。
- 字符串键：扩展、表格、热更新、低频配置使用，初始化或烘焙后转强类型 Key。

推荐写法：

```csharp
[InspectorName("控制/眩晕")]
控制类_眩晕 = 1
```

不要默认推广 `Buff.控制.冰冻` 这种点号字符串路径作为运行时核心身份。分类展示交给 `[InspectorName("分类/名称")]`，运行时判断交给枚举、RuntimeKey、已缓存 TagId。

## GameTag

`ESTagCollection` 是通用运行时事实容器，不是 Entity 专属类型；当前 Entity 与 Item 都已经按需持有自己的 Collection。只有对象自身需要参与跨系统组合查询时才实际创建 Host，Buff、装备、区域等影响者默认不复制一份 Host，而是持有自己的 `ESTagLeaseSet` 写入目标 Collection。

稳定身份来自 GameCore Catalog：Enum-only、String-only、双别名均允许；双别名同时存在时必须解析到同一声明。Bake 后 `RuntimeKey 1..63` 路由 Hot，`>=64` 路由 Sparse。运行时热路径传递已解析 `ESTagId` 或枚举，不得每帧按字符串查 Catalog。

所有权分为三种，不能混用：

```csharp
// Host 自身的幂等事实：只增加/撤销自身 0/1 贡献。
entity.Tags.SetTag(ESGameTag.控制类_眩晕, true);

// 外部生命周期的批量所有权：由 Buff、装备、区域等持有并在结束时 ReleaseAll。
tagLeaseSet.TryApply(entity.Tags, configuredTags, source, out error);

// 确实需要单独释放时的公开句柄。
using ESTagLease lease = entity.Tags.Acquire(tag, source);
```

`SetTag(false)` 只撤销 Host 自己的贡献，不能删除任意 Buff/装备 Lease；`Has(tag)` 判断聚合 Count，`HasOwnTag(tag)` 只判断 Host 自己的 SetTag 贡献。`ESTagLeaseSet` 使用内部值类型 Token，批量配置路径不为每个 Tag 创建托管 Lease；公开 `Acquire` 的托管对象成本是显式句柄语义的一部分。

事件只允许走 `ESTagCollection` 的 Count/Presence Link，不得恢复 Entity 的第二套 Tag C# event。规则：

- 重复订阅必须拒绝。
- 非派发期新增订阅立即生效；派发中增删在下一轮生效。
- 订阅者异常必须隔离，不能回滚已提交的 Tag 状态或阻断其他订阅者。
- 零订阅者必须跳过空派发。
- `ResetForReuse` 清除 Host 与 Lease 贡献并推进 generation；旧 Lease/Token 的延迟释放不能影响下一租用者。

不要恢复 `AddGameTag/RemoveGameTag` 一类无来源所有权 API，也不要把旧 `ESTagRefCountSet64` 当作业务主入口。`ESTagCollection` 才是 Count、条件、快照、通知与生命周期安全的唯一容器。

## RuntimeKey

RuntimeKey 是对应强类型 AssetTable 内部的运行索引协议；它必须与 AssetKind/EnumType 一起解释，不是跨资产类型的全局整数身份。

Buff、State、Skill、Item、Camera、Tag 都可以和 RuntimeKey 协作，但不能混成一个概念：

- BuffKey：这个 Buff 配置是谁。
- GameTag：实体当前拥有什么事实状态。
- RuntimeKey：当前进程、当前强类型表内的运行索引；由 ConfigKey 解析得到，不是配置身份。

不要让字符串路径直接承担 RuntimeKey 的热路径职责。

## StateMachine

状态机是当前核心竞争力之一，定位是状态语义、生命周期、动画混合、IK Pose 汇总、弱打断压制共存和编辑器预览。

已确认结论：

- 无动画状态可以存在，适合纯逻辑、标记、Buff 表现、行为门控、事件触发。
- 动画来源应区分：
  - `None`：纯逻辑。
  - `StateConfig`：使用状态配置动画。
  - `SkillTimeline`：技能编辑器轨道注入 Playable。
  - `External`：外部系统负责表现。
- 技能编辑器构建动画轨道后，状态必须能参与 Playable 混合。
- Clip Override 不应只扫运行中 State，应覆盖所有已注册且已初始化 Runtime 的缓存 State。
- Runtime 缓存复用是关键优化，但必须保证第二次进入状态和第一次进入语义等价。

状态退出后的新口径：

- 普通退出不再默认销毁整套 Playable。
- `HotUnplugStateFromPlayable` 主要断开上层连接、暂停、保留 Runtime。
- 真正销毁只应发生在状态机释放、状态对象回池、结构不兼容、显式销毁路径。
- 所有有阶段、数组、权重缓存、SmoothDamp、事件缓存、子 Runtime 的动画计算器必须实现复用复位。

旧思想已经过时：

- “退出即 DestroyPlayable，下次进入自然重建”已过时。
- “InitializeRuntimeInternal 每次进入都会重置语义”已过时。
- “缓存复用只影响性能，不影响逻辑”是错误的。复位不完整会导致状态第二次进入卡住。

性能口径：

- 最大结构成本仍是每实体 `PlayableGraph.Evaluate(deltaTime)`。
- 缓存复用显著降低状态重启成本，避免重复创建 Mixer、ClipPlayable、数组、OverrideSlot 和内部图连接。
- `GetRunningStatesSnapshot()` 是安全遍历，不是 GC 大头；它复用快照，主要是 CPU 成本。
- 后续要做 LOD/降频时，必须累计 deltaTime 后释放，并设置最大 catch-up，避免动画变慢或大跳。

商业级缺口：

- 压测和自动化验证仍不足。
- LOD 消费尚未真正接入 StateMachine。
- Clip Override 还缺来源句柄、优先级栈、事务回滚和覆盖表可视化。

## Final IK Driver

`StateFinalIKDriver` 是 Final IK 的产品化封装层。业务代码不应直接碰底层 solver。

公共 API 必须统一 `IK` 前缀，例如：

```csharp
ik.IKSetAimTargetTransform(target);
ik.IKSetAimTargetPosition(position);
ik.IKAimAt(target, targetWeight: 1f);
ik.IKSetAimWeight(0.5f);
ik.IKStopAim();
```

关键边界：

- 不要移动用户传入的目标 Transform。
- 目标 Transform、world position、peek 代理应该写内部 virtual target。
- 状态 IK Pose 和实时 IK API 不要无规则争夺同一意图。
- Final IK 复杂度不能暴露给普通业务使用者。

求解顺序目标：

1. 动画和状态机输出基础姿态。
2. Grounder 做地面探测、骨盆和脚底基础修正。
3. BipedIK / Limb IK 处理四肢定位。
4. AimIK / LookAtIK 处理瞄准和注视。
5. FullBodyBipedIK 处理全身协调。
6. HitReaction / Recoil 通过 FBBIK delegate 或受控时序叠加。
7. Debug/Gizmo/诊断只读结果，不反向驱动。

当前风险：

- `StateFinalIKDriver` 仍偏大，partial 只能缓解维护压力，不等于职责已经完全拆分。
- GrounderBipedIK 天然依赖 BipedIK solver，不能假装完全独立。
- 完全手动 Scheduler 最稳，但 Recoil/HitReaction 与 FBBIK delegate 有耦合，改动必须谨慎。
- FBBIK 商业配置还未完整覆盖 spine、肩、大腿、effector、pull/reach/push、bend、mapping、iteration、重心和武器持握。

性能规则：

- 常态运行不能受测试按钮、调试面板、字符串诊断影响。
- IK 贡献诊断必须可关，且使用采样频率、手动刷新、复用文本缓存，不要每帧 `StringBuilder.ToString()`。
- Solver 顺序显示只在诊断开启时记录，关闭时不产生额外字符串和集合分配。

## Buff

`EntityBuffDomain` 已有 Buff 实例、叠层、来源/持续时间、Tick、移除、查询、ValueChange / Permit、Op 与 Tag Lease 的运行时底座；它不是空域，也不等于完整商业 Buff 产品已经验收。

不要把 `StateLayerType.Buff` 当成 Buff 数值系统。它是动画/表现层，适合眩晕姿态、中毒循环、霸体姿态、蓄力表现。

未来高性能 Buff 应分层：

- BuffDomain：时间、叠层、来源、驱散、查询。
- ModifierSystem：属性修改、聚合、脏标记重算。
- EventRouter：攻击、受击、击杀、死亡、状态切换触发。
- VisualBridge：桥接 StateMachine Buff 层、VFX、UI、音效。
- GameTag：表达事实状态，如眩晕、沉默、禁止移动、霸体。
- RuntimeKey：连接当前表内的运行实例和缓存；配置资产仍以 ConfigKey 表达。

禁止：

- 每个 Buff 一个 MonoBehaviour。
- 每个 Buff 每帧虚函数 Update。
- 每帧全量重算属性。
- 高频字符串 Key。
- 大量 `List.Remove` 导致元素位移。

## Entity 与运动

Entity 被看作“意识生命体”的基础承载，但不要把所有系统都塞进 Entity。

当前结构理解：

- `EntityBasicDomain`：身体基础能力，移动、战斗、技能、相机、RootMotion、攀爬、游泳、飞行、交互、脚贴合等。
- `EntityAIDomain`：意识、输入、AI 调度。
- `EntityBuffDomain`：Buff 运行时域，承载已实现的实例生命周期与属性/Tag/Op 关联；完整玩法能力仍按源码和 PlayMode 验收。
- `EntityStateDomain`：状态机、动画数据包、预览、调试、IK 关系链。
- `EntityKCCData`：高频 KCC 运动核心，不是 Domain。

运动强化方向：

- KCC 的 `UpdateRotation / UpdateVelocity / AfterCharacterUpdate` 是 KCC 原生分开的回调，不要强行合并。
- 移动能力不要继续在 `EntityKCCData.UpdateVelocity()` 里堆 if。
- 攀爬、飞行、骑乘、立体机动、游泳等应通过高性能调度器注册到对应 KCC 阶段。
- 调试默认关闭，尤其每帧日志必须关。

骑乘规则：

- 骑乘不是普通移动模式加速度。
- 载具应接管运动权，但 Entity 仍要保留输入、状态、动画、相机、Buff、交互边界。
- 需要明确上下车、控制权、碰撞、动画、死亡/眩晕/强制下马规则。

飞行规则：

- 不能只做 `moveInput + verticalInput`。
- 后续要区分悬停、冲刺飞行、空中制动、朝镜头飞、朝角色飞、俯冲、空中碰撞滑移、落地切换。

## ESWorkScheduler

调度器命名应使用项目可理解词汇，避免生僻抽象。当前倾向名为 `ESWorkScheduler`，不是 `ESQuotaScheduler`。

核心定位：

- 一个轻量、可复用、0GC 的任务调度器。
- 不只用于帧，也可以用于任意“有限额度顺序执行”的流程。
- 不绑定 Entity，不绑定 Motion，不绑定 KCC。
- KCC 只是应用案例。

设计口径：

- 容量初始化 `Warmup()`，例如默认 4 个任务起步。
- 注册只需要 task 和 order。
- 添加/移除在更新中安全，延迟应用到 Reset/下次更新边界。
- 排序 dirty 后再排序，不要每帧排序。
- 额度数据直接在调度器里用 int 字段，不要封装过重的 Consume API。
- 用户任务自己决定怎么 Run，框架只负责顺序和安全更新。

KCC 应用：

- 由于 KCC 原生分 `UpdateRotation`、`UpdateVelocity`、`AfterCharacterUpdate`，可分别持有三组调度器。
- 每个模块在初始化时按 order 注册。
- 每个任务入口快速判断自身是否可用，不可用立即 return。
- 核心依赖初始化时准备好，热路径不要重复判空。

## LOD

当前设计结论：LOD 是 GameManager 的运行时模块，名为 `ESLODModule`，不是 `LODManager`。

范围：

- 通用轻量缓存，不绑定 Entity。
- 不做多态注册。
- 不在当前阶段直接接管 StateMachine / IK / AI / KCC。
- 下游系统后续自己读取 LODLevel + Gate 并决定怎么降级。

不要再添加：

- `ESEntityLODLevel`
- `ESEntityLODGate`
- Entity 专属 LOD 字段
- Entity 专属 LOD API
- LOD 模块内置 StateMachine/IK/AI 更新间隔

LOD 降频注意：

- 降频不是把动画时间缩短或放慢。
- Reduced 级别应累计 deltaTime，到更新帧一次释放。
- 必须有最大 catch-up 限制，避免远处实体突然大跳。
- Sleep 可以不跑 PlayableGraph，但要保留必要状态数据。
- Pause/Stop/Death/AlwaysFull/AlwaysSleep 等 Gate 是约束信号，不是业务系统本体。

## Debug / Diagnostics

调试功能必须帮助编辑器验证，不能污染正式运行。

要求：

- Debug 开关集中到 DebugSettings 或编辑器配置。
- 构建后不需要 Debug 内容时，应能完全关闭。
- 每帧日志、字符串格式化、`ToString()`、临时 List 都要避免。
- 诊断使用采样频率、手动刷新、缓存文本。
- 测试按钮和配置生成默认只在 Editor 或 Development 用。

## 协作关系

ESInput：

- 负责输入来源和重绑定，不应被旧输入系统兼容拖回。
- 输入可以转化为 Entity / Motion / Command 意图，但不应直接改 IK solver。

ESCommand：

- 适合作为行为命令入口，连接输入、AI、技能、交互。
- 命令层产生意图，具体运动、状态、IK 由对应系统消费。

GameManager：

- 提供全局模块和高速静态缓存，例如 LODModule。
- 模块不是 Manager 命名泛滥。

RuntimeMode / ModeTag：

- 可作为跨系统模式/标签协议参考。
- 相机、输入、状态、AI 不应各自复刻一套模式体系。

ValueChange：

- 适合属性/数值变化聚合、脏标记、来源变化。
- Buff 可借鉴，但不要强行把 Tag 引用计数做成完整 ValueChange。

Link：

- 适合对象关系、连接、接收池等，但不要为了 GameTag 引入无关 Link 改动。

State：

- 负责状态生命周期、动画、IK Pose、表现层 Buff。
- 不负责完整 Buff 数值核心。

Interaction：

- 适合承载交互意图和目标选择，不应直接硬改运动、状态、IK 的内部数据。

## 商业级判断

已经具备商业级潜力：

- 状态动画缓存复用和 SkillTimeline 注入方向先进。
- GameTag 64 核心标签 + 引用计数 + 枚举强类型热路径很适合游戏循环。
- WorkScheduler 能把旧 if 分发改成可控、有序、低成本任务链。
- FinalIK Driver 封装方向正确，能降低业务层学习 Final IK 的成本。
- LOD 模块作为全局轻量信号源，方向比各系统各造 LOD 更稳。

尚未完全商业级：

- StateMachine 缺自动化压力测试、LOD 消费、覆盖来源优先级。
- FinalIK Driver 缺完整 solver 顺序验证、Game 视图可视化、FBBIK 深配置。
- Buff 已有运行时底座，但事件覆盖、完整驱散策略、对象池 Host 重置、存档/网络恢复、玩法联调和压力证据仍不足。
- KCC 运动能力调度还需要更多真实运动模块接入验证。
- LOD 目前只提供基础模块，下游系统还没规模化消费。

## 后续 AI 改动前自检

- 我是否读了当前源码，而不是根据旧文档想当然？
- 我是否把已否定设计又写回来了？
- 我是否引入了每帧 GC 或每帧字符串？
- 我是否为了好看删除了用户需要的序列化配置？
- 我是否把 Debug/Test 放进了常态运行？
- 我是否把 Entity 专属概念强塞进通用系统？
- 我是否把 State Buff 层误当成完整 Buff 系统？
- 我是否把 Final IK solver 细节暴露给业务 API？
- 我是否明确说明了“已实现”和“建议方向”的边界？
