# Unity 3D 物理运动执行与查询边界（2022.3.45f1）

`KnowledgeId`: `es.unity.physics-motion-authority.v1`

`Authority`: `Unity 2022.3 official documentation + installed API documentation + project settings + current source + AIWarnings`

`RouteKeys`: `unity`, `physics-3d`, `fixed-update`, `fixed-step`, `rigidbody`, `kinematic-rigidbody`, `collider`, `trigger`, `physics-query`, `raycast`, `cast`, `overlap`, `layer-mask`, `query-trigger`, `transform-sync`, `interpolation`, `single-writer`

`ContentHash`: `cd0dd576fed08a778ae98eaa6df9d4c4a6747a633cf2009158d5aa7d92943804`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity 版本或官方来源响应、Time/Physics/Layer 项目设置、ESPhysicsLayers、ESPhysicsQueryModule、物理查询测试定义、AIWarnings 运动规则或任一 SourceRef 哈希变化。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `ProjectSettings/TimeManager.asset` (`1a83e54adbbda7c9f4851103a0c6ab7f6448a3343d4ea5b7620452fa08416ecd`)
- `ProjectSettings/DynamicsManager.asset` (`d2335e2eda7611069b27c87e7956b4f1846523ec6ef7dfbff1346375435201e8`)
- `ProjectSettings/TagManager.asset` (`dffef711c8f47c6c295932e08d3185123342f637934d2b9db3908a3c63050068`)
- `Documentation/AIKnowledge/Unity/unity-physics-motion-authority/official-source-lock.md` (`e6136f94be9db78fcc841e406996c7e391275a26d0242ac1ffd4c36b89710d06`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/玩家运动_PlayerMotion_AI协作说明.md` (`5657d82f084de45029e861ae669eeea0c45c54f11a65c8c9cc00fb99f56c6b0d`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESPhysicsQueryModule.cs` (`aa159770e0d72149bd899e05b6173ea5491cf64fd7d5195b4e4c0645ae7cfbab`)
- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESItemInstanceTableTests.cs` (`6d16382376a4c93639b9112d203bda36979c9ccd7b5dfa4f69816fd5a9e5fcc2`)

## EvidenceRefs

- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESItemInstanceTableTests.cs` 只证明命中排序、Owner Collider 排除和缓冲饱和回退等测试定义存在；没有本次 Unity Test Runner 回执。

## 1. Scope

本条目是通用 Unity 3D 物理执行边界的 canonical owner，负责三个相互关联的决策面：

1. `FixedUpdate`、固定步、Rigidbody 插值和 Transform/Physics 同步；
2. Transform、动态 Rigidbody、Kinematic Rigidbody 与 KCC 的单写入者选择；
3. Collider/Trigger、Cast/Overlap/Query 的 Layer、Trigger、缓冲、排序和溢出处理。

本条目不负责具体业务运动实现：

- Entity KCC 根运动、接地、移动平台、传送和运动影响由 `es.project.entity-kcc-motion.v1` 负责。
- Vehicle、Mount、驾驶输入和 Rigidbody/KCC 后端提交由 `es.project.vehicle-mount-motion.v1` 负责。
- Item/Shot 的飞行、命中候选、阻挡和生命周期按 Item/Shot AIWarnings 与当前源码处理。
- 2D Physics、DOTS Physics、关节、布料、布娃娃、网络预测、手感调参和具体 Prefab/Scene 配置不在本条目范围。

边界原则是一事实一归属：本条目只拥有 Unity 通用机制与项目查询基础设施；相邻条目只链接这些底层条件，不复制本条目的 API 摘要。

## 2. Trigger and routing

### 自然语言触发

`Rigidbody 抖动/插值`、`FixedUpdate 和 Update`、`直接写 Transform`、`MovePosition`、
`Collider/Trigger 不触发`、`Raycast/SphereCast/CapsuleCast/BoxCast`、`Overlap`、
`LayerMask`、`QueryTriggerInteraction`、`Physics.SyncTransforms`、`NonAlloc 缓冲满了`、
`碰撞结果顺序`、`物理单写入者`。

### 精确路由

Canonical routeKeys 为：`unity, physics-3d, fixed-update, fixed-step, rigidbody,
kinematic-rigidbody, collider, trigger, physics-query, raycast, cast, overlap, layer-mask,
query-trigger, transform-sync, interpolation, single-writer`。

预期命中规则：

- 通用 Rigidbody/Collider/Query 问题只命中本条目。
- 明确出现 Entity/KCC/接地/移动平台时，优先命中 `es.project.entity-kcc-motion.v1`，本条目只补充 Unity 底层边界。
- 明确出现 Vehicle/Mount/驾驶时，优先命中 `es.project.vehicle-mount-motion.v1`，本条目只补充 Rigidbody/Query 条件。
- 一个目标最多加载 1～3 个条目；只因出现宽泛的 `motion`、`runtime` 或 `lifecycle` 不得加载本条目。

可能误命中：角色 KCC 被 `fixed-step` 带入，载具被 `rigidbody` 带入，Shot 被 `raycast` 带入。
回退时先按对象和最终写入者裁决：Entity Motor -> KCC 条目；Vehicle Controller -> Vehicle 条目；
通用 Unity API/Physics Query -> 本条目。仍无法确定对象或 owner 时停止自动实施，请求目标组件、后端和提交阶段。

## 3. Decision rules

### 可以继续

- 已明确对象、物理后端、最终位姿写入者、提交阶段、LayerMask、Trigger 策略和缓冲 owner。
- 全部 SourceRef 与 ContentHash 当前一致，KnowledgeIndex 只有一个匹配绑定。
- 结论只到 S1 静态机制/源码事实，或另有与当前 HEAD、场景、平台绑定的真实运行回执。

### 必须先读额外来源

- Entity/KCC：读取 `es.project.entity-kcc-motion.v1` 及当前 Entity/KCC 源码。
- Vehicle/Mount：读取 `es.project.vehicle-mount-motion.v1` 及当前 Controller/Mount 源码。
- Item/Shot/Weapon：读取 Item/Shot AIWarnings、命中解析和调用方源码，不能把 Query 命中直接当最终伤害。
- 修改 Prefab、Scene、Layer 或碰撞矩阵：读取正式资产、TagManager、DynamicsManager、资产事务规则和匹配 TaskContract。
- 声称碰撞、Trigger、平滑度、GC 或 Player 可用：请求 Unity Test Runner、PlayMode、Profiler 或目标 Player 的当前回执。

### 必须停止、降级或重新规划

- 任一 SourceRef 缺失/漂移、索引重复、RouteKey 冲突或 Unity 版本变化：标记 `stale`，丢弃旧计划并回读来源。
- 同一根对象存在 Transform、Rigidbody、KCC、动画、网络或表现的第二位姿写入者：`Blocked`，先收敛 owner。
- 不知道查询是否需要 Trigger、使用 `~0`/默认层、共享缓冲被跨调用持有、结果达到容量但无溢出策略：`Blocked`。
- 只有组件/按钮/Prefab/测试源码存在，没有执行回执：保持 `S1` 与 `runtime-not-run`。
- 缺少匹配 AICommand 为 `NoMatchingCommand`；AIBrain 计划能力不可用为 `PlanTaskUnavailable`。二者不得互换，也不得扩权。

## 4. Verified facts

- `[S1][Project settings]` 当前项目声明 Unity `2022.3.45f1`，固定时间步为 `0.02` 秒；Physics 自动模拟开启、自动 Transform 同步关闭、全局查询默认命中 Trigger，gravity 为 `(0,-9.81,0)`。这些是配置值，不是运行测量。
- `[S1][Unity official/API]` `Time.fixedDeltaTime` 控制 Physics/FixedUpdate 的固定游戏时间间隔；固定步与渲染帧不是一一对应。
- `[S1][Unity official/API]` `Rigidbody.interpolation` 管理运行时 Rigidbody 运动的视觉抖动；`MovePosition` 是 Kinematic Rigidbody 的位姿提交 API。插值不把 Update 变成物理步，也不证明碰撞正确。
- `[S1][Unity official/API]` `Physics.SyncTransforms()` 将 Transform 变化应用到物理引擎；项目当前关闭 `autoSyncTransforms`，因此“改 Transform 后立即查询”不能假设物理世界已自动同步。
- `[S1][Unity official/API]` Query API 接收 LayerMask 与 `QueryTriggerInteraction`；`UseGlobal` 服从 `Physics.queriesHitTriggers`，`Collide/Ignore` 显式覆盖全局设置。
- `[S1][Current source]` `ESPhysicsLayers` 是项目 3D Layer 数值和语义 Mask 的当前代码入口；`GetShotHitMask` 会把历史 `~0` 收窄为 `ShotHitMask`，避免扫描自身、交互盒和纯表现 Collider。
- `[S1][Current source]` `ESPhysicsQueryModule` 通过调用方或共享数组使用 Raycast/SphereCast/Overlap NonAlloc 查询，显式接收 Trigger 策略，归一化方向并把负距离/半径收敛到零。
- `[S1][Current source]` 查询返回数量达到缓冲容量时模块增加 `overflowCount`；最近命中由显式遍历选取，不依赖返回数组顺序。
- `[S1][Current source]` `ESPhysicsQueryModule` 的共享 Raycast/Collider 数组是公开复用缓冲，并没有在模块层提供租借、并发隔离或跨调用持有保护；`count >= capacity` 目前只记录 overflow，不会自动扩容或重试。调用方必须在同一调用边界消费并清理结果，遇到饱和时自行扩容、重试、降级或阻断，不能把 overflow 计数等同于结果完整。
- `[S1][Test definition]` 当前测试源码定义了射线候选按行进距离选择、Owner Collider 排除和饱和缓冲回退用例；测试源码存在不证明本次已运行或通过。
- `[P0][AIWarnings]` Unity Layer 只做物理粗过滤，不表达阵营、友伤、无敌或玩法身份；高频查询使用明确语义 Mask、固定缓冲和 NonAlloc 路径，不能把 `~0` 当正常默认。

## 5. Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作与恢复 | 仍缺证据 |
|---|---|---|---|---|
| 在 `Update` 和 `FixedUpdate` 同时写刚体 | 抖动、速度被覆盖；未声明 writer/phase | 列出每个 Transform/Rigidbody/Motor 写入点 | 只保留一个最终 writer；输入采样与物理提交分阶段 | 多帧/多固定步 PlayMode |
| 对同一对象混写 Transform 与 Rigidbody/KCC | 穿透、回弹、插值错位；后端争权 | 搜索 position/rotation、Move、velocity、Motor 写入 | 按动态 Rigidbody、Kinematic Rigidbody、KCC 或纯 Transform 四选一；清除旧写入 | 碰撞与中断实跑 |
| 把插值当物理修复 | 画面变平滑但碰撞/控制仍错 | 区分渲染姿态和物理姿态 | 先修固定步和 owner，再按相机距离决定插值 | 目标帧率观察 |
| 改 Collider Transform 后立即 Query | 偶发查到旧位置；当前 autoSyncTransforms 关闭 | 检查变更 API、查询时机和同步 owner | 改在正确物理阶段提交；确需即时查询时由单一 owner 调用 SyncTransforms | 场景查询实跑与成本 |
| 省略 LayerMask 或使用 `~0` | 命中自身、Trigger、交互盒或表现 Collider | 每个 Query 标注业务 Mask | 使用 `ESPhysicsLayers`/`ESPhysicsLayerConfig` 语义 Mask；回退时撤销错误命中 | Layer/矩阵实机 |
| 使用 `UseGlobal` 却假设忽略 Trigger | 换项目设置后命中集合变化 | 检查 QueryTriggerInteraction 与全局配置 | 业务查询显式传 `Ignore` 或 `Collide`；只在确实接受全局策略时用 `UseGlobal` | Trigger 矩阵实跑 |
| 认为 NonAlloc 返回已排序且完整 | 最近目标错误、缓冲满后静默漏命中 | 检查排序、count、capacity 和 overflow | 显式选择/排序；count 达容量时扩容、重试、降级或报告不完整 | 压力场景/Profiler |
| 用 Collider 计数当逻辑对象计数 | 多 Collider 对象重复伤害/进入退出失衡 | 解析 attachedRigidbody/逻辑 owner 与去重键 | 物理层输出候选，上层按稳定对象身份去重和裁决 | 多 Collider PlayMode |
| 依赖 Trigger Exit 完成全部清理 | Disable/Destroy/回池后残留 owner | 声明 OnDisable/OnDestroy/Pool 清理路径 | Trigger 只更新接触；生命周期 owner 提供幂等兜底清理 | 中断/回池实跑 |
| 把测试文件或组件存在写成通过 | 报告可用但无 Test Runner/场景回执 | 检查证据层、HEAD、场景、时间和结果 | 降级 S1、标记 runtime-not-run，补真实回执 | Unity/Test Runner/Player |

## 6. Execution checklist

### 开始前

- 读取 `AGENTS -> AIBRAIN_ENTRY -> KnowledgeIndex`，只加载本条目及最多两个对象专项条目。
- 读取 AIWarnings Start、CurrentStatus、RuleIndex、运动/物理专项和当前调用方源码。
- 重算 SourceRef、ContentHash、唯一索引绑定；检查工作树重叠和当前授权。
- 写明对象、backend、writer、phase、输入来源、LayerMask、Trigger、buffer owner/capacity、溢出和取消策略。

### 实施中

- 输入可在 `Update` 采样，物理位姿只由选定后端在其正式阶段提交。
- 动态 Rigidbody、Kinematic Rigidbody、KCC、纯 Transform 不并行争写；网络/动画/表现只能提交请求或展示结果。
- 每个 Query 显式传语义 LayerMask 和 Trigger 策略；NonAlloc 结果显式处理数量、顺序、去重、溢出和共享缓冲生命周期。
- Trigger/Collision 状态覆盖重复进入、多 Collider、Disable、Destroy、回池、取消和幂等清理。

### 完成后

- 运行严格 UTF-8、SourceRef、ContentHash、Entry/Index、路由探针和相关 Skill 静态验证。
- 检查没有第二 writer、`~0`、隐式 `UseGlobal`、跨调用保留共享缓冲或把 count==capacity 当完整结果。
- 需要可用性结论时，在目标 Fixture 中执行 FixedUpdate/Update 比例、碰撞、Trigger、即时 Query、插值、缓冲饱和和中断恢复。
- 性能声明另取目标平台 Profiler；Player/IL2CPP/发布声明另取对应回执。

明确禁止：把静态检查当 Unity 运行、把 Layer 当业务身份、用插值遮盖 owner 错误、用默认参数掩盖 Query 决策、用测试源码冒充执行成功、无匹配权限扩大修改范围。

## 7. Evidence boundary

Static S1 可以证明当前 Unity/Physics 配置、官方 API 语义、ESPhysicsLayers/QueryModule 源码控制流、测试定义、SourceRef、ContentHash 和路由注册。它不能证明任何场景或 Prefab 的实际 Collider/Rigidbody 配置、FixedUpdate 时序、碰撞/Trigger 结果、插值手感、同步成本、缓冲容量、GC Alloc、Unity 编译、Test Runner、PlayMode、Profiler、Player、IL2CPP 或发布状态。

当前结论：`runtime-not-run`。没有与当前 HEAD、目标场景和平台绑定的真实回执时，只能说“静态注册完成、源码/测试定义支持”，不得说“Unity 物理功能已验收、性能达标或可发布”。
