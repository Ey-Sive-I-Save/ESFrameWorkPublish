# 对象池预热配置检查 AI 命令

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
默认改文件：否，补预热配置需用户确认。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md
Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md
```

## 执行要求

```text
检查对象池、PrefabPrewarm、GameManager 接入、运行时 Instantiate/Destroy、Space/场景切换清理策略。
```

## ContractCompleteness

```yaml
commandId: pool.prewarm.review
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
4. 验证结果：修复时编译 ES_Logic.csproj
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
