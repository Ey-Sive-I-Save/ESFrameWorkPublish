# P0：实际可玩闭环与运行证据

`Status`: `current`
`StableId`: `es.aiwarning.p0.playable-loop-runtime-evidence.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `playable-loop`, `runtime-evidence`, `playmode`, `profiler`
`Applicability`: 影响玩家体验/业务闭环的逻辑、角色、AICommand、相机、输入、交互、表现和性能；Editor/导入器/治理工具按自身边界验收。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-playable-loop-runtime-evidence.md`
`StaleWhen`: 运行闭环、测试场景、证据等级、成熟度口径或 SourceRefs 变化。

## 长期 P0 约束

- 架构、接口、模块、配置、静态编译、日志或抽象设计不能证明功能完成；运行时功能必须形成“输入意图 → 权威 Request/State/Command → 唯一执行入口 → 业务结果 → 运动/世界 → 动画/IK/VFX/音频/UI → 可观察终态 → 成功/失败/取消/打断/超时/禁用/回池清理”闭环。
- 玩家主链优先可用：角色取得/释放控制权、相机旋转跟随避障、相机映射移动、明确转身意图；重复/抖动/并发输入、设备/焦点切换及所有失败终态必须有反馈和重置路径。
- 无正式或专用测试场景只能报告未验收。场景至少覆盖角色、相机、输入、起点/目标/障碍/失败区、重置及成功/失败/取消/重入/回池观察点。
- 生命体/AICommand 必须覆盖发现、准备、执行、成功/失败/取消/打断、收尾、控制权释放、Lease/资源/临时目标清理和回池重置；日志、动画或事件不能冒充业务成功。
- 表现必须与业务状态一致；按风险实测输入延迟、相机/移动响应、动画过渡、状态切换以及必要的 Profiler GC/CPU/内存。指标不适用要说明理由，不能凭代码形态声称无性能风险。
- 报告必须区分实现、静态、Unity 编译/域重载、真实操作/PlayMode、表现、Profiler、发布/IL2CPP 证据。缺真实运行证据时成熟度最高为 `Verifying`，不得称 Stable/完成/商业级验收。
- AI 应主动追踪入口/消费者、检查配置序列化并执行适用验证和最小修正；被环境/授权阻断时列出具体节点、影响、风险、复现/规避和下一步，不得虚构通过或扩大范围。

详细闭环节点、场景要求、证据分层和违规处理见 Knowledge：`es.aiwarning.p0.playable-loop-runtime-evidence.v1`。
