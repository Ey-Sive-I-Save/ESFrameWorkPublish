# Codex 核心上下文：State、IK、Tag、RuntimeKey 与 LOD 边界

`KnowledgeId`: `es.aiwarning.p0.codex-core-context.v1`  
`Authority`: `AIWarnings + current core protocol source`  
`RouteKeys`: `aiwarnings`, `p0`, `codex-context`, `statemachine`, `final-ik`, `gametag`, `runtimekey`, `lod`, `performance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `5405e29b7288ae05974c903c724f871f833c54867d340bf87adf0f20b6f926f7`  
`SourceSetHash`: `5405e29b7288ae05974c903c724f871f833c54867d340bf87adf0f20b6f926f7`  
`EntryBodyHash`: `44e5cfee8d3bafdb77a5e8783ad296f5e36a9055e68f4b13e2a6f6d85c63a0e0`  
`StaleWhen`: `State/IK/Tag/RuntimeKey/LOD 源码、核心协议或 SourceRefs 变化。`

## 迁移说明

原 Warning 379 行、18,098 UTF-8 字节；现 Warning 保留 P0 初始化、热路径、统一身份、状态/IK、LOD 与证据边界。详细架构、历史纠偏、API 和验收矩阵迁移至本条目，原文与当前源码可由 SourceRefs 回溯。

## 核心协议

- 热路径（Update/KCC/IK/State Evaluate/Buff Tick）禁止 LINQ、字符串、反射、扫描、临时集合、动态扩容和装箱；核心依赖在初始化阶段断言/失败。调试日志、测试按钮和诊断文本必须可关闭且不污染常态路径。
- `ESTagCollection` 是 Entity/Item 的聚合事实容器；外部影响者使用带来源的 `ESTagLeaseSet`，只释放自身贡献。SetTag(false) 不能删除他人 Lease，`Has` 看聚合计数，`HasOwnTag` 看 Host 贡献，ResetForReuse 清理并推进 generation，旧 Token 不得影响新租用者。
- RuntimeKey 是当前强类型 AssetTable 的运行索引，必须与 AssetKind/EnumType 一起解释；BuffKey、GameTag、StringKey 和配置身份各自独立。Bake 后 Hot/Sparse 路由使用已解析 ID，禁止每帧字符串查 Catalog。

## StateMachine 与 FinalIK

- StateMachine 负责状态语义、生命周期、动画混合、IK Pose 汇总和弱打断；状态包注册完成后再启动默认状态。运行时缓存、Mixer、Clip、数组、OverrideSlot 和子 Runtime 必须完整复位，普通退出优先热断开并保留 Runtime，不默认 DestroyPlayable。
- Driver 是 FinalIK 产品化封装，业务只能经统一 `IK*` API 和内部 virtual target；不得直接访问 AimIK/BipedIK/LookAtIK/FullBody solver，不得移动用户传入的目标 Transform。状态 Pose、Grounder、Limb、Aim/LookAt、Recoil/Hit 必须有固定顺序与优先级。
- Solver 缺失时 Variant 必须显式关闭能力或在 Bind/模板门禁硬失败；禁止 autoAdd 和静默 no-op。Driver 偏大、FBBIK 配置和手动调度仍属风险，不能写成已完整商业化。

## LOD、Editor 与证据

- LOD/降频只在正式消费链路接入后才有效；累计 deltaTime、最大 catch-up，避免动画变慢或跳帧。不要新增 Entity 专属 LODManager 或第二套 Mode/Tag/Key。
- Inspector 以中文标题、分区、折叠和可见开关组织，不以“降噪”删掉用户配置字段；双键身份按高频强类型/扩展字符串初始化转换原则使用。
- 需要逐项验证状态二次进入等价、Tag Lease 隔离、RuntimeKey 表路由、IK 缺失失败、LOD 大规模压测、编辑器可用性、目标平台 GC、PlayMode/Player/IL2CPP/发布；当前均未运行。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/_EntityStateDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/_EntityBuffDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_Codex核心上下文总纲_状态机IK标签调度LOD_AI协作警告.md` (`01f6ed792f732746ece9d853fda41c131ea289d3a8c114daa445a30ab2427d65`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/_EntityStateDomain.cs` (`59bc2cb164538f00824ebb75e6eb9f1b101e624bd99e0f3f2716ec62382eb046`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/_EntityBuffDomain.cs` (`95e0ae56541c0410867a8de6f18f67f70eb04ae2b0026d8a0f6a559a40305af4`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` (`397d0c465f6b59069e388445d6d5724d190a0d08ec3d1719bfdfc9c6a1418c46`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
