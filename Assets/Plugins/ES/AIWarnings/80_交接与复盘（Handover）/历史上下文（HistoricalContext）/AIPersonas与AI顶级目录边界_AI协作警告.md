# AIPersonas 与 AI 顶级目录边界 AI 协作警告

## 结论

ES 当前允许存在四个 AI 顶级协作目录：

```text
Assets/Plugins/ES/AIWarnings
Assets/Plugins/ES/AICommands
Assets/Plugins/ES/AITalk
Assets/Plugins/ES/AIPersonas
```

这四个目录合理，但职责必须严格分开。后续 AI 不允许把人设、命令、警告和会话记录混成一个东西。

## 四类目录职责

```text
AIWarnings：
长期项目理解、架构纠偏、系统边界、禁止事项、过时思想修正。

AICommands：
可复制给 AI 的任务执行协议。它决定能不能改代码、必须读哪些文件、怎么验证、怎么交付。

AITalk：
多个 AI 在同一个 Session 里连续交流、写 Messages、沉淀 Consensus。它记录过程，不替代最终代码验证。

AIPersonas：
交互人设和表达风格。它只改变语气、分析节奏、提问方式和反馈口吻。
```

## 当前 AIPersonas 结构

```text
Assets/Plugins/ES/AIPersonas/README.md
Assets/Plugins/ES/AIPersonas/当前人设.md
Assets/Plugins/ES/AIPersonas/人设切换_复制模板.md
Assets/Plugins/ES/AIPersonas/冷静架构师_Persona.md
Assets/Plugins/ES/AIPersonas/磁小鬼_Persona.md
Assets/Plugins/ES/AIPersonas/可爱正太_Persona.md
```

## 使用顺序

当用户同时给出 Persona 和 AICommand 时，AI 必须按以下顺序处理：

```text
1. 先读取 Persona 文件全文，确认本轮交互口吻。
2. 再读取 AICommand 文件全文，确认任务权限、必读文件、验证要求。
3. 再读取 AIWarnings 中被命令要求的规则。
4. 执行任务时以本地源码和当前工作树为准。
5. 交付时仍按 AICommand 要求报告改动、验证和风险。
```

## 冲突优先级

```text
项目安全规则 > AIWarnings 项目事实 > AICommands 执行协议 > AITalk 会话规则 > AIPersonas 表达风格
```

解释：

```text
1. Persona 不能授权改代码。
2. Persona 不能要求跳过编译、跳过测试、隐藏风险。
3. Persona 不能覆盖 AIWarnings 里的项目事实和禁止事项。
4. Persona 不能让 AI 编造源码、编造验证结果或忽略脏工作树。
```

## 一键切换的正确理解

`AIPersonas/人设切换_复制模板.md` 是给开发者复制到 AI 窗口的提示模板。它不是运行时代码，也不会让所有 AI 窗口自动切换。

正确用法：

```text
请应用 Persona：
Assets/Plugins/ES/AIPersonas/冷静架构师_Persona.md

然后执行 AI 命令：
Assets/Plugins/ES/AICommands/<某个命令>.md

需求：
<具体需求>
```

## Persona 安全边界

```text
1. 冷静架构师：先结论后原因，重视边界、维护成本和验证。
2. 磁小鬼：轻快、嘴硬、轻度吐槽、主动抓坑点，但不能持续辱骂、性羞辱或人身攻击。
3. 可爱正太：温和、乖巧、少年感、好理解，但不能成人化、暧昧化、恋爱化或使用依附化称呼。
```

## 禁止事项

```text
1. 禁止把 Persona 写成工作职责文件；职责属于任务、系统文档或 AICommands。
2. 禁止把 Persona 写成架构事实；架构事实属于 AIWarnings。
3. 禁止把 Persona 写成多人会话记录；会话记录属于 AITalk。
4. 禁止把 Persona 当作跳过安全规则、编译验证、脏工作树检查的理由。
5. 禁止把人设口吻写进代码命名、正式注释、资产名或运行时文本，除非用户明确要求且内容适合项目。
```

## 后续维护建议

```text
1. 新增 Persona 时先改 AIPersonas/README.md，再新增具体 Persona 文件。
2. 每个 Persona 必须包含：定位、交流风格、分析习惯、提问方式、执行节奏、禁止事项、启动提示。
3. 只要新增了会影响 AI 使用方式的人设规则，就同步更新本警告或 README。
4. 不建议继续增加新的 AI 顶级目录；优先在现有四类目录下分子目录。
```
