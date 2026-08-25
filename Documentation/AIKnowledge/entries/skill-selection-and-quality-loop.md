# Skill 定向选择、拆分与质量迭代

状态：候选合同 / 结构验证已执行，代表任务质量闭环未完成。

`KnowledgeId`: `es.skill.selection-quality.v1`
`EvidenceLevel`: `S1`
`Authority`: `Derived`
`RouteKeys`: `skill`, `routing`, `validation`, `iteration`
`ContentHash`: `0d76a288c1a87585f1006c6522957375807af48a66ae1cda8e8e04345b5ecf0d`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`025244bd6a0ec254e8cde057bf7c782dd6e6cbc96b29fb2514de3bed172bc6fb`)
- `.agents/skills/es-generate-agent-artifacts/SKILL.md` (`b2c08931566c9cdd9b6ad5f8cea2dda0be00cb2358468ee8dac841fb5469c60e`)

`EvidenceRefs`: 三个候选 Skill 的 `quick_validate.py` 结构验证输出；未执行代表任务、非触发任务和失败注入。

`StaleWhen`: Skill 目录合同、候选生成合同、验证器、候选正文或任一 SourceRef 哈希变化。

## 选择方式

AIBrain 不加载全部 Skill，而是使用 KnowledgeIndex 的 `routeKeys`、`relatedSkills` 和任务领域匹配最小集合。

```text
任务意图
  -> Knowledge routeKeys
  -> RequiredReads + RelatedSkills
  -> 可选 AICommand / TaskContract 受管通道协议
  -> Skill 工作流
```

Skill 的等级、证据和恢复义务以 `es-skill-governance` 为治理权威；创建、升级和结构验证以 `es-skill-creator` 为工具权威。两者协同但不扩大 AICommand 或用户授权。

## Skill 拆分规则

- `SKILL.md` 只保留触发边界、核心流程、权限和失败处理。
- 领域细节进入一层 `references/`。
- 重复且确定性的动作才进入 `scripts/`。
- 不能为了“完整”复制 AIWarnings、AICommand 或源码。
- 禁止建立包办所有领域的万能 Skill。

## 质量闭环

```text
候选生成 -> UTF-8/结构验证 -> 代表任务前测 -> 失败样本记录 -> 修订 -> 再测 -> Diff Review -> 按当前用户目标决定候选保留或正式写入
```

每轮至少记录：触发是否准确、是否读取了正确 Knowledge、是否越权、输出是否可验证、失败是否可恢复。

## 正式导入边界

AI 自主或 `ManagedAIBrain` 候选生成先写入 `ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/`。当前用户明确要求正式创建、导入或更新 Skill 时，可在验证和 Diff Review 后直接写入 `.agents/skills`，不再索取项目内二次批准。
