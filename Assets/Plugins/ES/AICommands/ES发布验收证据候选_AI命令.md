---
name: es-release-acceptance-evidence-candidate
description: Assemble an ES release acceptance evidence candidate without executing Unity or release operations.
---

## Direct effect contract

Command type: candidate evidence generation. It requires AIBrain `planTask` and writes only `ES/Automation/Candidates/ReleaseAcceptance/<request-id>/candidate/`.
命令类型：候选内容生成：ES 发布验收证据候选。
默认改文件：仅允许 `ES/Automation/Candidates/ReleaseAcceptance/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

- `.agents/skills/es-release-acceptance/SKILL.md`
- `.agents/skills/es-skill-governance/references/es-preservation-refactor-contract.md`
- `.agents/skills/es-skill-governance/references/verification-semantics.md`
- `.agents/skills/es-skill-governance/references/evidence-receipt-contract.md`

## 交付格式

```ContractCompleteness
commandId: release.acceptance.evidence.candidate
cancellation: before commit; cancel leaves no formal release evidence
recovery: isolated candidate cleanup; NeedsReissue on uncertain state; no replay
validation: candidate schema, content hash, and isolated-path checks
evidenceRef: candidate path, SHA-256, receipt, and Static/Runtime status
allowRoots: ES/Automation/Candidates/ReleaseAcceptance/<request-id>/candidate/ only
denyPaths: .agents/skills, Assets/Plugins/ES/AICommands, Assets, Runtime, Git, release
deny-overrides: true
```

Produce a profile-specific matrix separating static proof, runtime-not-run, stale or missing receipts, compatibility claims, and release claims not proven. Never infer Unity, Player, Profiler, IL2CPP, or external-provider success from source-only evidence.

## Prohibitions

No Unity launch, build, publish, external process, release artifact mutation, or Accepted/Released decision by the command itself.
