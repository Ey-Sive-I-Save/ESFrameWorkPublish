# 项目最高警告：Codex 核心上下文总纲

Status: current
StableId: es.aiwarnings.p0.codex-core-context.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, p0, codex-context, statemachine, final-ik, gametag, runtimekey, lod, performance
Applicability: AI 读取、设计、修改和验证 ESFramework 架构时的核心边界
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-p0-codex-core-context.md`
StaleWhen: State/IK/Tag/RuntimeKey/LOD 源码、核心协议或 SourceRefs 变化
Knowledge: `es.aiwarning.p0.codex-core-context.v1`

## P0 长期约束

- ESFramework 优先保证初始化可信、热路径低/零 GC、中文可用、统一协议和可商业化扩展；文档必须区分已实现、未验证、建议方向。
- Update、KCC、IK、StateMachine Evaluate、Buff Tick 禁止字符串/LINQ/临时集合/扫描/反射；核心依赖缺失应初始化断言或失败，不用大量判空掩盖。不得未经授权保留旧 API 包装或撤掉可配置字段。
- `ESTagCollection` 是 Entity/Item 按需持有的聚合事实容器；Buff/装备/区域以 `ESTagLeaseSet` 写入并只释放自身来源。SetTag(false) 不得清除他人 Lease，ResetForReuse 必须推进 generation；禁止第二套 Tag event 或无来源 Add/Remove API。
- RuntimeKey 只解释当前强类型表内的运行索引，必须与 AssetKind/EnumType 同解；不得把 StringKey、BuffKey、GameTag 和 RuntimeKey 混为一体或每帧按字符串查表。
- StateMachine 负责状态语义、生命周期、动画混合、IK Pose 和弱打断；状态运行时缓存必须等价复位，普通退出不默认销毁 Playable。`StateFinalIKDriver` 统一消费 Pose/实时请求，业务不得直碰 FinalIK solver。
- LOD/降频必须累计 deltaTime 并有最大 catch-up；不得把实体专属 LOD、第二套 Mode/Tag/Key 或未接线能力写成当前事实。调试/诊断不得进入常态热路径。
- Inspector/Editor 保持中文分区、折叠和可见开关；静态、编辑器、运行时、Profiler、Player、IL2CPP 和发布证据严格分层。

## 证据边界

详细 Tag/RuntimeKey/StateMachine/FinalIK/LOD 事实、历史纠偏、性能策略和验收矩阵迁移至 Knowledge；执行前必须回读当前源码，静态结果不得证明运行时或发布。
