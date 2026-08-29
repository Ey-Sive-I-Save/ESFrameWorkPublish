# ES 协作 AI 职责卡：StateMachine / FinalIK / Buff

Status: current
StableId: es.aiwarnings.architecture.state-ik-buff-boundary.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, architecture, state-machine, final-ik, buff, performance, editor
Applicability: Entity 状态机、IK 驱动、装备表现与 Buff 运行时边界
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-state-ik-buff-boundary.md`
StaleWhen: State/IK/Buff 源码、Equipment 接线、Solver 或 SourceRefs 变化
Knowledge: `es.aiwarning.state-ik-buff-boundary.v1`

## 长期约束

- `EntityStateDomain` 负责状态语义、生命周期、动画混合和 IK Pose 汇总；`StateFinalIKDriver` 负责消费最终 Pose/实时请求并统一调度 Solver；Buff 逻辑不得伪装成 `StateLayerType.Buff`，StateMachine 也不得变成 FinalIK 配置器。
- 调用链固定为 `Entity/Basic/AI/Buff/Equipment → StateDomain → StateMachine → Animator/PlayableGraph → Pose → StateFinalIKDriver → FinalIK`。状态数据注册完成后才能启动默认状态，禁止注册前抢跑。
- 业务不得直接访问 AimIK/BipedIK/LookAtIK/FullBody solver；统一经 Driver API 和内部代理 Transform。状态 Pose 与实时 Aim/Peek/Grounder/Recoil/Hit 贡献必须有明确优先级，不得双写争夺。
- Solver 缺失时，模板必须显式关闭能力；正式 Variant 开启能力必须挂齐 Solver、骨骼并由 Bind/模板验证报错，禁止 `autoAdd` 或静默 no-op 掩盖装配错误。
- 热路径避免 LINQ、字符串拼接、组件扫描和临时集合；复用 running-state snapshot 与 `SwapBackSet`，按距离/可见性/权重分档 IK 和远端实体降频。未有 Profiler 证据不得声明零 GC/性能达标。
- `EntityBuffDomain` 只拥有 Buff 实例、时长、层数、合并/刷新/移除、触发 Op 和自身 Lease；属性聚合/ValueChange/Tag 容器的权威宿主仍是 Entity。每个 ActiveBuff 只释放自己的 Lease/订阅，禁止按 Tag/属性键清理他人来源。
- Equipment 只提交挂点阶段和 IK 请求，不直接操作 Animator/Solver；Basic/AI/Buff 只发起意图。禁止删除用户可配置序列化字段或为旧 API 添加未经授权的包装。
- 当前仅冻结职责与证据边界；动画事件、最终 IK、完整 Buff 玩法、对象池恢复、PlayMode/Profiler/Player/IL2CPP/发布仍未由静态结果证明。

## 证据边界

详细链路、实时 API、Solver 契约、性能策略、Buff 分层和验收矩阵已迁移至 Knowledge，执行前仍须回读源码。
