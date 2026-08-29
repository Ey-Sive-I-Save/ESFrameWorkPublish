# 历史交接：AIPersonas 与 AI 顶级目录边界

`KnowledgeId`: `es.aiwarning.handover.ai-personas-top-level-boundary.v1`  
`Authority`: `AIWarnings historical handover + current AI directory source`  
`RouteKeys`: `aiwarnings`, `handover`, `historical`, `aipersonas`, `aicommands`, `aitalk`, `boundary`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `0d9397f51649588bdca2830c8b95a982da8a1de5f8385b96a0eb73b21b64a05d`  
`SourceSetHash`: `0d9397f51649588bdca2830c8b95a982da8a1de5f8385b96a0eb73b21b64a05d`  
`EntryBodyHash`: `b121682542b0945bb695d47d17f7d77eec2771354b218ece4987b0293aab9333`  
`StaleWhen`: `四类 AI 目录结构、会话协议或 SourceRefs 变化。`

## 保真迁移

原 Warning 113 行、3,883 UTF-8 字节；现 Warning 保留历史性质、目录分工、权限边界和 Knowledge 导航。详细目录清单、使用顺序和 Persona 约束迁移到本条目，不把历史建议写成运行时事实。

## 四类目录与优先级

- `AIWarnings`：长期项目事实、架构边界和禁止事项；`AICommands`：任务权限、必读、验证与交付协议；`AITalk`：Session 消息与 Consensus 过程；`AIPersonas`：语气、节奏、提问方式和反馈口吻。
- 冲突优先级为项目安全规则 > AIWarnings 事实 > AICommands 协议 > AITalk 规则 > AIPersonas 风格。Persona 永远不能授权写入、跳过编译/测试、覆盖 Warning 或制造验证结果。
- 同时提供 Persona 与 Command 时，读取 Persona 全文确认表达，再读取 Command 全文确认权限/必读/验证，再读取命令指向的 Warning；执行和交付以当前源码、工作树和合同为准。

## Persona 与维护

- 人设切换模板只是复制给 AI 窗口的提示，不会自动切换所有窗口。Persona 不应承担职责、架构事实或多人会话记录，也不应把口吻写进代码命名、正式注释、资产名或运行时文本。
- 新 Persona 需包含定位、交流风格、分析习惯、提问方式、执行节奏、禁止事项和启动提示；优先扩展现有四类目录，不新增平行顶级目录。当前目录/文件清单变化需回读 README。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AICommands`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/AIPersonas与AI顶级目录边界_AI协作警告.md` (`f366285980c1fcbd1e1c282cec96dc2b75d07b940ac9faad1e022b4f46937abb`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
