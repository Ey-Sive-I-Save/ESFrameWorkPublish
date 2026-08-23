# Skill 定向选择、拆分与质量迭代

状态：候选合同 / 结构验证已执行，代表任务质量闭环未完成。

`KnowledgeId`: `es.skill.selection-quality.v1`
`EvidenceLevel`: `S1`
`Authority`: `Derived`
`RouteKeys`: `skill`, `routing`, `validation`, `iteration`
`ContentHash`: `9dd2daf5748fc393af9a8d01d0f79eabca202071fc0607f0df9f88c3e703433b`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`8213b590650bbca456ce77f2545419e695ae736d979cfc03de08d17728c01cdf`)
- `.agents/skills/es-generate-agent-artifacts/SKILL.md` (`2962ac46089ada1a54a139b74cf9fa57f895aac955bf475b8a9f6d2cfd067e5d`)

`EvidenceRefs`: 三个候选 Skill 的 `quick_validate.py` 结构验证输出；未执行代表任务、非触发任务和失败注入。

`StaleWhen`: Skill 目录合同、候选生成合同、验证器、候选正文或任一 SourceRef 哈希变化。

## 选择方式

AIBrain 不加载全部 Skill，而是使用 KnowledgeIndex 的 `routeKeys`、`relatedSkills` 和任务领域匹配最小集合。

```text
任务意图
  -> Knowledge routeKeys
  -> RequiredReads + RelatedSkills
  -> AICommand 权限合同
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
候选生成 -> UTF-8/结构验证 -> 代表任务前测 -> 失败样本记录 -> 修订 -> 再测 -> Unity Diff Review -> 人工批准
```

每轮至少记录：触发是否准确、是否读取了正确 Knowledge、是否越权、输出是否可验证、失败是否可恢复。

## 正式导入边界

新 Skill 只能先写入 `ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/`，经候选验证、Diff Review 和明确批准后才导入 `.agents/skills`。
