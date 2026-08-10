# ES Developer Cockpit Architecture Contract

```text
ContractId: ES.DeveloperCockpit.Architecture
ContractVersion: 0.1
ContractStatus: DesignFrozen
SourceBaselineHead: f812e104595bec256b3f9929014ec9184cb537d7
ContractContentAuthority: CurrentWorkingTree
ContractWorktreeState: AM
ContentHashAlgorithm: sha256-canonical-v1
ContentSha256: e4d235c1553660688830fe4ae8140cfd2e05786b82622ae584c9e961eabe3c84
ChangeAuthority: HumanApprovalRequired
```

`sha256-canonical-v1` 规范化算法：

1. 使用严格 UTF-8 解码，拒绝非法字节。
2. 文件禁止包含 BOM。
3. 将 CRLF 和独立 CR 统一为 LF。
4. 不执行 Unicode NFC、NFD 或其他规范化。
5. 哈希字段必须且只能出现一次。
6. 计算时仅将哈希字段值替换为 64 个 ASCII `0`，不删除整行。
7. 使用 UTF-8 无 BOM 重新编码完整内容。
8. 对完整字节流计算 SHA-256。
9. 输出小写 64 位十六进制。
10. 写入真实 Hash 后必须按相同算法重新计算并验证。

## 最终定位

> ES Developer Cockpit 是现有开发系统的统一上下文、事件和证据投影入口。
> 它消费各领域权威状态，通过稳定 ActionId 路由受控动作，并保存可恢复的本地工作区；
> 它不拥有业务状态、不直接执行领域操作，也不提供 Git、发布或外部系统回滚能力。

首个垂直切片的正式类型是 `Frame-Aligned Observation`。它提供同帧对齐的诊断快照，
不宣称严格因果追踪，也不宣称人工输入可以形成确定性 A/B。严格因果和确定性输入均延后交付。

## 五条硬约束

### 1. Runtime 与 Editor 职责边界

只有 Player/PlayMode 需要发出的事件协议进入 Runtime-safe 层；
驾驶舱 UI、工作区恢复、动作推荐和 Editor 投影全部留在 Editor。

| 契约 | 正式归属 |
|---|---|
| `ESDeveloperEventEnvelope` | `ES_Stand`，Runtime-safe 基础程序集 |
| `ESDeveloperEvidenceRef` | `ES_Stand`，Runtime-safe 基础程序集 |
| `RunId` / `CorrelationId` / `SourceId` | `ES_Stand`，Runtime-safe 基础程序集 |
| Runtime Trace Provider 接口 | `ES_Design`，Runtime-safe 契约程序集 |
| `ESDeveloperContextSnapshot` | `ES_Editor` |
| `ESDeveloperActionDescriptor` / `Request` / `Result` | `ES_Editor` / Automation 契约 |
| `ESDeveloperWorkspaceSnapshot` | `ES_Editor` |
| 下一步动作推荐模型 | `ES_Editor` |

禁止把 EditorWindow、SerializedObject、UnityEngine.Object 引用或 Editor 工作区模型
下沉到 `ES_Stand` / `ES_Design` / `ES_Logic`。

### 2. 事件身份与顺序协议

首版事件 Envelope 至少包含：

```text
EventId
RunId
CorrelationId
SourceId
SourceInstanceId
SourceEpoch
Sequence
OccurredUtc
EventKind
OwnerRef
EvidenceRef
SchemaVersion
```

排序规则：

- `SourceInstanceId` 表示逻辑 Source 的稳定身份，跨 Domain Reload、Player 重连和 Provider 重建保持稳定。
- `SourceEpoch` 在 Domain Reload、Player 重连、Provider 重建或 Source 显式重置时递增。
- `Sequence` 在 `(RunId, SourceInstanceId, SourceEpoch)` 内单调递增，并在 `SourceEpoch` 变化后从 1 重新开始。
- 全局唯一键为 `(RunId, SourceInstanceId, SourceEpoch, Sequence)`。
- 禁止只用系统时间重建同一 Source 的先后关系。

### 2.1 同帧对齐诊断，不冒充严格因果

首版运行时样本正式命名为 `ESCharacterControlFrameSnapshot`，追踪类型正式命名为
`Frame-Aligned Diagnostic Trace`。`ESGameManager.LateUpdate()` 末尾的单点采样只能证明
采样时读取到的多个领域状态位于同一个诊断快照，不能证明某个 Input 必然导致某个 KCC、
Animator 或 Camera 结果。

每个快照至少记录：

```text
FrameCount
FixedTick
SampleSequence
UnscaledTime
DeltaTime
InputUpdatedFrame
KccUpdatedFrame
AnimatorStateFrame
CameraArbitrationFrame
```

阶段没有显式权威时间戳时必须记录 `Unknown`，禁止用采样帧、系统时间或推算值伪装。
首版所有未知帧号和 Tick 统一序列化为 `-1`，并同时保存阶段有效性标记；`FixedTick`
只有在权威 FixedUpdate 边界显式递增时才有效。`SampleSequence` 在单个 Run 内从 1 单调递增。
未来只有在受控领域边界传播 `CorrelationId` / `ProvenanceId` 后，才能把对应链路升级为严格因果追踪。

### 2.2 内容、运行和证据身份必须分层

最终 Run 证据必须同时保存三类身份：

| 身份层 | 必须保存的内容 |
|---|---|
| 稳定身份 | `StableKey` / `DefinitionReference` / `EntityStableId` |
| 运行身份 | `RuntimeHandle` / `CatalogGeneration` / `PoolGeneration` |
| 证据身份 | `RunId` / `SourceEpoch` / `Sequence` |

`RuntimeHandle`、Unity InstanceId、场景对象引用和池对象引用只允许用于当次运行诊断，
不得作为跨 Domain Reload、Catalog 重建、场景切换或对象池复用后的内容身份。
正式稳定身份暂时无法取得时必须记录身份缺口，该 Run 只能保留为 `LocalEphemeral`，不得提升证据等级。

### 3. 受控 Action 与权限边界

Action 描述符至少包含：

```text
PermissionCategory
IsReadOnly
IsIdempotent
CanCancel
RecoveryKind
PreconditionPolicyId
PreconditionSpec
```

Action Availability 单独计算，不写入注册表缓存：

```text
PreconditionResult
BlockedReason
EvaluatedAt
```

动作必须经稳定 `ActionId` 路由到中央执行器；
驾驶舱只能推荐当前权限和状态允许的动作，不能通过驾驶舱扩大执行权限。
注册表只缓存静态 Descriptor，每次推荐都重新评估 Availability。

### 4. 不拥有业务状态

驾驶舱不持有领域权威状态，不直接调用领域写操作。
所有执行经现有 ES 受控执行链路由，失败必须保留证据、原因和恢复动作。

### 5. 关闭即零常驻负担

驾驶舱关闭、无活跃 Experiment Run 且详细追踪关闭时，不得保留
`EditorApplication.update`、全量扫描或常驻事件泵。
Domain Reload 后注册数量必须稳定，Repaint 不得触发全量扫描，
Profiler 必须证明追踪关闭时接近零成本。

首版允许在 `ESGameManager.LateUpdate()` 末尾保留一个受控门禁调用，但门禁关闭路径必须满足：

- 不创建集合或对象。
- 不格式化字符串。
- 不执行反射、LINQ、层级搜索或资产扫描。
- 不调用 Animator、Camera 或其他领域 API。
- 非 Development Release Player 中编译移除或由等价构建门禁完全关闭。

该单点接线只是首版低侵入观测方案，不是永久因果架构。后续允许在经过专项性能验证的领域边界增加 Provenance。

## Frame-Aligned Observation Run 契约

### 人工测量边界

首版只交付人工起步、人工停止和人工 180 度反向测量，统一称为 `Observation Run`。
人工输入曲线不可复现，因此报告不得标记为确定性实验或确定性 A/B。后续可复现输入只能经
ES 已有虚拟输入或 Command 受控路径执行，不得直接修改 `ESInputModule` 内部状态。

### 统一采样与有效性

- 速度统一使用相对地面的地面切线世界速度，单位为米/秒。
- 首版排除空中、攀爬、载具、控制权切换和移动平台样本；无法确认状态时 Run 无效，不做猜测。
- `DeadZone` 取 Run 开始时捕获的有效角色配置，缺失时使用 `0.05`，并写入不可变参数快照。
- 有效样本比例低于 `90%` 时 Run 无效；丢失阶段时间戳不等于样本无效，但必须显示为 `Unknown`。
- 所有阈值交点使用相邻有效样本的线性时间插值，禁止直接取首个越界帧作为测量时间。

### 冻结指标定义

`T90`：

- 起点是移动输入从不大于 `DeadZone` 首次越过 `DeadZone` 的插值时刻。
- 输入方向在保持阶段与起始方向点积不得低于 `0.98`，输入幅值漂移不得超过 `0.05`。
- 至少持续输入 `0.80s`；稳定速度估计值取最后 `0.30s` 有效地面切线速度的中位数。
- 终点是速度首次达到稳定速度估计值 `90%` 的插值时刻。

`停止距离`：

- 起点是移动输入从大于 `DeadZone` 回到不大于 `DeadZone` 的插值时刻。
- 释放前稳定速度复用 T90 的稳定速度估计器；释放前不足 `0.80s` 稳定输入时 Run 无效。
- 停止阈值为 `max(0.10m/s, 释放前稳定速度的 5%)`。
- 速度低于停止阈值并连续保持 `0.20s` 才视为停止；短暂穿越阈值不算完成。
- 距离使用有效地面切线速度做梯形积分，积分到 Settling Window 的起点。

`180 度反向`：

- 旧方向取触发前 `0.20s` 有效输入方向的归一化平均值。
- 新输入大于 `DeadZone`，且与旧方向点积不高于 `-0.80` 时触发。
- 分别报告旧方向投影速度过零时间和完整反向时间；过零点使用线性插值。
- 新稳定速度复用 T90 的稳定速度估计器；反向后不足 `0.80s` 稳定输入时 Run 无效。
- 新方向投影速度达到新稳定速度估计值的 `90%`，方向点积不低于 `0.90`，并保持 `0.20s` 后完成。

阈值、窗口、排除原因和实际采用的稳定速度都必须写入已终结 Run，不能只存在于 UI 文案或代码常量。

### 实测帧率证据

驾驶舱不修改帧率。每个 Run 必须记录：

```text
ActualAverageFps = 有效样本数 / 有效 UnscaledTime 总和
P50DeltaTime
P95DeltaTime
MaximumFrameInterval
FixedDeltaTime
VSyncCount
TargetFrameRate
ValidSampleRatio
```

30/60/120 仅是实测分类，不是用户声明值：平均 FPS 必须分别位于目标值的正负 `10%`，
且 P95 DeltaTime 不得超过目标帧时长的 `1.5` 倍；否则分类为 `CustomOrUnstable`。

## Evidence 与 ReloadDomain 契约

### 证据等级

`ESDeveloperEvidenceRef` 必须包含以下等级之一：

```text
LocalEphemeral
Exported
Verified
ReleaseEvidence
```

`Library/ESFramework/DeveloperCockpit/Runs/<RunId>/` 只属于 `LocalEphemeral`，可以被 Unity 或用户清理。
只有用户显式导出到受管目录后才能成为 `Exported`；测试核验和发布验收必须分别显式提升为
`Verified` 与 `ReleaseEvidence`。等级不得因文件存在或测试通过自动提升。

### Domain Reload 中断

受控 Editor 生命周期必须在 Reload 前原子写入最小终结记录。Reload 后必须：

- 不恢复旧 RuntimeHandle、对象引用、Catalog Generation 或 Pool Generation。
- 不继续向旧 Run 追加事件。
- 将未终结 Run 标记为 `InterruptedDomainReload`。
- 保证 Provider 和 Action 注册数量与 Reload 前一致且不重复。
- 隔离单个损坏 Run；损坏文件不能阻止其他 Run 加载、终结或导出。

Reload 前终结记录写入失败时必须明确报告，不能在 Reload 后把缺失记录伪装成正常完成。

## 首个里程碑退出条件

角色控制垂直切片：

```text
只读 ContextSnapshot
有界 EventEnvelope 缓存
起步 / 停止 / 180°人工 Observation Run
Input / LocalControl / AI Intent / KCC / Animator / Camera 同帧对齐快照
已终结 Run 的参数和证据不可变
Library 本地证据与显式导出证据分级
Domain Reload 前原子终结、Reload 后拒绝旧运行身份
驾驶舱关闭、无活跃 Experiment Run 且详细追踪关闭时无常驻更新
Domain Reload 后注册数量稳定
Repaint 无全量扫描
Profiler 证明追踪关闭时接近零成本
```

## ES 深度验证

### 程序集

- `ES_Stand` 无 Editor includePlatforms，是 Runtime-safe 基础程序集。
- `ES_Design` 是 Runtime-safe 契约程序集，被 `ES_Logic` 和 `ES_Editor` 引用。
- `ES_Editor` 仅 Editor，引用 `ES_Stand`、`ES_Design`、`ES_Logic`。
- `ES_Logic` 引用 `KCC`、`Cinemachine`、`Unity.InputSystem`。

### 角色控制观测链路

首条同帧观测链：

```text
ESInputModule
→ ESLocalControlService
→ EntityAIDomain
→ EntityKCCData / KCC
→ Animator
→ Camera Lease / Request
```

- 输入入口：`ESInputModule` / `ESInputService`。
- 本地控制权：`ESLocalControlService`，通过 `ControlledEntity` 和 `OnControlledEntityChanged` 暴露。
- 角色意图：`EntityAIDomain` 把输入解析为世界意图。
- 角色移动：`EntityKCCData` / `KCC`，现有 `KinematicCharacterMotor`、`ICharacterController`、`KinematicCharacterSystem` 可用。
- 表现：`Animator`。
- 相机：`ESCameraModule` 通过 Lease 暴露 `Push` / `Update` / `Release` / `TrySetLook` / `TrySetTarget`。

垂直切片只投影这些现有权威状态的同帧快照，不新增“万能 Runtime 状态持有者”。
这些阶段可能属于不同 PlayerLoop 时点；没有显式阶段时间戳或 Provenance 时，不得宣称严格因果。

## 下一步

首个垂直切片按 `Frame-Aligned Observation` 实现，并以本契约的退出条件作为验收门禁。
Camera Definition 专项、ResourcePlan、确定性输入和全域严格因果链继续留在后续阶段。
