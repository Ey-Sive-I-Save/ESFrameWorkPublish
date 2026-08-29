# ESAdvancedDialog 通用编辑器输入边界
Status: current
StableId: es.aiwarning.editor.es-advanced-dialog-input-boundary.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, editor, esadvanceddialog, input, authorization
Applicability: Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-es-advanced-dialog-input-boundary.md
StaleWhen: ESAdvancedDialog 实现或 SourceRef 变化。
- `ESAdvancedDialog` 只是通用 Editor 交互外壳，不属于 AutomationCenter，也不授予业务权限。
- 稳定 OptionId 才是协议值；显示标签可本地化，旧 `AddChoice` 仅适用于显示值即业务值。
- 对话框不得启动进程、越过正式入口读写资产/发布物/设置/凭据、接收机密，或把确认/进度/取消冒充权限和任务完成证据。
- 修改业务状态须由调用方完成权限、目标、前置检查并经正式 C# Editor 入口执行，遵守 Undo/Dirty/保存/回滚合同。详见 Knowledge。
Knowledge：`es.aiwarning.editor.es-advanced-dialog-input-boundary.v1`
