# Stable Graph V2：Legacy 删除与 Automation 闭环交接

交接日期：2026-08-11  
项目根：`F:\aaProject\ESFrameWorkPublish`  
分支：`main`  
交接时 HEAD：`94bc7941b20744bc65301362c4b89bed3a61c30c`

## 接手目标

继续担任 Stable Graph V2 的工程验收者，依据当前源码而不是旧对话判断：

1. 复核 Legacy Graph/NodeRunner 删除是否完整且没有资产、程序集、配置或文档残留。
2. 验收 Graph AI 候选生成与单次执行是否真正经 AutomationCenter 建立 RunId、输入 Hash、RunRecord 和回执。
3. 在可用 Unity Editor 环境执行 Domain Reload、EditMode Test Runner 和至少一次真实 Graph 到候选、Diff、批准、实现启动闭环。
4. 未取得真实运行证据前保持 `Verifying`，不得宣布商业级完成。

## 已完成事实

- Legacy `Assets/Plugins/ES/Editor/ESGraphView` 已删除。
- Legacy `Assets/Plugins/ES/1_Design/Define/0Define-NodeRunner` 已删除。
- 仅服务旧 Runner 的 `SupportInterface_.cs` 与 `.meta` 已删除。
- 合计 65 个受 Git 跟踪的 Legacy 文件处于删除状态，包含两个顶层目录 `.meta`。
- `link.xml`、全局编辑器配置、Hardcoded 资产指南、生成缓存与正式文档中的旧执行引用已清理。
- 定向检索未发现可执行源码仍引用 `ESGraphViewWindow`、`NodeRunnerSO`、`NodeContainerSO`、
  `INodeRunner_Origin`、`0Define-NodeRunner` 或 `Editor/ESGraphView/`；现行门禁文档保留旧名称仅用于禁止恢复。

## Automation 接入

- 新增受管 Endpoint：
  `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs`
- 注册任务：
  - `es.agent.generate@1`：Graph AI 候选生成。
  - `es.agent.use@1`：即时执行与单次 AICommand/AISkill 使用。
- Graph V2 的三条发送路径已从直接窗口调用改为 `ESAutomationFacade.RunTask(...)`。
- 每次派发创建：
  - N 格式 RunId。
  - `agent-graph-dispatch.json`。
  - `run-record.json`。
  - `dispatch-receipt.json`。
  - Prompt SHA-256、GraphId、ContentSignature、OperationKind 与操作者证据。
- 候选请求发送前重新读取 `generation-request.json`，核对请求目录、GraphId 和 ContentSignature，阻止跨图派发。
- Automation Worker Contract 禁止 `WriteAssets`；Endpoint 只在 `ES/Automation/Temp/<RunId>` 写证据。
- Graph 运行结果不得自动修改 Graph，任何反馈必须由用户重新编辑、Bake 和批准。

## 已取得验证证据

- `dotnet build ES_Editor.csproj --no-restore`：0 error，1 个既有 `CS0649` warning。
- `dotnet build ES_Design.ConfigKey.Tests.csproj --no-restore`：0 error，3 个既有 `CS0649` warning。
- `git diff --check`：通过，仅有仓库换行策略提示。
- 生成工程缓存中的旧 Graph/NodeRunner 文件清单已清理。
- 新增 `AgentAuthoring_GraphAutomationTasksAreDiscoverable` 测试，覆盖 Facade/TaskContract 注册和无 `WriteAssets` 能力。

## 未完成与禁止误报

- 本机未发现 Unity Editor，因此没有执行 Unity Domain Reload 或 Unity Test Runner。
- `dotnet test` 没有形成 Unity Test Runner 执行证据，不得写成测试通过。
- 尚未取得一次真实 `Graph -> Bake -> Automation Run -> Candidate -> Diff -> Approval -> 实现启动` 运行记录。
- 尚未验证 HeldForUser、派发失败、Domain Reload、stale Graph、多 Graph 交替和 RunRecord 恢复。
- 尚未取得窗口关闭、深图验证和大量字段绘制的 Profiler 证据。

## 工作树边界

工作树在本轮前已经包含大量用户和其他任务改动。接手窗口必须：

- 先执行定向 `git status`、`git diff` 和源码复核。
- 不恢复、不覆盖、不清理无关修改。
- 不把其他模块的文档、装备、资源或场景改动归因于 Graph 本轮。
- 不提交、不推送，除非用户另行授权。

## 权威入口

- `Assets/Plugins/ES/1_Design/Graph/ESGraphAsset.cs`
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAgentArtifactGenerationWorkflow.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESAgentAuthoringGraphTests.cs`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md`

## 新窗口第一步

验证 immutable launch envelope 后，只读检查上述入口和当前工作树，先报告：

1. `ContextAccepted` 状态与交接快照哈希结果。
2. Legacy 删除是否仍完整。
3. Automation 接入是否存在绕过 Facade、身份错配、证据不可恢复或权限过宽。
4. 当前环境能否运行 Unity；不能时明确给出最小人工验收步骤，不得伪造测试结果。
