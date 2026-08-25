---
name: es-change-risk-register-candidate
description: Generate an isolated, auditable ES change risk register candidate.
---

## Direct effect contract

Command type: candidate content generation. It requires AIBrain `planTask` and writes only `ES/Automation/Candidates/RiskRegister/<request-id>/candidate/`.
命令类型：候选内容生成：ES 变更风险登记候选。
默认改文件：仅允许 `ES/Automation/Candidates/RiskRegister/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

- `.agents/skills/es-change-risk-register/SKILL.md`
- `.agents/skills/es-skill-governance/references/es-preservation-refactor-contract.md`
- `.agents/skills/es-skill-governance/references/commercial-controls.md`

## 交付格式

Produce owner, scope, risk, permission, budget, stop condition, rollback, compatibility, evidence, and unresolved-runtime fields. Do not silently convert a risk register into authorization; all production changes still require their own AICommand.

## Prohibitions

No production file edits, no deletion, no Git or Unity operation, and no Accepted/Released claim without external verification.
