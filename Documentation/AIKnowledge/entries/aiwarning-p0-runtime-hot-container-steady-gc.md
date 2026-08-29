# 热路径容器预热与稳态 GC：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.runtime-hot-container-steady-gc.v1`  
`Authority`: `AIWarnings`、目标容器源码/测试、真实消费者调用链与目标平台 Profiler 证据  
`RouteKeys`: `runtime-hot-container`, `container-warmup`, `steady-state-gc`, `aiwarnings`, `p0`, `performance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `e904cb0d62ebf706d06ffcbc2667ce03e761f7eb7d5a2656fbb1ccadbd6296b3`  
`SourceSetHash`: `e904cb0d62ebf706d06ffcbc2667ce03e761f7eb7d5a2656fbb1ccadbd6296b3`  
`EntryBodyHash`: `d584a5065b4a8b253b2f179c26828d98b429c7cf90df078dae23712d7097dd56`  
`StaleWhen`: 热路径消费者、容量/工作区策略、Profiler 证据、平台后端或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留热路径 P0、预热和证据边界；本条目承载工作区/结果合同、冷路径范围、并发隔离、Profiler 签收和案例细节。Knowledge 不提供实际 0 GC 证明，也不授予 Unity/Profiler/Player 执行权限。

## 命中与结果合同

规则是否命中由调用频率、实例规模和运行位置决定，不由泛型、集合类型或序列化方式决定。实现排序、筛选、去重、批处理、索引映射、候选副本或快照前，必须明确输入所有权和原地修改语义、公共返回对象身份、工作区所有者/清理/释放、首次/稳态/扩容/异常分配、并发或重入策略，以及源码、分配计数、Profiler、Player 或目标平台签收方式。

算法辅助 List/Dictionary/索引表属于内部工作区，不能擅自替代用户要求的领域结果，也不能把可复用成本无条件转嫁调用方。单宿主且不可并发/重入时可用实例字段；并发/重入使用调用级、任务私有、分区池或显式租借 Workspace；调用方可传入批次缓冲；`ThreadStatic` 只适合同步、线程隔离、不可重入且不跨异步边界路径；禁止无隔离全局可变静态工作区。复用清逻辑内容并保留容量，只在 Trim/卸载边界缩容。

## 热路径、冷路径与预热

Update、FixedTick、KCC、StateMachine、Buff、AI、交互、池、Tag/ValueChange 和调度等热路径禁止 LINQ、反射、捕获委托、装箱枚举、迭代器、临时集合/数组、字符串构造、动态日志和异常控制流。容量、比较器、稳定 Key、索引、注册、池、订阅与缓冲提前准备；可变结果写调用方缓冲、无分配枚举或稳定 View。StringKey 既有字典查询可无显式分配，但热路径不得临时创建、裁剪、规范化或拼接，已有 Enum/RuntimeKey/Handle 时优先预解析身份。

构造、首次索引、反序列化恢复、显式重建、EnsureCapacity/Warmup、首次注册、受控扩容、批量替换/排序/压缩/原子提交、诊断、加载、装备/场景切换可在有界冷路径分配；一旦进入常态帧或按实体重复执行，就必须预热、复用、增量化或移出高频链路。预热根据稳定上限预留容量，完成索引/Key/比较器/池/订阅，并至少执行正式路径的命中/未命中或出入队操作；缓存 View 必须服从正式 Generation/Version/Handle 生命周期。

## 验证与签收

预热后分别重复采样命中、未命中、空状态、常规写入和真实业务链；关闭日志、异常注入和初始化噪声，记录调用次数、实例规模、容量、帧数和后端。数据变换还验证首次、连续稳态、容量突破、重复值、空输入及适用的并发/重入，并核对返回身份和输入修改语义。Editor 仅预检查；Player、IL2CPP 和发布需对应后端复验。只有 Profiler 证明声明范围稳定帧 `GC Alloc = 0 B` 才能声称实际 0 GC；低 GC 必须记录字节、频率、峰值、规模关系和机制原因。

`ESEnumStringMirrorMap<TEnum,TValue>` 只是案例：镜像就绪且预热后的 Enum 查询是待独立验证稳态路径；首次镜像、反序列化重建、扩容、Dense/Sparse 转换、批量提交、冲突和索引器异常属于冷路径。其双别名、Generation 和 API 不能被复制成所有未来容器的 P0 模板。

## 原文快照

迁移前台账快照：118 行、10830 字节，原始 SHA-256 `2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2e9933512b183976b29b712ab0aeb885a17c8b5b14f79417aac380781ae92edc`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`0830198981b8bce9611ad450bfa75e0fe8c59865e17f61ed884c3af7cb14467d`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-runtime-hot-container-steady-gc.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md`
