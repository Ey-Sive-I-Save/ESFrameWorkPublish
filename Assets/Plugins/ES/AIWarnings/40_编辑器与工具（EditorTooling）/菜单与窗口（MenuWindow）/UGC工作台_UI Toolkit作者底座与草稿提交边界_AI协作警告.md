# UGC 工作台：UI Toolkit 作者底座与草稿提交边界

Status: current
StableId: es.aiwarnings.editor.ugc-workbench-draft-commit-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, editor, ugc, workbench, draft, commit
Applicability: 修改 ESWorkbench、World/关卡/剧情/对话/Graph 作者工作台、拖放、锁定、Undo、Draft、提交或恢复时。
EvidenceRef: ES/Tools/Validation/Test-ESMenuArchitecture.ps1 -RouteId es.aiwarnings.editor.ugc-workbench-draft-commit-boundary
Owner: ES Editor/Workbench
StaleWhen: Workbench Host、Contribution、Draft/Commit、Undo/Redo、拖放事件或 World 作者合同变化。
Knowledge: es.aiwarning.editor.ugc-workbench-draft-commit-boundary.v1

长期约束：
- 工作台必须提供稳定作者会话：资源/层级发现、视口、Inspector、问题、预览、锁定、Undo、Draft、验证和显式提交；外壳或二维表单不等于能力完成。
- Source → Baseline/Hash → 隔离 Draft → ChangeSet/Dirty → Validation → Commit Plan → 正式提交/取消；默认只修改 Draft，禁止直接改正式 Source 再以 Undo 充当草稿。
- 稳定选择使用 Stable ID/Kind/GUID/领域 Key，不持久化 Unity Object、InstanceId 或临时视口对象；外部 Source 漂移必须锁定提交。
- 外部拖放事件由 Host 在子视口/IMGUIContainer 之前以 TrickleDown 接收；取消路径（DragLeave、失焦、PointerCaptureOut、Detach、DragExited）必须幂等清理 owner、session token 和外部状态。
- 所有 mutation 必须有明确 Undo 目标；Undo/Redo 后同步领域 Dirty、Hash、ChangeSet、Draft 快照、选择和外部漂移检查，不能只刷新界面。
- 预览、Terrain、Camera、RenderTexture、临时对象和回调受统一生命周期管理；贡献注册与窗口实例分离，不得复制第二套底座。
- 正式提交须验证、检查基线漂移、建立快照/Undo、写唯一 Source、重新读取核对并更新恢复状态；失败保持 Draft 可恢复，后处理失败不得谎报整体回滚。
- 关键交互、Domain Reload、冲突、失败注入、Profiler、运行时和发布证据未形成前，不得宣称 Accepted 或商业级完成。

静态证据仅覆盖源码、配置和规则；Unity Editor 交互矩阵、Reload、Undo/Redo、性能和发布行为仍未证实。
