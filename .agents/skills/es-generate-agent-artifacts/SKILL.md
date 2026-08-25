---
name: es-generate-agent-artifacts
description: Generate review-only ESFramework AICommand and Agent Skill candidate packages from Agent Authoring Graph generation-request.json files. Use when a Graph/Cmd Agent request asks Codex to create or revise AICommands or project Agent Skills. Candidate isolation constrains autonomous Graph generation; a current user request may explicitly authorize formal target paths without a second approval.
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
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Candidate mode limits what this Skill initiates autonomously. A current user request may directly authorize bounded candidate, source, Assets or governance changes; Runtime, external, destructive and Git actions must be explicitly named.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

When this Skill runs autonomously from a Graph/Cmd Agent generation request, generate candidates only and never approve or write directly to the formal AICommand or Agent Skill directories. A current explicit user request may instead name bounded formal targets; validate those outputs and present their diff for review without inventing a second approval step.

## Workflow

1. Select `AutonomousCandidate` only for a Graph/Cmd Agent launch; select `UserDirectedFormal` only when the current user explicitly names bounded formal targets.
2. For `AutonomousCandidate`, read `Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md`, the request's `generation-request.json`, every required Reference, and [generation-contract.md](references/generation-contract.md).
3. For `AutonomousCandidate`, reconstruct the requirement mind map from `relations`; reject missing, disconnected, contradictory, or cross-stage relationships and reject paths outside the request's `candidate/` directory.
4. For `UserDirectedFormal`, read the current target artifacts and applicable AIWarnings/validation contracts; do not require a candidate request or infer additional formal targets.
5. Generate only the artifacts declared by the selected mode, preserving connected context, detailed fields, and validation gates.
6. In `AutonomousCandidate`, create `candidate-manifest.json` and `validation-report.md`, then run [the candidate packet validator](scripts/Test-ESGenerationCandidatePacket.ps1) for path isolation, target allowlists, and declared user scope.
7. In `UserDirectedFormal`, restrict writes to the named formal targets, run their formal validators, and provide a Diff Review. The current explicit request is sufficient and does not require a second project approval.
8. Run all safe read-only validation available in the current environment and report the selected mode, exact outputs, failures, and non-claims.

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

- In autonomous/Graph-triggered candidate mode, do not modify `Assets/Plugins/ES/AICommands` or `.agents/skills` directly. A current explicit user request may authorize named formal paths; keep the write bounded to those paths and validate the resulting diff.
- Do not invoke Git staging, commit, reset, clean, push, or release operations.
- Do not edit generated `.csproj` files.
- Do not generate gameplay `ESCommand`, `ESSkillConfigKey`, Graph Runtime, Runner, Story Runtime, or BehaviorTree Runtime code.
- Do not mark candidates as approved.
- `scripts/Test-ESGenerationCandidatePacket.ps1` 通过前不得交付候选包。
- Stop when required context is missing or target paths conflict with the GenerationSpec.

## Validation

Check strict UTF-8, U+FFFD, target allowlists, manifest completeness, AICommand metadata, Skill frontmatter, direct skill folder naming, and referenced project paths. Record unavailable validation as not run rather than claiming success.
