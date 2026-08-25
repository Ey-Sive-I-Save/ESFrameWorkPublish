---
name: es-automation-worker-candidate
description: Generate an isolated candidate packet for an ES Automation Worker, TaskContract, RunRecord, or recovery boundary.
---

## Direct effect contract

Command type: candidate content generation. It must run through AIBrain `planTask` and the matching TaskContract. It only writes `ES/Automation/Candidates/WorkerAuthoring/<request-id>/candidate/`; it never changes production workers or starts a process.
命令类型：候选内容生成：ES 自动化 Worker 候选设计。
默认改文件：仅允许 `ES/Automation/Candidates/WorkerAuthoring/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

- `.agents/skills/es-automation-worker-authoring/SKILL.md`
- `.agents/skills/es-skill-governance/references/es-preservation-refactor-contract.md`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs`

## 交付格式

Produce a candidate manifest, contract diff, allowed roots, cancellation/recovery matrix, StaticDeepReplay results, and separate runtime claims not proven. Candidate output must preserve existing ES TaskContract, Facade, Worker registration, and ProcessRunner boundaries.

## Prohibitions

No direct `Process.Start`, no Unity launch, no production Asset edits, no arbitrary command arguments, and no claim of runtime acceptance.
