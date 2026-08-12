# P0：ES 活跃请求仲裁协议

> 状态：现行底层设计标准；CameraDirector 首个运行时切片已存在，但不代表 Unity/PlayMode/Profiler 已验收。
> 级别：P0（新增活跃请求仲裁系统必须遵守）。
> 适用范围：镜头、控制权、UI 焦点、音频 Voice 抢占以及其他“多来源申请、集中决策、单点执行”的领域。

## 最高结论

ES 将下列流程定义为“活跃请求仲裁协议”（Active Request Arbitration Protocol）：

```text
Request（声明意图）
  -> Lease（独立租期与代际身份）
  -> Active Set（当前有效请求集）
  -> Arbitration（确定性仲裁或合成）
  -> Commit（领域唯一提交点）
  -> Executor（领域执行后端）
```

这是统一的协议、术语、安全纪律和验收标准，不是一个要求 Tag、Stat、资源、音频和相机共用的万能 `ESRequestManager<T>`。领域只在语义真正相同时复用实现；否则只复用协议。

## 规范术语

| 术语 | 唯一含义 | 禁止混用 |
|---|---|---|
| `Request` | 请求者提交的领域意图和仲裁输入 | 不等于已生效结果，不直接驱动后端 |
| `Lease` | 一次请求的独立持有与释放资格；允许 Update 时它也必须是写入门禁 | 不用裸 `OwnerId`、索引或对象引用代替 |
| `Token` | Lease 在所属宿主当前代际中的不透明定位值 | 不是稳定 Key，不是 `CancellationToken`，不进入配置、存档、网络或回放载荷 |
| `CancellationToken` | 异步操作的协作取消信号 | 不是 Lease 身份，不能替代 Release 或 Generation 校验 |
| `Generation` | 宿主、槽位或场景的运行时代际，用于拒绝旧 Lease | 不与资源发布版本、SchemaVersion 或 Pack `Version` 混称 |
| `Owner` | 请求的业务生命周期所有者 | 不单独作为租期身份，同一 Owner 可有多个 Request |
| `Active Set` | 某一仲裁域或视口当前有效的 Request 集合 | 不是恢复栈，不保存已失效请求 |
| `Arbitration` | 从 Active Set 得出 Winner 或合成结果的确定性决策 | 不在各请求者中分散修改最终状态 |
| `Commit` | 将本轮最终结果写给 Executor 的领域唯一提交点 | 不允许普通 Push/Update/Release 立即制造中间态 |
| `Executor` | 只执行已仲裁结果的领域后端 | 不自行发明业务优先级或所有权 |

`Winner` 只用于必须唯一获胜的主体请求；可叠加数值必须称为 `Modifier` 或“合成输入”，不得伪装成多个 Winner。

`Push` / `Update` / `Release` 是协议动词，不强制所有领域公开 API 采用相同方法名。领域 API 应优先使用项目已有、业务可直接理解的名称，但不得改变协议语义。

## P0 硬契约

1. 每次可独立释放的 Request 必须取得唯一 Lease，并由 `Owner + Token/Slot + Generation` 或等价不透明身份验证写入与释放。
2. 重复释放、过期释放、跨代释放和跨 Host/跨 View 操作必须失败，不得影响当前租期。值类型 Lease 的复制品也必须共享同一次释放状态。
3. Owner 销毁、回池、取消，以及场景或 Provider 代际变更时，必须使所有旧 Lease 失效。清空 Active Set 时必须同步清理 Token 或推进 Generation，禁止只清状态后复用旧 Token。
4. `Push` / `Update` / `Release` 只修改 Active Set 并标脏。领域必须在唯一 Commit 时序点清理失效请求、重算结果并驱动 Executor。
5. 普通业务不得绕过仲裁器直接写 Executor。立即提交只能是明确受限的 `FlushNow` 类场景边界，不得成为普通 API。
6. 优先级相同时必须有稳定、可重现的决胜键，不得依赖 `Dictionary` 枚举顺序、对象 Hash 或未定义的回调顺序。
7. 单个 Request、回调或 Executor 适配层异常不得破坏其他请求、污染半成品合成值或跳过必要清理。
8. 多视口、多玩家或多输出域必须先按 `ViewId` 或领域等价键隔离，再在各自 Active Set 中仲裁。
9. 诊断必须能解释 Owner、Request 类型、优先级、决胜键、失效原因、Winner/合成过程和最终提交结果；诊断关闭时不得留下字符串或集合分配。
10. 预热后的常规 Push/Update/Release 与 Commit 路径以 0 GC 为强制目标，但只能在 Unity Profiler 实测后签收，不得由静态代码直接宣称实现。

## 领域语义不得强行合并

| 领域 | 实际决策语义 | 与本协议的关系 |
|---|---|---|
| Tag | 存在性与引用计数 | 复用 Lease/代际纪律，不是 Winner 仲裁 |
| Stat / ValueChange | Modifier 按固定运算顺序合成 | 复用来源租期与可解释性，不是唯一主体获胜 |
| Resource | 加载合并、持有计数与释放 | 复用 Token/代际安全，不是业务优先级仲裁 |
| Audio | Voice 预算、并发限制与抢占 | 可按本协议对齐生命周期和确定性，保留音频领域策略 |
| Camera | Base/Shot Winner + Modifier 合成 | `Request/Lease/ViewId/SceneEpoch/Director/CM2 Adapter` 首切片已实现；Skill、Timeline、TrackView Preview、完整诊断和运行证据仍待完成 |

禁止为“看起来统一”让上述领域继承同一大型管理器。只有至少两个领域出现完全相同的 slot/generation 状态机、异常边界和清理规则时，才可抽取极小的不透明身份组件；不得提前抽取业务仲裁器。

## 现有实践与协议的关系

本协议是对 ES 已在多个领域使用的安全原则进行命名和收口，不是从 Camera 开始发明一套新原则。

| 现有领域 | 已实现的相关部件 | 当前准确口径 |
|---|---|---|
| Tag | Lease、引用计数、generation 失效、Host 清理 | 已实现生命周期安全，不属于 Winner 仲裁 |
| Stat / ValueChange | EffectLease、slot + generation、Modifier 优先级和固定合成顺序 | 已实现租期写入与数值合成，不是 Active Request Winner 模型 |
| Resource | Scope/Handle/TemporaryLease、并发加载合并和代际清理 | 已实现资源持有协议，不承担业务仲裁 |
| Vehicle | 唯一 `(seat, driver)` 权威、输入验证和四阶段能力调度 | 已有控制权仲裁实践，但尚未按六阶段术语做统一符合性审计 |
| Audio | VoiceHandle generation、Owner 回收、优先级、预算与抢占 | 已有并发仲裁实践，保留 Voice 领域语义，尚未按六阶段术语做统一符合性审计 |
| State / Input | 状态环境权威、输入路由与部分控制源仲裁 | 已有边界分层；多控制源仍是需要继续收口的领域问题 |

“统一符合性审计”只是用本文的术语和验收矩阵复核现有系统，不要为了审计而重写已稳定的领域实现。只在发现真实过期释放、多权威写入、非确定决策或无法清理时强化代码。

## Camera 现行投影（首切片，未验收）

```text
CameraRequest
  -> CameraLease
  -> ViewId Active Set
  -> Base Winner + Modifier Composition
  -> CameraDirector.LateUpdate Commit
  -> Cinemachine Backend Executor
```

- `CameraDirector` 是每个 View 的唯一镜头写入权威。玩家、AI、载具、Skill、Timeline 和 TrackView 只提交或更新 Request。
- `CameraLease` 必须绑定取得时的 View、Slot/Token、Generation 和 Scene Epoch。释放只撤销该请求，不执行“恢复上一镜头”命令。
- Cinemachine 是 Executor，只执行 Follow、LookAt、Blend、避障和输出；不承担业务仲裁。
- `ESCameraDirector` 已按 Active Set 重算 Base/Shot，并对 FOV、距离、肩偏和震动 Modifier 做显式合成；`ESCameraSceneBinding` 持有每 View 的 Scene Epoch，CM2 Adapter 是唯一 VCam 写入点。
- 玩家自由观察镜头不得使用会持续继承角色 yaw 的 BindingMode。`CameraTarget` 只提供位置与激活时的初始朝向参考，玩家 FreeLook 必须使用 `LockToTargetOnAssign`；载具追逐镜头可按明确设计使用 `LockToTargetWithWorldUp` 跟随车头。
- 当前已有 Director 核心编辑器测试源码与运行时 Skill Camera Clip（池化 UserData 释放 Lease），但尚无 Unity Test Runner、PlayMode、Profiler 或 Player/IL2CPP 证据；TrackView 独立 Preview、Timeline 与载具镜头仍待实现。禁止宣称“ES 相机系统已交付或冻结”。

## 验收矩阵

每个落地本协议的领域至少覆盖：

1. 两个独立 Lease 与 Lease 复制品乱序释放；
2. Owner 回池后旧 Lease 更新和释放；
3. 清空或安全点后同 Slot/同身份重用；
4. 目标销毁、请求取消、场景和 Provider 转换；
5. 同优先级、不同注册/释放顺序下结果一致；
6. 仲裁中增删请求在规定轮次生效；
7. 单请求异常、合成半成品回滚与后续请求继续执行；
8. 多 View/多领域隔离；
9. 旧的绕过链路彻底关闭；
10. 预热后常规帧 Profiler GC 与 CPU 预算。

## AI 实施前必答

1. 该领域的 Active Set 由谁唯一拥有？
2. 唯一 Commit 时序点和 Executor 是什么？
3. Winner 规则、Modifier 顺序和同优先级决胜键是什么？
4. Lease 如何拒绝复制释放、跨 Host/View 和跨代操作？
5. Owner 销毁、回池、异常与场景转换由谁清理？
6. 是否真有两个语义完全相同的领域支持抽取公共组件？

任一答案不清楚时，不得开始新的仲裁器、通用管理器或后端直写 API。
