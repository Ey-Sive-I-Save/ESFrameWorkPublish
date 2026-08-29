# 预备案提案：UnityMCP、AI 工程验收代理与自动化路线图
Status: proposed
StableId: es.aiwarnings.proposal.unitymcp-engineering-agent-roadmap.v1
Authority: ESFramework AIWarnings / proposal
RouteKeys: aiwarnings, proposal, unitymcp, agent, validation, automation, evidence, release
Applicability: UnityMCP、Agent Skills、AICommands、Unity 验收、资源发布与证据台账的未来设计
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-proposal-unitymcp-engineering-agent-roadmap.md`
StaleWhen: UnityMCP/AICommands/验收合同、CurrentStatus 或路线图发生变化
Knowledge: `es.aiwarning.proposal.unitymcp-engineering-agent-roadmap.v1`

## 提案边界（预备案，未实现）
- 目标链路是用户目标→AICommand 权限→RuleIndex/Skill 最小读取→安全事务→Unity 真实证据→分层验收→可追溯结果；本文件不是开发计划、授权、现行事实或已交付能力。
- 候选能力分为只读采证、受控验收和高风险自动化；每项必须声明输入、输出、超时、取消、回滚、失败语义和证据等级，禁止跨层升级结论。
- 进入实施前必须重新读取最新入口/P0/源码/AICommand，并取得本次明确授权；默认只读，资产写入、远端上传、删除和发布不得由提案推导。
- 禁止绕过 AICommand、P0、Undo、工作树或文档门禁；禁止递归吞入全库、伪造 Runtime/Profiler/IL2CPP/发布证据或把工具可调用当作授权。

详细候选能力、阶段优先级、ES 专项切入点与未来验收清单见 Knowledge。
