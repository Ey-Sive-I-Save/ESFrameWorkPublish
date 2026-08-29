# Feishu 只读知识接入 AI 命令

## 直接生效协议

当用户明确要求查询 Feishu/Lark 状态、搜索知识或拉取文档时，AI 必须：

```text
1. 先读取本文件全文、AIWarnings 启动链和 Feishu Knowledge 条目。
2. 只调用已注册的 es.feishu.read TaskContract，不接受脚本路径、Node 路径或任意命令行参数。
3. 默认 dryRun=true；真实网络读取必须由用户当前请求明确允许，并保留 RunId、输入/输出 Hash 和退出码。
4. 只允许 auth-status、knowledge-search、document-pull；禁止发送消息、发布、上传、删除或修改 Unity Assets。
5. 凭据只能从受管进程环境读取，不得写入请求、日志、Knowledge 或交付文本。
6. 超时、取消或输出校验失败时原样报告，不得伪造外部服务成功。
``` 

命令类型：安全执行。
默认改文件：否；只读外部知识接入，运行目录可以写入受管临时结果和 RunRecord。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md
Documentation/AIKnowledge/entries/feishu-adapter-boundary.md
.agents/skills/es-use-ai-command/SKILL.md
```

## 执行合同

```text
commandId: feishu.read
taskId: es.feishu.read
taskVersion: 1
入口：AIBrain planTask -> runTask -> ESAutomationFacade
允许操作：auth-status / knowledge-search / document-pull
默认：dryRun=true
输出：RunId、结构化结果路径、输入 Hash、输出 Hash、退出码、错误与待人工确认项
```

Skill 只提供 AICommand 选择和验证流程，不扩大 Feishu TaskContract 权限。

## 禁止事项

```text
- 禁止直接启动 Node、npm、npx 或任意外部命令。
- 禁止把 Feishu 内容当作 ES 源事实或覆盖 AIWarnings/源码。
- 禁止提交凭据、Cookie、Token 或完整 Authorization 头。
- 禁止把 DryRun 或静态构建结果写成真实网络已连通。
```

## ContractCompleteness

```text
commandId: feishu.read
cancellation: before remote request; cancellation returns Cancelled with no write claim.
recovery: request cursor/idempotency guarded; unknown result returns NeedsReissue, no duplicate pull.
validation: tenant/app identity, read-only contract, query bounds, freshness and response schema.
evidenceRef: commandBodyHash, planHash, RunId, source document IDs/content hashes and receipt.
allowRoots: Feishu read/pull response and受管 local cache only.
denyPaths: remote mutation, local source writes, Git and release; deny-overrides.
```

## 交付格式

```text
1. AIBrain PlanHash 与 TaskContract。
2. RunId、操作类型、DryRun、退出码和结果状态。
3. 结果文件及 Hash；无真实网络证据时明确写无。
4. 凭据、网络、Node 版本和 Unity 受管进程的剩余缺口。
```
