# ESCmdAgent 失败复盘与后续禁止事项 AI 协作警告

> 职责：记录 `ESCmdAgentWindow` 这轮失败式迭代的真实结论，给后续 AI 避免继续错误推进。
> 适用路径：`Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs`、`Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/ESCmdAgent.cs`、`Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs`。
> 结论日期：2026-07-23。

## 最高结论

`ESCmdAgent` 不应该继续被设计成“Unity 内的劣化 Codex 终端”。命令行原生 TUI 在输入、光标、历史、快捷键、输出稳定性上天然更强。这个工具只有在做 Unity 专属上下文集成时才有意义：

- 附加 Unity 选中资产、脚本、SO、表格、Console 报错、截图。
- 一键读取 AIWarnings、AICommands、AITalk、项目目录上下文。
- 管理本地会话别名、置顶、备注和恢复 Key。
- 作为“AI 任务面板”发起任务，而不是复制一个完整终端。
- 架构 AI 应输出可执行架构判断，节点图只是证据资料板。

后续 AI 不要再把主要精力投入“模拟完整终端 TUI”。那条路会不断接近命令行但永远不如命令行。

## 已确认的问题

- ConPTY 可以解决 `stdin is not a terminal`，但不等于拥有成熟终端模拟器。
- ANSI 清理不足以正确显示 Codex TUI。必须至少有屏幕缓冲、光标移动、清屏、行清除、回车覆盖；即便如此也只是近似。
- 把 Codex TUI 输出直接塞进 Unity `TextArea`，体验依然不如 CMD/Windows Terminal。
- 亮色按钮不能靠 `GUI.backgroundColor` 期待 Unity 深色皮肤正确显示；也不能做成大面积高饱和发光，会刺眼。
- `项目图` 这个名字误导。用户并不需要一个抽象图，用户需要“主架构 AI”给出架构主线、风险、长期原则和下一步动作。
- 只给 `恢复最近` 一个按钮是错误 UX。恢复必须是菜单，让用户知道最近有哪些会话、哪个是哪个任务。
- 本地会话必须有别名、置顶、最近使用时间。只显示 UUID/短 Key 没有可用性。
- `codex cloud list` 是云端任务列表，不是本地 `codex resume` 会话列表。不要把 Cloud task 伪装成可 resume 会话。

## 禁止事项

- 禁止承诺“云端 resume 会话列表”已经可用。当前 CLI 暴露的是 `codex cloud list/status/apply/diff` 任务接口，不是 TUI resume session picker 的云端等价物。
- 禁止再把默认界面做成控件堆叠的终端调试器。
- 禁止默认显示大量折叠面板、重复页签栏、无意义说明文字。
- 禁止让高频动作只藏在 `更多` 菜单；也禁止把所有控制键一排铺满到挤压输入区。
- 禁止让按钮颜色过暗到不可识别，或过亮到刺眼。采用低饱和强调色、左侧色条、轻微描边即可。
- 禁止用“项目图”描述主架构 AI。应该叫 `架构AI`、`主架构AI`、`资料板` 等明确概念。
- 禁止只在终端正文里提示任务完成。需要额外状态提示，但不能大面积高饱和发光。
- 禁止将用户输入、AI 输出、系统日志混成一种颜色和一种前缀。

## 推荐产品方向

默认界面应更像“AI 任务面板”：

1. 顶部：任务入口、恢复菜单、新会话、停止、会话别名、状态。
2. 中部：短输入框，支持发送当前需求。
3. 输出：区分 `你`、`AI`、`系统`、`错误`、`完成`，颜色克制。
4. 附件：Unity 选中资产、外部文件、截图、Console 错误。
5. 会话：本地别名、置顶、备注、最近使用，恢复菜单可快速定位。
6. 架构AI：读取 AIWarnings/AITalk/CodexSessions，输出架构判断；节点图只是证据板。

终端调试视图可以保留，但应作为高级视图，不应成为默认价值主线。

## 当前实现事实

截至本警告写入时，`ESCmdAgentWindow` 已经包含：

- Windows ConPTY 后台 CMD 启动，用于让 Codex TUI 有真实 TTY。
- 简易终端屏幕缓冲，处理清屏、光标移动、行清除、回车覆盖。
- 本地会话元数据，使用 `EditorPrefs` 保存别名、置顶、最近使用，不生成资产。
- 恢复菜单，列出当前项目本地 Codex session。
- Cloud task list 入口，但仅作为云端任务查看，不作为 resume。
- `AI任务 / 架构AI` 顶部页签。
- `主架构AI` 按钮会向当前 Agent 发送架构分析 prompt。
- 输出行有 `你：`、`AI：`、系统、错误、完成的文字和 rich text 颜色区分。

这些只能算过渡实现，不代表最终方向正确。后续优先做 Unity 上下文集成，而不是继续修终端细节。

## 后续修改建议

- 如果继续做，先把输出模型从“终端文本”拆成“消息列表 + 原始终端调试视图”。
- 将 Console 错误、选中资产、截图、AIWarnings 快捷读取做成一键附件，而不是手动复制。
- 架构AI页应该先展示“它将读取什么、输出什么、写到哪里”，再展示节点图。
- 云端任务应单独叫 `云端任务`，提供 list/status/diff/apply，不要混进 `恢复` 菜单。
- 若保留 ConPTY，必须注意进程生命周期：窗口关闭、域重载、Unity 退出、编译期间都要停止/释放。
- 所有 UI 文案必须中文明确，避免“项目图”“恢复最近”这种含义不完整的名称。

## 编码与协作要求

- 读取此文件必须使用 UTF-8。
- 不要根据 PowerShell 乱码输出误修中文。
- 修改相关文件前必须编译 `ES_Editor.csproj` 和 `ES_Stand.csproj`。
- 不要回滚用户或其他 AI 的无关改动。
