# ESAutomationCenter 与受管 Worker 治理 AI 协作警告

> 状态：现行约束（含 `es.scene.scan@1` 分阶段原型与 Graph AI/AISkill 接入源码）。
> 最后核对：2026-08-16；本次只复核 Graph AI/AISkill 接入事实，其他 Worker 状态保留原证据边界。
> 适用范围：`Assets/Plugins/ES/Editor/ESAutomation`、`ES/Automation` 及所有 Python、PowerShell 或其他自动化执行器。

## 最高结论

自动化能力必须先进入 C# Editor 权威的 `ESAutomationCenter`，再允许增加具体 Worker。禁止先散落创建 `audit.py`、`upload.py`、`cleanup.py`，再事后补权限和报告。

```text
ESAutomationCenter
  -> TaskRegistry
  -> TaskContract
  -> PathPolicy
  -> AutomationFacade / AI Bridge
  -> ProcessRunner
  -> RunRecord / ReportCenter
  -> ReleaseGate
  -> 受管 Worker
```

## 当前入口

```text
Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs
Assets/Plugins/ES/Editor/ESAutomation/ESAutomationSceneScanPrototype.cs
Documentation/ES_AUTOMATION_CENTER_STANDARD.md
ES/Automation/Contracts/
```

`ESAutomationCenter` 是权限、任务注册、路径策略、运行记录和发布门禁的管理权威。Python 和 PowerShell 只负责实现已注册的 Worker，不得自行定义任务权限。

## 强制规则

1. Worker 只能执行注册、版本锁定且有入口指纹的任务；禁止任意脚本路径。
2. 默认只读。删除、上传、发布、覆盖和资产写入必须分别声明能力并取得当前授权。
3. Worker 不得写 `Assets/`；Unity 资产变更只能经过 C# Editor。
4. 所有真实任务必须有结构化输入/输出、DryRun、超时、取消、退出码、报告和可重试语义；`es.scene.scan@1` 已有源码协议，但尚不存在 Unity/Python 运行级验收。
5. 运行记录必须包含操作者、RunId、TaskId/版本、Git commit、Unity/Worker 版本、输入 Manifest Hash、输出 Hash 和错误。Graph AI Endpoint 与 AISkill 工作流已经分别持久化 `run-record.json` / `workflow-run.json`；其他 Worker 不得继承该结论，仍须逐任务证明持久化、原子写入和恢复合同。
6. 报告先写 RunId 临时目录，成功后原子提升；失败不能留下可误发布的半发布目录。
7. CI 读取 JSON/Markdown 报告和 ReleaseGate，不依赖弹窗；弹窗只是本机展示。
8. 凭据不得出现在命令行、输入文件、普通日志或报告中。
9. 分阶段 Worker 必须写检查点后退出。C# 只有同时核验 `RunId + Generation + StepId + SchemaHash` 才能打开固定表单并继续；Python 禁止长时间等待 Unity 对话框、动态生成控件或下发任意命令。
10. AI 不得直连 ProcessRunner。只能经 `ESAutomationFacade` 调用已注册、显式 `allowAiInvoke` 的 Task；AI Bridge 默认关闭，使用固定 JSON 信封和本机收件箱，不开放网络端口。PlayMode 会临时停止收件箱监听；只有受信 Unity 主线程控制通道可为本次 Play 临时恢复，且 Task 仍须显式 `allowInPlayMode`。
11. `submitContentProposal` 不是“AI 可随意写资产”。必须由所属领域注册 ContentType、版本、SchemaHash 与事务；未注册内容类型一律拒绝，Automation 平台不直接创建 `Assets/` 内容。
12. 所有外部进程必须经过受管生命周期入口：任务级超时不得超过协议上限；stdout/stderr 必须异步排空并限制最大采集量；Windows 优先加入 Job Object，回退 `taskkill /T /F` 时必须确认进程树终止。终止未确认时不得注销句柄、不得伪装为 Cancelled 或成功。
13. 受管进程必须登记到全局生命周期注册表；`ReloadDomain` 前统一终止并报告失败。相同 `RunId` 的并发启动必须拒绝，避免重复 Worker、重复报告和旧阶段竞态。
14. Automation 的全局监听、编译控制、任务注册和进程清理不得再新增 Unity 原生 `[InitializeOnLoad]`；统一通过 ES `EditorInvoker_Level0`/AssemblyStream 注册，避免把自动化业务塞入 ReloadDomain 隐式热路径。

## 当前状态

- C# Editor 管理骨架：已建立；TaskContract 的共享字段已按 JSON Schema 对齐。`ESAutomationFacade` 会强制核对注册合同、Worker 身份、能力、DryRun、输入 Hash 及读写路径，未注册或未绑定合同的 Endpoint 必须拒绝。受管进程已补任务级超时上限、Job Object/进程树回退、stdout/stderr 有界异步排空、ReloadDomain 全局清理和同 RunId 并发拒绝；自动往返与故障注入测试仍待实现。
- Python 场景扫描 Worker：`es.scene.scan@1` 已注册固定 Python Adapter、Facade Endpoint、入口指纹与本机 AI 调用元数据；它只分析 Unity 导出的当前场景快照，临时报告经 C# 白名单校验后再提升。真实解释器配置、Unity 刷新编译、Python 定向测试、端到端运行均未执行。
- AI Bridge：文件收件箱、请求/响应协议、任务直调和内容提案扩展点已有源码；默认关闭，尚无 Unity 运行级证据。当前没有注册资产内容 Endpoint，不能宣称 AI 能直接创建游戏资产。
- Graph AI/AISkill：`es.agent.generate@1`、`es.agent.use@1` 已注册；Graph Endpoint 在调用方提供稳定 InvocationId 时将其作为 RunId，否则生成新 RunId，并在首次派发前写入 RunRecord、拒绝同 ID 对应不同任务或输入。普通 Graph `Dispatch` 当前未传稳定 InvocationId，不能宣称所有交互派发都端到端幂等；AISkill 执行协调器会先持久化稳定 InvocationId，再调用 Automation。AISkill 执行工作流源码具备 TaskContract、父子 Run、取消确认、执行合同 Hash、可变状态 Hash 和受限恢复。当前没有重新取得 Unity Test Runner、真实受管会话闭环、故障注入或 ReloadDomain 恢复证据，状态仍为源码接入待验收。
- PowerShell Worker：未注册、未实现。
- 发布物只读审计：未实现。
- 上传、清理、发布：禁止提前实现。
