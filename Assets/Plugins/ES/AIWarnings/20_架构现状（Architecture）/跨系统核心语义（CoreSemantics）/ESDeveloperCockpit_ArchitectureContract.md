# ES Developer Cockpit Architecture Contract

Status: current
StableId: es.aiwarning.esdeveloper-cockpit-architecture-contract.v1
Authority: AIWarnings（Cockpit 长期合同）；详细事实见 Knowledge
RouteKeys: aiwarnings, cockpit, architecture, runtime-editor-boundary, event, action, observation, evidence, reload
Applicability: Developer Cockpit、Runtime/Editor 分层、事件/Action、Observation Run、证据和 Domain Reload
EvidenceRef: Documentation/AIKnowledge/entries/esdeveloper-cockpit-architecture-contract.md
StaleWhen: Cockpit ContractVersion、事件/Action Schema、Observation Run、Domain Reload 或 SourceRef 哈希变化。

## 长期约束

- Cockpit 只投影领域权威状态、路由受控 Action 和保存本地工作区；不拥有业务状态、不直接执行领域写操作、不提供 Git/发布/外部回滚。
- Runtime-safe 只放事件、证据和运行身份；Context/Action/Workspace/推荐模型留在 Editor，禁止下沉 EditorWindow、SerializedObject 或 UnityEngine.Object。
- 事件身份使用 `(RunId, SourceInstanceId, SourceEpoch, Sequence)`；Frame-Aligned 首版不宣称严格因果或确定性 A/B，稳定/运行/证据身份必须分层。
- Action 必须声明权限、只读、幂等、取消、恢复和前置条件；Availability 重新评估，Cockpit 不得扩大授权。关闭且无活跃 Run 时不得保留常驻扫描或事件泵。
- Observation、证据等级、Domain Reload 原子终结和旧运行身份拒绝按 Knowledge 合同执行；静态结果不得冒充 Runtime/Profiler/Release 证据。

## Knowledge 导航

详细事件排序、同帧观测、Action 权限、FPS/阈值、证据升级和 Reload 规则见 `es.aiwarning.esdeveloper-cockpit-architecture-contract.v1`。本 Warning 不授权新增运行时、Editor、Git、发布或外部操作。
