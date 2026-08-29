# 检查：编译错误定位 AI 命令

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户需求超出本文范围，先说明需要换用哪个命令。
6. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：只读体检。
默认改文件：否，除非用户明确要求修复。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md
```

## 执行要求

```text
定位编译错误文件、行号、直接原因和最小修复建议。区分本任务相关错误和无关脏工作树错误。
```

## ContractCompleteness

```yaml
commandId: compile.error.review
writeMode: read-only
cancellation: N/A (read-only; no external effect; stop before analysis)
recovery: N/A (read-only; rerun from unchanged inputs; no rollback)
validation: read-only checks only; no writes, runtime, Git, release, or external effects
evidenceRef: source refs + SHA-256/content hash when available + read receipt; static evidence cannot claim Runtime
actionBoundary: AIBrain/ABCD selects intent and route; this command only reviews and reports; Automation/ABCC execution is out of scope
allowRoots: project files explicitly listed in 必须先读 and the contract's declared read-only targets only
denyPaths: source writes, undeclared paths, Git/history, release, Runtime/Unity, external services; deny-overrides
```
## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：只读时无需编译；修复时运行相关 dotnet build
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
