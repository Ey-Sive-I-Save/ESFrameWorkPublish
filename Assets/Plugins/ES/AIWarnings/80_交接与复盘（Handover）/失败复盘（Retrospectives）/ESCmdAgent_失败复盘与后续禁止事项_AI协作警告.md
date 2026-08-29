# 失败复盘：ESCmdAgent 不应成为劣化 Codex 终端

Status: historical
StableId: es.aiwarnings.retrospective.escmdagent-terminal.v1
Authority: ESFramework AIWarnings / failure retrospective
RouteKeys: aiwarnings, retrospective, escmdagent, editor, terminal, ux, session
Applicability: ESCmdAgentWindow、会话恢复、架构 AI 和 Unity 上下文集成
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-retrospective-escmdagent-terminal.md`
StaleWhen: ESCmdAgent、CLI 会话协议、编辑器 UI 或 SourceRefs 变化
Knowledge: `es.aiwarning.retrospective.escmdagent-terminal.v1`

## 长期禁止事项

- 不要把 Unity 工具继续做成完整终端 TUI；其价值是 Unity 资产/脚本/Console/Warning 上下文、会话元数据和任务面板。终端调试视图只能是高级视图。
- ConPTY/ANSI 近似不等于成熟终端；不得承诺云端任务列表可替代本地 resume 会话。Cloud list/status/diff/apply 与本地 Codex session 必须分开命名。
- 恢复必须是可识别的本地会话菜单（别名、置顶、最近使用、备注），不能只给 UUID 或“恢复最近”。架构 AI 应输出主线、风险、原则和下一步，节点图只是证据板。
- UI 文案、输入、AI、系统、错误、完成必须区分；避免控件堆叠、重复页签和过饱和颜色。高频动作不能全藏在“更多”菜单。
- ConPTY/预览进程在窗口关闭、域重载、编译和 Unity 退出时必须停止释放；中文文件读写使用严格 UTF-8，不得因 PowerShell 乱码误修。

详细失败原因、过渡实现和产品方向见 Knowledge。
