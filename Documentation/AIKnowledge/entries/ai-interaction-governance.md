# AI 交互治理与任务收尾

`KnowledgeId`: `es.ai.interaction-governance.v1`  
`Authority`: `es-ai-interaction-governance` contract and project AGENTS closeout policy  
`RouteKeys`: `interaction`, `conversation`, `prompt`, `objective`, `verification`, `uncertainty`, `next-step`, `behavior-tree`, `numeric-selection`, `next-step-dispatch`, `context-collection`, `goal-drift`, `handover`, `closeout`, `evaluation`, `dialogue-quality`  
`EvidenceLevel`: `S1`  
`ContentHash`: `ac4e7a3732cb184e0d2841ce0f43a88ca420a44f997531555246004278e79371`
`StaleWhen`: 评价 Profile、下一步行为树、收尾合同、AGENTS 门禁或任一 SourceRef 哈希变化。

## Scope

本条目只负责用户提示词评价、任务目标清晰度、验证充分度、未证实项、目标漂移、交接摘要和下一步候选生成。评分是建议性证据，不授予权限，不替代 Skill 验证或 Runtime 验收。

## RequiredReads

- `.agents/skills/es-ai-interaction-governance/SKILL.md`
- `.agents/skills/es-ai-interaction-governance/references/interaction-governance-contract.md`
- `.agents/skills/es-ai-interaction-governance/references/evaluation-profiles.json`
- `.agents/skills/es-ai-interaction-governance/references/next-step-behavior-tree.json`

## SourceRefs

- `AGENTS.md` (`10de16335dc5eacbc13e943bd61b2c5cde770a1358cfc07612d697fe77f09ced`)
- `.agents/skills/es-ai-interaction-governance/SKILL.md` (`d4e38a3bf2e87a1737de68108c81187bf9e29fe12aeda2aa3f685dbe84e9bf6f`)
- `.agents/skills/es-ai-interaction-governance/references/interaction-governance-contract.md` (`ed6ff9cc6f71381883c2642da8d88b602e9a79c1bac106063dce30c418d1d816`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_AI协作历程与本地Session兜底恢复_AI协作警告.md` (`87f2289a4a7d597433a5593b54451738efb30adbe4bd0dcc53ef3fbd9c7498a0`)
- `.agents/skills/es-ai-interaction-governance/references/evaluation-profiles.json` (`b3d0a8bf3884991376f610e2da42e6dd91e575ca0e658ddfba510ec6cbfd5f60`)
- `.agents/skills/es-ai-interaction-governance/references/next-step-behavior-tree.json` (`e77e118c14a5f237e5641aadacac49b992761a3680e12816e527b0db94a558be`)
