# AIWarnings 更新与交接总结 AI 命令

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

命令类型：交接沉淀。
默认改文件：是，只允许改 AIWarnings/AICommands 文档。
风险等级：L1。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md
```

## 执行要求

```text
更新相关 AIWarnings。必须写模块定位、有效设计、过时设计、风险、入口文件、禁止事项、下一步。
```

命令 ID：`handover.update-aiwarnings`

## ContractCompleteness

```text
cancellation: before-commit only; after-commit returns RecoveryRequired.
recovery: reread target/hash, new idempotencyKey; no automatic replay.
validation: Test-ESAICommands.ps1 and Test-ESUtf8.ps1 with per-item result.
evidenceRef: commandBodyHash, planHash, writeScope, receipt and source SHA-256.
writeScope: AIWarnings/AICommands documentation only; deny source, Assets, Git, release and Runtime.
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：文档更新无需编译
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
