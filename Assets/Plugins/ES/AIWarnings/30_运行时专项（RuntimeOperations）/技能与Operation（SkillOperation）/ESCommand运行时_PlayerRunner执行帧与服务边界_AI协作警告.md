# ESCommand 运行时 Player/Runner 边界
Status: current
StableId: es.aiwarning.runtime.escommand-player-runner-boundary.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, runtime, escommand, player, runner
Applicability: ESCommandPlayer、Runner、Playable 与 RuntimeMode 输入
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-escommand-player-runner-boundary.md
StaleWhen: ESCommandPlayer/Runner、Playable 或 SourceRef 变化。
- `MODULE_ESCommandModule.Update()` 是唯一 `TickAll` 驱动；禁止 Skill/UI/业务循环重复调度。
- `Play`、`Cancel`、`Stop` 语义不可混用：重播前须 Stop，Cancel 延迟到下次 Tick，Stop 立即补偿/注销；同帧 Tick 受 `lastTickFrame` 门禁。
- Playable 必须处理 Running/Failed/Canceled 及输入、RuntimeMode、资源的对称清理；RuntimeMode 多来源不得用枚举扫描伪装精确回收。
- 命令异常尚无逐命令隔离；场景/模块重置须清 Runner 与 Services。完整主链与验收见 Knowledge。
Knowledge：`es.aiwarning.runtime.escommand-player-runner-boundary.v1`
