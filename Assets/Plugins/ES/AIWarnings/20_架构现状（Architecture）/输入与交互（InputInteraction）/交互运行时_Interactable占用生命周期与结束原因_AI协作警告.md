# 交互运行时：Interactable 占用、生命周期与结束原因 AI 协作警告

Status: current
StableId: es.aiwarning.runtime.interactable-ownership-lifecycle-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, runtime, interaction, interactable, ownership, lifecycle
Applicability: ESInteractable、EntityBasicInteractionModule、ESTagApplyZone 交互链
Owner: ESFramework Runtime Interaction 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-interactable-ownership-lifecycle-boundary.md
StaleWhen: 交互占用、结束原因、State/IK/SupportFlag 清理或 Tag Zone 实现变化。

## 长期约束

- Input 只产生交互意图；候选探测、距离/朝向/Tag/Permit/状态可用性和占用判定由交互模块与目标完成。
- `_interactionOwner` 是单 Owner、first-acquire-wins：同 Entity 可重入，其他 Entity 返回 `Occupied`；只有 Owner 可释放。它不是排队、抢占、超时、网络并发或 generation-safe lease 协议。
- 只有 `TryAcquireInteraction` 成功后才能建立 State、IK、MatchTarget、SupportFlag；Begin 失败必须释放占用。所有结束原因都经统一收口，停止 IK/MatchTarget、退出 State、恢复 SupportFlag 后 `ReleaseInteraction`。
- `Completed`、`UserCancelled`、`MovementCancelled`、`Timeout`、`TargetLost`、`StateExited`、`ModuleDisabled`、`BeginRejected` 必须可区分；禁用、销毁、池化和目标丢失不得只清 UI 或 Owner。
- 业务回调不拥有最终清理权；当前异常隔离尚未完整验收。`ESTagApplyZone` 的 Tag lease 与交互占用独立，不得借其拼排队/独占。
- 未完成 PlayMode 竞争、异常、禁用、池化/销毁和多 Collider 证据前，不得宣称异常安全、多人竞争支持或 0 GC 稳态。

## Knowledge 导航

完整主链、回调/异常边界、Tag Zone、性能口径、验收矩阵和原文快照见 `es.aiwarning.runtime.interactable-ownership-lifecycle-boundary.v1`。
