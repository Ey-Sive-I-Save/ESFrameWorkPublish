# 历史上下文：RuntimeKey 旧结论废止与旧输入坏引用

Status: historical
StableId: es.aiwarnings.handover.runtimekey-input-legacy.v1
Authority: ESFramework AIWarnings / historical handover
RouteKeys: aiwarnings, handover, historical, runtimekey, asset-pipeline, legacy-input
Applicability: 后续 AI 处理旧场景/Prefab、RuntimeKey 与资源管线迁移时
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-handover-runtimekey-input-legacy.md`
StaleWhen: 历史记录解释、当前资源 P0 Warning 或输入/资源源码变化
Knowledge: `es.aiwarning.handover.runtimekey-input-legacy.v1`

## 历史约束（不可恢复）

- 2026-07-28 已废止“持久化稳定 RuntimeKey”结论：RuntimeKey 只属于当前进程、当前强类型表和当前表生命周期，不进入 Page、Library、Catalog、Manifest、JSON、ConfigKey 或存档/网络。
- 不恢复 `ESAssetRegistry` 页面分配/快照/手工改键、30000+ RuntimeKey、Runtime AssemblyStream 或旧输入类型 `EntityAIInputSystemModule` / `EntityInputStateModule` 的兼容壳。
- 旧场景/Prefab 的坏 SerializeReference 应在 Unity 中清理并保存；当前输入链是 `EntityAIDomain.inputState + EntityPlayerInputWriteModule + EntityAIInputDispatchModule`。
- Runtime 仍沿 `Library/Book/Page (Editor) → Manifest/Table → GameManager AssetModule → Loader/RunMode`；EditorDirect 的 GUID/AssetDatabase 不得扩散到 Player。
- 本文是历史交接，不把旧 Unity 日志或管理员权限提示写成当前运行时事实；静态记录不能替代 Unity 场景清理和资源链验收。

详细历史日志、旧设计根因和推荐清理步骤见 Knowledge。
