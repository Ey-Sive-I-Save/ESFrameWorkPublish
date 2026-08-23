# Feishu CLI 外部适配器边界

状态：候选接入已实现 / 待 Unity 受管运行与真实认证验收。

`KnowledgeId`: `es.feishu.adapter-boundary.v1`
`Authority`: `Derived`
`RouteKeys`: `feishu`, `lark`, `external-adapter`, `dry-run`
`EvidenceLevel`: `S1`
`ContentHash`: `9d807c344b24436c35835ed69f5cad65159b4c40c956ac12288de4fac5d071a1`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Documentation/ES_AUTOMATION_CENTER_STANDARD.md` (`fc2da7d1f70575744515c6ecbabb878c407ccdeebacd9b4bd39f5da84aea89cf`)

`EvidenceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs`（本地 SHA-256：`37aa6c084a1047a00c30975487265cdc24c0141ea64eaed1f323a42559f94a43`）
- `ES/Automation/Workers/Node/Feishu/worker.js`（本地 SHA-256：`16b419b0452b9761e9d8f08acbf1e65ff815d8ac3043773df3e146786a4c887e`）
- `ES/Automation/Workers/Node/Feishu/package-lock.json`（本地 SHA-256：`f12bad503b40ce56b7dedf47bb7e98846d10dbcf4f00bcd95ed6881f98ed9f40`）
- `ES/Automation/Workers/Node/Feishu/tests/dry-run-input.json`（本地 SHA-256：`8fd40a68fbc03f0bba0536620f2496069c771f39004801d1c18cdf82a947667f`）
- Node Worker DryRun：`auth-status` 返回 `exitCode=0`、`status=DryRun`、`networkCalled=false`；输出位于项目 `ES/Automation/Temp/Feishu/<runId>/`，尚无真实 Feishu 网络证据。

`StaleWhen`: Feishu 官方 SDK/API、凭据策略、目标接入类型、TaskContract 或任一 SourceRef 哈希变化。

## 规划中的第一阶段操作

```text
auth status
knowledge search
knowledge pull
```

第一阶段已实现并仅允许 `auth-status`、`knowledge-search`、`document-pull` 三个只读操作；`knowledge publish` 与 `message send` 仍未开放。计划默认只读；发布、发消息和修改远端知识库必须显式确认。

## ES 接入链

```text
AIBrain
  -> AICommand / TaskContract
  -> ESAutomationCenter 注册任务
  -> ESAutomationFacade
  -> Feishu Adapter Worker
  -> 结构化输出 + RunRecord
```

不得让 AI 直接调用 CLI、ProcessRunner 或任意脚本路径。

## 凭据与证据

- 凭据只允许环境变量或 Windows 凭据管理器。
- 不得进入命令行参数、输入 JSON、普通日志、Knowledge 或 Git。
- 每次运行必须有 DryRun、超时、取消、退出码、输入/输出 Hash 和失败报告。
- Feishu 内容必须标记为外部缓存或协作记录，不能提升为 ES 源事实。

## 当前状态

官方 `@larksuiteoapi/node-sdk@1.73.0` 已下载到受管 Node Worker 工作区；C# TaskContract、WorkerAdapter、Facade Endpoint 和 RunRecord 接入源码已完成并通过生成工程编译。用户级 `ES_AUTOMATION_NODE_PATH` 已指向受审查的 Node 入口，但当前 Unity 进程需重启后才能继承；Feishu 凭据、Unity 受管运行和真实网络调用尚未验收，因此本条目不代表外部服务已连通。
