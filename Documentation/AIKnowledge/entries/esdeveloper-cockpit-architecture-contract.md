# ES Developer Cockpit 架构合同

`KnowledgeId`: `es.aiwarning.esdeveloper-cockpit-architecture-contract.v1`  
`Authority`: `AIWarnings + current Cockpit contract`  
`RouteKeys`: `aiwarnings`, `cockpit`, `architecture`, `runtime-editor-boundary`, `event`, `action`, `observation`, `evidence`, `reload`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `bdd92f2eabe276a7d02b9046a27d4908b6b5e4b73221c252e7037048de7adfaf`  
`SourceSetHash`: `bdd92f2eabe276a7d02b9046a27d4908b6b5e4b73221c252e7037048de7adfaf`  
`EntryBodyHash`: `e50bad0c7dbacb20c2a03a1410c4ea0e1a93f0ff3e92fda33d9fe51284f3f182`  
`StaleWhen`: `Cockpit ContractVersion、事件/Action Schema、Observation Run、Domain Reload 或 SourceRef 哈希变化。`

## 迁移说明

原 Contract 317 行、12,926 UTF-8 字节；现 Warning 仅保留 Runtime/Editor、事件身份、Action 权限、业务状态所有权、关闭零常驻和证据等级门禁。本条目承接 Frame-Aligned Observation、事件排序、身份分层、性能与 Reload 规则。

## 核心边界

- Cockpit 是开发上下文、事件和证据投影入口；消费领域权威状态、按稳定 ActionId 路由受控动作、保存可恢复本地工作区，但不拥有业务状态、不直接执行领域写操作、不提供 Git/发布/外部回滚。
- Runtime-safe 层只放 Player/PlayMode 事件、EvidenceRef、Run/Correlation/Source 身份和 Trace Provider；ContextSnapshot、Action Descriptor/Request/Result、Workspace 与推荐模型留在 Editor。不得把 EditorWindow、SerializedObject 或 UnityEngine.Object 下沉。
- Event 全局身份为 `(RunId, SourceInstanceId, SourceEpoch, Sequence)`；Epoch 在重连/重建/重置递增，Sequence 在 Epoch 内单调递增，不得只用系统时间排序。
- 首版是 Frame-Aligned Diagnostic Trace，不宣称严格因果或确定性 A/B；未知阶段时间戳写 `Unknown`/`-1`，只有显式 Provenance 才能升级因果。
- 稳定身份、运行身份和证据身份分层；RuntimeHandle、Unity InstanceId、场景/池对象引用不得跨重载或重建充当内容身份。
- Action Descriptor 声明权限、只读、幂等、取消、恢复和前置条件；Availability 每次重新评估，Cockpit 只能推荐被授权动作，不能扩大权限。
- 关闭且无活跃 Run/详细追踪时，不得保留全量扫描、常驻事件泵或 update；重绘不触发扫描，性能/Profiler 结论必须有对应证据。

## Observation 与证据

首版 Observation Run 由人工起止和 180 度反向测量组成，不得写成确定性实验。速度、DeadZone、有效样本比例、T90、停止距离、反向阈值、FPS 统计和 Unknown 状态必须写入已终结 Run。证据等级 `LocalEphemeral/Exported/Verified/ReleaseEvidence` 只能按显式导出、测试或发布验收提升，不能因文件存在自动提升。

## Reload 门禁

Reload 前原子写最小终结记录；Reload 后拒绝旧 RuntimeHandle/引用/Generation、停止向旧 Run 追加、标记 `InterruptedDomainReload`，保持注册数稳定并隔离损坏 Run。写入失败必须显式报告，不能伪装正常完成。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESDeveloperCockpit_ArchitectureContract.md` (`90a6a6d3cc442dd7c288d1c8a70ecd1d3f05cc66d9e0813ffe27bec0fe1f248f`)
