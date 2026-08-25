---
name: es-test-fixture-candidate
description: Generate an isolated ES test-fixture candidate and deterministic replay packet.
---

## Direct effect contract

Command type: candidate content generation. It requires AIBrain `planTask` and writes only `ES/Automation/Candidates/TestFixtureAuthoring/<request-id>/candidate/`.
命令类型：候选内容生成：ES 测试夹具候选创建。
默认改文件：仅允许 `ES/Automation/Candidates/TestFixtureAuthoring/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

- `.agents/skills/es-test-fixture-authoring/SKILL.md`
- `.agents/skills/es-static-deep-replay/SKILL.md`
- `.agents/skills/es-skill-governance/references/es-preservation-refactor-contract.md`

## 交付格式

Produce fixture inputs, expected outputs, invalid and denial cases, idempotency key, interruption recovery, source snapshot, deterministic replay results, and runtime claims not proven. Fixtures must target existing ES contracts rather than introduce a parallel execution path.

## Prohibitions

No production test replacement, no Unity or external process launch, no generated fixture outside the candidate root, and no acceptance claim without the registered verifier.
