---
name: es-generate-agent-artifacts
description: Generate review-only ESFramework AICommand and Agent Skill candidate packages from Agent Authoring Graph generation-request.json files. Use when a Graph/Cmd Agent request asks Codex to create or revise AICommands under Assets/Plugins/ES/AICommands or project Agent Skills under .agents/skills, while keeping all writes isolated under ES/Automation/Candidates/AgentAuthoring until Unity Diff Review and explicit human approval.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Generate ES Agent Artifacts

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Generate candidates only. Never approve or write directly to the formal AICommand or Agent Skill directories.

## Workflow

1. Read `Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md` completely.
2. Read the request's `generation-request.json` and every required Reference.
3. Read [generation-contract.md](references/generation-contract.md).
4. Reconstruct the requirement mind map from `relations`; preserve which References and Constraints feed each OutputArtifact.
5. Reject missing, disconnected, contradictory, or cross-stage relationships instead of flattening or guessing them.
6. Reject paths outside the request's `candidate/` directory.
7. Generate every declared OutputArtifact using its connected context, detailed fields, and validation gates.
8. Create `candidate-manifest.json` and `validation-report.md`.
9. Run safe read-only validation available in the current environment.
10. Report that formal import still requires Unity Diff Review and explicit human approval.
    使用 [候选产物验证器](scripts/Test-ESGenerationCandidatePacket.ps1) 检查 candidate 路径隔离、目标白名单和人工批准门禁。

## AICommand candidates

Include these exact metadata labels with non-empty values:

```text
命令类型：
默认改文件：
风险等级：
```

Also include mandatory reads, execution boundaries, delivery format, and a requirement placeholder where appropriate. Reference only live project paths.
Use the declared expected inputs, execution outline, acceptance criteria, and connected Constraint verification steps. Do not replace them with generic boilerplate.

## Agent Skill candidates

Use a direct `.agents/skills/es-*/` folder. Include:

```text
SKILL.md
agents/openai.yaml
```

Keep `SKILL.md` concise. Its YAML frontmatter contains only `name` and `description`. Add one-level `references/`, `scripts/`, or `assets/` only when the workflow genuinely needs them. Do not add a README, changelog, installation guide, hidden binary, or session output.
Use the declared trigger scenarios, workflow, non-goals, validation steps, and connected requirement branches to define the final Skill boundary.

## Hard boundaries

- Do not modify `Assets/Plugins/ES/AICommands` or `.agents/skills` directly.
- Do not invoke Git staging, commit, reset, clean, push, or release operations.
- Do not edit generated `.csproj` files.
- Do not generate gameplay `ESCommand`, `ESSkillConfigKey`, Graph Runtime, Runner, Story Runtime, or BehaviorTree Runtime code.
- Do not mark candidates as approved.
- `scripts/Test-ESGenerationCandidatePacket.ps1` 通过前不得交付候选包。
- Stop when required context is missing or target paths conflict with the GenerationSpec.

## Validation

Check strict UTF-8, U+FFFD, target allowlists, manifest completeness, AICommand metadata, Skill frontmatter, direct skill folder naming, and referenced project paths. Record unavailable validation as not run rather than claiming success.
