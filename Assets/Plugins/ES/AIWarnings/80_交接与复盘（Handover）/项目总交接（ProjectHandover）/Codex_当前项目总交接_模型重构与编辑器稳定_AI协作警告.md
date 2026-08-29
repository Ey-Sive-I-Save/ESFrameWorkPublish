# 历史交接：Codex 当前项目总交接——模型重构与编辑器稳定

Status: historical
StableId: es.aiwarnings.handover.codex-project-model-editor.v1
Authority: ESFramework AIWarnings / project handover
RouteKeys: aiwarnings, handover, historical, entity-model, editor, preview, lifecycle, aicommands
Applicability: 后续 AI 接手 Entity 模型、资源包编辑器、预览、生命周期与协作入口
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-handover-codex-project-model-editor.md`
StaleWhen: 项目模型、编辑器预览底层、生命周期合同或 SourceRefs 变化
Knowledge: `es.aiwarning.handover.codex-project-model-editor.v1`

## 交接边界

- ESFramework 需按配置入口、生命周期、ReloadDomain、资源链、可回退和可复用性整体判断；不得只凭单脚本能编译或预览存在宣称完成。
- 统一角色入口与控制请求沿 Entity/Domain/Module 边界；不得恢复已废止的临时 AnimatorController、重复导出、深层扫描、第二套 Controller 或 VirtualTransform/LOD 等未经证据的设计。
- 编辑器预览、资产包导出、Inspector/菜单和事件生命周期必须统一底层、命名解绑、幂等释放，避免场景污染、T Pose、重复导出和 ReloadDomain 泄漏。
- AICommands 是执行协议，AIWarnings 是长期事实/约束；二者与 AITalk、Persona 分离。中文路径读写必须严格 UTF-8，脏工作树不得回滚他人改动。
- 当前交接为历史上下文；静态/编辑器证据不得替代 Unity、PlayMode、Profiler、Player、IL2CPP 或发布验收。

详细模型、预览、资源导出和稳定性清单见 Knowledge。
