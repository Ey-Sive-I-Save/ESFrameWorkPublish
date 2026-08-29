# StateMachine、FinalIK 与 Buff 运行时边界

`KnowledgeId`: `es.aiwarning.state-ik-buff-boundary.v1`  
`Authority`: `AIWarnings + current State/IK/Buff source`  
`RouteKeys`: `aiwarnings`, `architecture`, `state-machine`, `final-ik`, `buff`, `performance`, `editor`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `032047e122d6d2f1b075009eec0624f59036c56265b84aba903e06d8243c2ee1`  
`SourceSetHash`: `032047e122d6d2f1b075009eec0624f59036c56265b84aba903e06d8243c2ee1`  
`EntryBodyHash`: `fd7f7204c2b875a283ad5f6ca755ec0f7828a4adfbd30203a314dc9b65caec23`  
`StaleWhen`: `State/IK/Buff 源码、Equipment 接线、Solver 或 SourceRefs 变化。`

## 迁移说明

原 Warning 336 行、15,364 UTF-8 字节；现 Warning 保留 State/IK/Buff 所有权、性能与证据边界。详细调用链、Solver/API 契约、Buff 语义和验收清单迁移至本条目，原文及源码由 SourceRefs 回溯。

## State 与 IK

- 链路为 `Entity/Basic/AI/Buff/Equipment → EntityStateDomain → StateMachine → Animator/PlayableGraph → state Pose → StateFinalIKDriver → FinalIK`；状态注册完成后才能启动默认状态。
- StateMachine 负责状态语义/生命周期/动画混合/IK Pose 汇总；Driver 负责最终 Pose 与实时请求的统一 Solver 调度。Equipment 只提交 Attachment 阶段和 IK 目标，不能直接写 Animator 或 Solver。
- 业务代码不触碰 AimIK/BipedIK/LookAtIK/FullBody solver；通过统一 Driver API 和内部代理 Transform。状态姿态与 Aim/Peek/Grounder/Recoil/Hit 等实时贡献必须明确优先级，不能双写。
- 正式 Variant 开启任一 IK 能力时必须具备对应 Solver、骨骼和性能预算；缺失必须在 Bind/模板验证报错，不能 autoAdd、静默 no-op 或把 Driver 当 Solver。

## 性能与 Buff

- 复用 running-state snapshot 与 `SwapBackSet`，不要替换为会破坏遍历安全的普通 Remove；热路径避免 LINQ、字符串、扫描、临时集合和无意义 Solver 更新，远端实体与 IK 按距离/可见性/权重降频。Profiler 未执行时不得声称 0 GC。
- BuffDomain 已有实例、时长、层数、合并/刷新/移除、Op 和 Lease 入口，但这不等于完整商业 Buff。属性聚合/ValueChange/Tag 容器的权威宿主是 Entity；ActiveBuffRuntime 只释放自己的 EffectLease、Tag Lease 和 Context 订阅。
- `StateLayerType.Buff` 仅表达中毒/眩晕/霸体/蓄力等动画表现，不是 Buff 逻辑系统。未来分层为 BuffDomain（逻辑时间/来源）、ModifierSystem（属性聚合）、EventRouter（触发）、VisualBridge（表现）。

## 验收边界

需逐项验证状态注册/启动、IK 贡献优先级、Solver 缺失失败、动态装备过渡、Buff 对象池重置/Lease 隔离、PlayMode、目标平台 Profiler、Player、IL2CPP 和发布；当前均未运行。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/_EntityStateDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/_EntityBuffDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/状态机与IK（StateIK）/AI协作职责_状态机与IK上层_Buff边界说明.md` (`c86832d48e0eefbbeed6cba0fe85ff607cd861e4b3fa1ee05c5ae312ad1ee3fc`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/_EntityStateDomain.cs` (`59bc2cb164538f00824ebb75e6eb9f1b101e624bd99e0f3f2716ec62382eb046`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/_EntityBuffDomain.cs` (`95e0ae56541c0410867a8de6f18f67f70eb04ae2b0026d8a0f6a559a40305af4`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs` (`9a05fd7f643fc9dfc2d9e359a178cb133e9e0ef3cb3de9e9ab7901b6078b8d76`)
