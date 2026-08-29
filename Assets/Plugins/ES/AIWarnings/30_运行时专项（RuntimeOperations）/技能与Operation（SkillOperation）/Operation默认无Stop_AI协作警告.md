# Operation 默认无 Stop 边界
Status: current
StableId: es.aiwarning.runtime.operation-default-no-stop.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, runtime, operation, stop, ownership
Applicability: ESOutputOp、SkillOperationClipRuntimePlayer 与 Pack 租期
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-operation-default-no-stop.md
StaleWhen: Operation/Clip/Pack 所有权实现或 SourceRef 变化。
- `ESOutputOp.NeedsStop` 默认 `false`；仅持有跨时状态/外部资源且 `StopOperation` 成对归还者可为 `true`。
- 一次性伤害、事件、日志、OneShot 音频和单次数值写入不得伪造 Stop；复合 Op 必须由子 Op 推导，`MustTriggerStop` 不改变 NeedsStop。
- 不在共享配置保存实例状态，不每帧判断 NeedsStop；新增 Stop 必须覆盖成功/失败补偿/正常退出/强制退出。
- Pack 只能由创建者按 Version 归还；借用引用、异步生命周期和废弃 Buffer 不得越权。详见 Knowledge。
Knowledge：`es.aiwarning.runtime.operation-default-no-stop.v1`
