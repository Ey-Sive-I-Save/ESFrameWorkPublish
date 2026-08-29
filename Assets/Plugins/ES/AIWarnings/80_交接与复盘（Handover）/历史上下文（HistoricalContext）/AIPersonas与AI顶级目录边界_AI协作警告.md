# 历史上下文：AIPersonas 与 AI 顶级目录边界

Status: historical
StableId: es.aiwarnings.handover.ai-personas-top-level-boundary.v1
Authority: ESFramework AIWarnings / historical handover
RouteKeys: aiwarnings, handover, historical, aipersonas, aicommands, aitalk, boundary
Applicability: AIWarnings、AICommands、AITalk、AIPersonas 的目录与协作分工
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-handover-ai-personas-top-level-boundary.md`
StaleWhen: 四类 AI 目录结构、会话协议或 SourceRefs 变化
Knowledge: `es.aiwarning.handover.ai-personas-top-level-boundary.v1`

## 不可混淆的长期边界

- 四个 AI 顶级目录职责分离：AIWarnings 保存长期约束/事实，AICommands 保存执行协议，AITalk 保存会话过程/共识，AIPersonas 只保存表达风格；不得互相冒充。
- Persona 不能授权改代码、跳过验证、覆盖 Warning、隐藏脏工作树或编造事实；安全规则 > Warning 事实 > Command 协议 > Talk 会话 > Persona 口吻。
- 同时出现 Persona 与 AICommand 时，先读 Persona，再读 Command 和其要求的 Warning；最终以源码、当前工作树和命令合同交付。
- 人设切换模板只是复制提示，不是运行时代码或全局窗口开关；不得把人设口吻写进代码、资产名或运行时文本，除非用户明确要求。

详细目录结构、使用顺序、Persona 安全要求和维护建议见 Knowledge。
