# ESCmdAgent 失败复盘与 Unity AI 任务面板边界

`KnowledgeId`: `es.aiwarning.retrospective.escmdagent-terminal.v1`  
`Authority`: `AIWarnings failure retrospective + current ESCmdAgent source`  
`RouteKeys`: `aiwarnings`, `retrospective`, `escmdagent`, `editor`, `terminal`, `ux`, `session`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `b48476d4c0c72f07059cc261be4e0386d3b63e28e39259d0f257ae1d63d4f9c3`  
`SourceSetHash`: `b48476d4c0c72f07059cc261be4e0386d3b63e28e39259d0f257ae1d63d4f9c3`  
`EntryBodyHash`: `796365890e60196d6d99c89d7896f52ea447fb757d6c41551cda9f32b1c7ac1a`  
`StaleWhen`: `ESCmdAgent、CLI 会话协议、编辑器 UI 或 SourceRefs 变化。`

## 保真迁移

原 Warning 84 行、5,670 UTF-8 字节；现 Warning 保留失败结论、禁止事项和证据边界。详细 ConPTY/ANSI 问题、会话菜单、Cloud task 区分、过渡实现和后续产品方向迁移至本条目。

## 失败结论与正确方向

- ConPTY 只解决 stdin TTY，ANSI 清理也不能提供成熟终端；将 Codex TUI 塞进 Unity TextArea 在光标、屏幕缓冲、快捷键和稳定性上仍不如原生终端。默认产品应是 AI 任务面板：任务入口/新会话/停止/状态、短输入、消息列表、Unity 上下文附件和本地会话恢复。
- Unity 上下文集成包括选中资产、脚本、SO、表格、Console、截图及 AIWarnings/AICommands/AITalk；终端调试保留为高级视图。架构 AI 展示读取范围、输出类型、写入位置和风险，节点图仅作资料板。
- 本地 Codex session 与 `codex cloud list/status/diff/apply` 必须分开；Cloud task 不是可 resume session。会话菜单需别名、置顶、最近时间、备注和稳定恢复 Key。

## 生命周期与 UI

- ConPTY 进程必须在窗口关闭、域重载、编译和 Unity 退出时停止、解绑命名事件并释放；EditorPrefs 只保存本地会话元数据，不生成资产。任何“当前实现已存在”仍需回读源码和运行验证。
- 输出区分用户、AI、系统、错误、完成；低饱和色条/描边优于大面积发光。菜单动作使用明确“打开/定位/复制”前缀和快捷键，避免重复命令与“项目图”等误导名称。
- 修改前应编译 ES_Editor/ES_Stand 并检查脏工作树；本条目没有执行这些运行/编译证据，不能把过渡实现写成最终可用。

## EvidenceRefs

- `Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs`
- `Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/失败复盘（Retrospectives）/ESCmdAgent_失败复盘与后续禁止事项_AI协作警告.md` (`1c4e98300cab7531038f48e062dc3b1f1973aaa8f3144a4167f18bf2a63cbe41`)
- `Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs` (`15ee25d40dcbf0bea32f2f36ada64927b9959dfa46c58ca7546e3d9cdc08d01c`)
- `Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs` (`81ec4df8733b13408174a7e9fdb6f1eeb2267e792450047f7804bdbcb696c4db`)
