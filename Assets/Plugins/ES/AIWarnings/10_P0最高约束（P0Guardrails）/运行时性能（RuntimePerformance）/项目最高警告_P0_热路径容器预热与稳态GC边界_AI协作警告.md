# P0：热路径容器预热与稳态 GC 边界

Status: current
StableId: es.aiwarning.p0.runtime-hot-container-steady-gc.v1
Authority: AIWarnings（长期 P0 约束）；详细工作区与验收合同见 Knowledge
RouteKeys: runtime-hot-container, container-warmup, steady-state-gc, aiwarnings, p0, performance
Applicability: 进入 Update/FixedTick/KCC/StateMachine/Buff/AI/交互/池/调度等高频链路的容器、索引、缓存、Registry、Queue、Set 与复用缓冲
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-runtime-hot-container-steady-gc.md
StaleWhen: 热路径消费者、容量/工作区策略、Profiler 证据、平台后端或任一 SourceRef 哈希变化。

## P0 长期约束

- 本规则由调用频率、规模和运行位置触发，不因泛型、List/Dictionary/HashSet/Queue 或序列化形态自动触发；编辑器、一次性初始化和低频管理操作不自动承担稳态 0 GC 合同。
- 预热后的常规稳态操作以 `GC Alloc = 0 B` 为目标；确需低 GC 必须记录来源、频率、字节上限和实测证据。构造、首次注册、扩容、重建、批量提交、诊断和快照只能在明确冷路径，不能按帧或按实体反复发生。
- 高频分支禁止 LINQ、反射、闭包/捕获委托、装箱枚举、迭代器、临时数组/集合、字符串拼接/格式化、异常控制流和动态日志；容量、Key、比较器、索引、池、订阅和复用缓冲在进入热路径前准备。
- 可变数量结果优先写入调用方复用缓冲、无分配枚举或稳定只读 View；不得默认返回新集合。调用方 Key 构造、转换、事件、回调和结果复制必须与容器一起归因。
- 工作区所有权、输入可否原地修改、返回对象身份、首次/稳态/扩容分配、并发/重入隔离和验证方式必须在实现前声明。禁止无隔离全局静态工作区；复用时清逻辑内容并保留容量，不能残留业务状态或成为第二份权威。
- 只有目标场景/平台 Profiler 在声明范围证明稳定帧 `GC Alloc = 0 B` 才能声称实际 0 GC；源码、单元测试、编译、Benchmark 或 Editor 单次采样不能替代 Player/IL2CPP/目标平台证据。

## Knowledge 导航

完整冷/热路径、预热、工作区、并发、结果合同、`ESEnumStringMirrorMap` 案例和签收矩阵见 `es.aiwarning.p0.runtime-hot-container-steady-gc.v1`。本 Warning 不授予 Unity、Profiler、Player 或性能结论权限。
