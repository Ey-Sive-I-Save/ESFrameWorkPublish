# Feishu CLI 外部适配器边界

状态：候选接入已实现 / 待 Unity 受管运行与真实认证验收。

`KnowledgeId`: `es.feishu.adapter-boundary.v1`
`Authority`: `Derived`
`RouteKeys`: `feishu`, `lark`, `external-adapter`, `dry-run`
`EvidenceLevel`: `S1`
`ContentHash`: `08d52951a7f03caf4983af215bc2f908e5d8e5696bec49186204d7092b71f8a9`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Documentation/ES_AUTOMATION_CENTER_STANDARD.md` (`fda3f8e4408e507fd257bb4093b8e19f83c1374834578639b443b52690280121`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs` (`4abfcc41a21a17e289e617451c8d06e119de8ac5e0b4d1635d93dc8e049e11b0`)
- `ES/Automation/Workers/Node/Feishu/worker.js` (`6648314a09548129bbb70a3399644b5b03375c8a9b1e635e3a69bbb89b086033`)
- `ES/Automation/Workers/Node/Feishu/package-lock.json` (`f12bad503b40ce56b7dedf47bb7e98846d10dbcf4f00bcd95ed6881f98ed9f40`)
- `ES/Automation/Workers/Node/Feishu/tests/dry-run-input.json` (`46e8b8fa3eb7c7b55a24f27cde81ff285decbdad09eb08e493649d910f7afde2`)

`EvidenceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs`（规范化 SHA-256：`4abfcc41a21a17e289e617451c8d06e119de8ac5e0b4d1635d93dc8e049e11b0`）
- `ES/Automation/Workers/Node/Feishu/worker.js`（规范化 SHA-256：`6648314a09548129bbb70a3399644b5b03375c8a9b1e635e3a69bbb89b086033`）
- `ES/Automation/Workers/Node/Feishu/package-lock.json`（本地 SHA-256：`f12bad503b40ce56b7dedf47bb7e98846d10dbcf4f00bcd95ed6881f98ed9f40`）
- `ES/Automation/Workers/Node/Feishu/tests/dry-run-input.json`（规范化 SHA-256：`46e8b8fa3eb7c7b55a24f27cde81ff285decbdad09eb08e493649d910f7afde2`）
- 当前 Worker 指纹变更后尚未执行 Node DryRun、Unity 受管运行或真实网络请求；旧 DryRun 不能证明当前实现。

`StaleWhen`: Feishu 官方 SDK/API、凭据策略、目标接入类型、TaskContract 或任一 SourceRef 哈希变化。

## 规划中的第一阶段操作

```text
auth status
knowledge search
knowledge pull
```

第一阶段只读合同仍仅允许 `auth-status`、`knowledge-search`、`document-pull`；它不能发送消息或修改知识。单人纯文本消息已由独立 `es.feishu.message.send@1` L3 合同形成静态实现候选，必须走独立 AICommand、角色许可、DryRun 和一次性授权；真实认证与消息送达仍未验收。`knowledge publish` 继续未开放。

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
- Live 运行必须绑定租户 Hash、空间白名单策略 Hash、PlanHash 和稳定 InvocationId。
- 每次成功运行必须有规范化输出、`feishu-receipt.json`、输入/输出 Hash、分类、净化版本、SourceRef 和未证实项。
- Feishu 内容固定标记为 `ExternalCollaboration`；搜索/文档只落盘白名单字段和受限文本，不能提升为 ES 源事实。

## 当前状态

官方 `@larksuiteoapi/node-sdk@1.73.0` 锁定在受管 Node Worker 工作区。当前源码已补 `ExternalRead` 能力、输入字段/InvocationId 拒绝、租户与空间策略绑定、目录/reparse 边界、文档空间归属复核、凭据模式脱敏、256 KiB 文档上限、哈希化 SourceRef 和 RunRecord 外部回执投影。生成工程静态编译不等于 Node、Unity、凭据、租户权限、真实网络、取消或 Domain Reload 已验收，因此本条目不代表外部服务已连通。
