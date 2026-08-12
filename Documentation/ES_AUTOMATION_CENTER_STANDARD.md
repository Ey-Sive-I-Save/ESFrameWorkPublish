# ESAutomationCenter 管理级标准

状态：现行治理标准（含场景扫描分阶段原型源码）
最后验证：2026-08-05
适用源码入口：`Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs`

## 定位

`ESAutomationCenter` 是 ESFramework 自动化任务的 C# Editor 权威入口。Python、PowerShell 或其他语言只能作为受管 Worker，不得直接拥有 Unity `Assets/` 写权限，也不得绕过任务注册、路径策略、运行记录和发布门禁。

```text
ESAutomationCenter（C# Editor）
  -> TaskRegistry
  -> TaskContract
  -> PathPolicy
  -> AutomationFacade / AI Bridge
  -> WorkerAdapter / ProcessRunner
  -> RunRecord / ReportCenter
  -> ReleaseGate
  -> 受管 Worker（Python、PowerShell 或其他语言）
```

## 第一阶段边界

- 只建立任务协议、Worker 注册、路径权限、运行记录、报告和门禁模型。
- 不创建散落的 `audit.py`、`upload.py`、`cleanup.py`。
- 不实现发布上传、删除、清理或 Unity 资产修改任务。
- 首个注册 Worker 为 `es.scene.scan@1`：它只分析 C# Editor 导出的当前场景快照，写入 RunId 临时目录；不解析 `.unity` YAML，不修改场景或 `Assets/`，也不具备发布能力。

## TaskContract 最低字段

每个任务必须声明：

- `TaskId`、`Version`、`WorkerType`、`WorkerId` 和 Worker 版本/指纹；
- 结构化输入、输出文件和协议版本；
- `ReadRoots`、`WriteRoots` 和禁止路径；
- `Capabilities`，默认只读；
- 超时、取消、退出码和是否支持 `DryRun`；
- 可重试语义、临时目录和报告目标。

示例：

```yaml
protocolVersion: 1
taskId: es.release.audit
version: 1
worker:
  type: Python
  workerId: es.release.audit.python
  version: 1.0.0
  entrypointHash: <64 位 SHA-256，真实注册时填写>
inputs: [releaseRoot, channel, version]
readRoots: [ES/Releases]
writeRoots: [ES/Automation/Reports]
capabilities: [ReadArtifacts, WriteReports]
timeoutSeconds: 600
supportsDryRun: true
supportsRetry: false
outputs: [result.json, audit.md]
```

`supportsRetry` 在协议 v1 中是向后兼容的可选字段，缺省值固定为 `false`。只有 Worker 对相同输入具备幂等执行或稳定去重键，并且失败后不会扩大写入、副作用或发布动作时，注册方才允许显式设为 `true`；AISkillGraph 不得自行推断或绕过该声明。

## 不可绕过的安全边界

1. Worker 只能执行注册且版本锁定的任务，禁止任意脚本路径和任意命令行。
2. 默认只读；删除、上传、发布、覆盖和 Unity 资产写入必须具有独立能力标记和当前用户授权。
3. Python、PowerShell 或其他 Worker 不得写入 `Assets/`；Unity 资产变更只能由 C# Editor 入口执行。
4. 所有路径先规范化，再检查是否位于允许根目录内；拒绝 `..`、junction、symlink 和大小写绕过。
5. 所有真实任务必须支持 `DryRun`、超时、取消、退出码和落盘报告；取消必须终止完整进程树。当前 `es.scene.scan@1` 已有源码级实现，但 Unity/故障注入运行级证据仍待补齐。
6. 运行记录必须保存任务、操作者、RunId、Git commit、Unity 版本、Worker 环境版本、输入 Manifest Hash、输出 Hash 和结果。Graph AI Endpoint 已持久化 `run-record.json` 与受控会话回执，并使用状态机区分启动、接收、运行、完成、失败、取消和超时；真实 Unity/Worker 闭环仍需单独验收。
7. 报告先写入 RunId 临时目录，验证完成后原子移动；失败或取消不得留下可误发布的半发布目录。
8. CI 消费结构化报告和 ReleaseGate，不依赖 Unity 弹窗；弹窗只是本机展示。
9. 凭据只能通过受控环境注入，禁止出现在命令行、输入 JSON、报告或普通日志中。
10. 受管进程任务的 `timeoutSeconds` 必须经过协议上限校验；stdout/stderr 必须由统一执行器异步排空并限制采集量。Windows 优先使用 Job Object，回退 `taskkill /T /F` 后必须确认进程树退出；权限不足、子进程脱离、输出管道未排空或终止未确认都必须硬失败。
11. 受管进程登记到全局生命周期注册表，`ReloadDomain` 前统一终止；同一 `RunId` 不得并发启动第二个 Worker。句柄只有在终止确认后才允许注销和释放。
12. Automation 的全局初始化统一走 ES `EditorInvoker_Level0`/AssemblyStream；不得新增 Unity 原生 `[InitializeOnLoad]` 作为普通任务、AI Bridge、编译控制或进程清理入口。

## 分阶段输入协议

有少量人工输入需求的 Worker 不得阻塞等待 Unity 对话框。统一采用检查点恢复：

```text
Worker 阶段 N
  -> 写 StageResult.json (NeedsInput) 并退出
  -> C# 同时校验 RunId + Generation + StepId + SchemaHash
  -> ESAdvancedDialog 只收集已注册表单
  -> C# 写 InputResponse.json
  -> 启动新的 Worker 进程执行阶段 N+1
```

- Python 只能请求已由 C# 注册的 `StepId`；不能下发任意标题、控件、脚本路径或动态代码。
- `InputResponse.json` 由 C# 规范化写入。选择控件传输稳定 OptionId，不能以本地化显示文本作为协议值。
- 取消也走 `InputResponse.json`，由下一阶段写 `Cancelled` 检查点；它不等于发布授权。
- Unity 域重载后不恢复旧 Python 进程。已落盘的 `AwaitingInput` / 已完成 `Running` 检查点可经菜单显式恢复；CI 遇到 `NeedsInput` 必须标记为 `Blocked`，除非已预置合法响应。

## `es.scene.scan@1` 原型

入口：`【ES】/自动化与开发/自动化中心/扫描当前场景（Python 原型）`，管理窗口为 `【ES】/自动化与开发/自动化中心/打开自动化中心`。

1. C# 从当前已加载 Active Scene 导出层级路径、激活状态、Layer、Tag、Static、深度和组件类型名到 `ES/Automation/Temp/<RunId>/scene-snapshot.json`。
2. Python 阶段 0 只请求三项固定报告选项：`includeInactive`、`detailMode`、`topComponentCount`。
3. C# 的 `ESAdvancedDialog` 校验输入并写入规范化响应；Python 阶段 1 生成 `scene-scan.json` 与 `scene-scan.md`。
4. C# 校验 Worker 身份、代次、SchemaHash、退出码、文件大小和 SHA-256，随后仅提升白名单报告文件至 `ES/Automation/Reports/<RunId>/`。

Python 环境解析顺序固定为：本机显式 `ES_AUTOMATION_PYTHON`，再到项目受管 `ES/Automation/Environments/Python/python-runtime.lock.json`。项目受管运行时必须锁定解释器、整个 Runtime 内容树、Python 版本，以及未来可选依赖锁文件 SHA-256；启动 Worker 前 C# 会重新探测 Python 3 并复核指纹。不会回退到 PATH、`py.exe`、Windows Store 别名，也不会从调用方接收解释器或脚本路径。当前工作区已部署受管 CPython 3.12.10 x64 Runtime；后续分发应通过受审核制品渠道同步 Runtime 与锁文件，升级后必须重新跑 Worker/Unity 验收。Worker 当前不创建子进程；未来若需子进程，先实现完整进程树取消。

所有 Python Worker 启动统一经过 `ESAutomationProcessRunner.Start()`：它只能使用已注册 TaskContract 的受信 Adapter，复核禁用 Shell、可执行入口存在、工作目录和重解析点，然后返回唯一的 `ESAutomationProcessExecution`。Endpoint 保留自身的阶段协议、输入和报告校验；进程句柄、统一超时、输出排空和进程树终止不再由每个任务重复实现。Worker 的 stdout/stderr 不构成业务协议，最终结果只能由受签名、受路径策略约束的结构化结果文件提交。

## AI 直接调用

`ESAutomationFacade` 是人工快速任务、Center、AI Bridge 和未来 CI 的共同入口。任务由领域 Endpoint 注册描述信息：稳定 TaskId/版本、分类、显示名称、Preset、SchemaHash 及 `allowAiInvoke`。Center 使用 `ESSearchDropdown` 按分类显示无输入快速预设；AI 读取同一份元数据，不点击 UI。

任务若需要分阶段输入，必须在 Descriptor 中预注册 `ESAutomationInputSchemaDescriptor`：稳定 `StepId + SchemaHash`、字段 ID、受限类型、默认值、Choice 和数值范围。Python 到达 `NeedsInput` 后退出；Endpoint 仅接受该检查点的精确代次。`ESAdvancedDialog` 与 AI 的 `submitInput` 使用同一份 Session、Generation、StepId、SchemaHash 与字段校验，最先成功提交者继续新 Python 阶段，迟到提交不得重复驱动 Worker。AI 可通过 `listTasks` 获取类型化字段描述，或在 `getRun` 的 `data.inputRequest` 获取当前检查点描述；它不能指定脚本、解释器、命令行、文件路径或未注册字段。

`ESAutomationAiBridge` 默认关闭。用户在 `【ES】/自动化与开发/自动化中心/打开自动化中心` 显式授权本机收件箱后，AI 可通过 `ES/Automation/AI/Inbox/<RequestId>.request.json` 请求：

- `listTasks`
- `runTask`
- `getRun`
- `submitInput`
- `submitContentProposal`

Unity 只在 Editor 主线程处理最终 `.request.json`，把结构化响应写入 `Responses/`，并归档请求。没有 HTTP 监听器、远程网络端口、任意命令执行或任意路径参数。

### PlayMode 暂停与受信 UnityMCP 恢复

“用户授权”和“当前监听”是两层状态：进入或退出 PlayMode 时，Bridge 自动停止文件变化监听并清空内存中的尚未开始队列，但不会撤销本机授权；Inbox 中尚未处理的请求文件会留在原处，回到 EditMode 后才重新扫描并处理。

该策略只暂停**新的 Inbox 检测与分发**，不擅自终止进入 Play 前已经启动的只读 Worker；每个长任务须自行声明、实现并验收其取消/暂停语义，不能把收件箱暂停误当作进程取消。

若项目另行接入了**已由宿主鉴权**的 UnityMCP（或等价的 Unity 主线程桥接器），该桥接器可以调用 `ESAutomationAiBridge.TrySetTrustedPlayModeListening(true, out reason)`，只为**本次已进入的 PlayMode**临时恢复监听。仅支持菜单调用的 UnityMCP 可执行 `【ES】/自动化与开发/AI 控制/PlayMode 临时恢复收件箱监听`；对应暂停菜单同路径提供。它不能开启首次用户授权，不能通过 Inbox 自举，也不会在退出 Play 后保留覆盖状态。

恢复监听不等于允许全部自动化：每个 Task 必须显式声明 `allowInPlayMode = true`，否则 Facade 仍返回 `Blocked`。当前 `es.scene.scan@1` 明确禁止在 PlayMode 运行；未来测试任务应单独注册、声明运行时边界并完成 PlayMode 验收。内容提案与资产写入不得在 PlayMode 通过此机制执行。

AI 内容补充只通过 `ESAutomationContentIngress` 的领域 Endpoint：领域必须预先注册 ContentType/版本/SchemaHash 和自身事务逻辑。Automation 平台不直接创建 Unity 资产；当前尚无资产内容 Endpoint，不能宣称 AI 已可创建 Story、Prefab 或配置。

## 语言分区与统一协议

```text
ES/Automation/
├─ Contracts/                 # Python、PowerShell 和其他 Worker 共用
├─ Workers/Python/            # Python 环境、依赖和任务实现
├─ Workers/PowerShell/        # PowerShell 模块、任务实现和策略
├─ Environments/              # 各 Worker 的锁定环境清单
├─ Reports/
└─ Temp/
```

语言在 Worker、环境和 Adapter 层严格分区；TaskContract、Capability、PathPolicy、RunResult、RunRecord 和 ReleaseGate 统一。

## 当前状态

- C# Editor 管理骨架：已建立；共享字段命名已按 JSON Schema 对齐，未注册 Worker 时 ProcessRunner 会拒绝构造可执行命令；已提供统一受管进程执行器，并补齐任务级超时上限、Job Object/进程树回退、stdout/stderr 有界异步排空、ReloadDomain 全局清理和同 RunId 并发拒绝。Facade、分类快速任务和默认关闭的本机 AI Bridge 已有源码；C#↔Schema 自动往返与故障注入测试仍待实现。
- Python 场景扫描 Worker：原型源码已实现并由固定入口指纹注册；项目受管 CPython 3.12.10 已部署，Worker 定向单测已通过。当前生成的 `ES_Logic` 与 `ES_Editor` 项目均已静态构建通过；活跃 Unity Editor 尚未刷新本轮程序集，因此 Unity 内菜单、Inbox 端到端、Test Runner 与 PlayMode 验收仍不能宣称通过。
- PowerShell Worker：未实现、未注册、不得宣称可执行。
- 发布物只读审计：尚未实现。
- Unity、CI、发布验收：未运行。
