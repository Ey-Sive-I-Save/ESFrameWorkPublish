---
name: es-use-ai-command
description: Select, validate, and execute one ESFramework AICommand as the task authorization contract. Use when the user provides an Assets/Plugins/ES/AICommands path, asks to choose or run an AICommand, or requests an ES project task that should follow an existing command template.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Use an ES AICommand

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

Treat the selected AICommand as the project-specific task contract. Do not infer permission from the file name.

## Workflow

1. Resolve the Git repository root and confirm it is the ESFramework project.
2. For ordinary command selection, do not run the full-library validator: it deliberately reads every contract body and is a CI/library-maintenance gate. Run `scripts/Test-ESAICommands.ps1 -ProjectRoot <root>` only for catalog maintenance, CI, or a suspected command-library defect.
3. Read these live project files with explicit UTF-8:
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
4. If the user supplied a command path, verify it with `scripts/Find-ESAICommands.ps1 -ProjectRoot <root> -CommandPath <path> -Json` before reading it completely. Otherwise use `scripts/Find-ESAICommands.ps1 -ProjectRoot <root> -Query <task terms> -Json` first; it loads only the compact discovery JSON and validates its referenced path metadata without reading any contract Markdown body, then returns at most six candidates. Select exactly one entry, then read only that Markdown contract in full. Read the full `AICommandCatalog.json` only for an explicit catalog-maintenance or exhaustive-browse request. `README.md` and `命令合集索引_AI命令.md` are navigation documents, not authorization contracts.
5. Recompute the selected Markdown SHA-256 immediately before relying on it. Restate the command ID, path, hash, command type, write permission, risk level, required inputs, required reading, affected paths, and verification contract.
6. Inspect the worktree before editing. Preserve unrelated user or agent changes.
7. Apply only the intersection of the user's request and the command's authorization. Ask before proceeding when a required parameter is genuinely missing.
8. Run the verification required by the command. Keep `.csproj` compilation, Unity Editor compilation, Test Runner, PlayMode, Profiler, IL2CPP, and release evidence distinct.

## Rules

- Never execute multiple commands as a combined permission grant.
- Never let an AICommand override P0 AIWarnings or current source facts.
- Never modify files when the command is read-only unless the user separately authorizes the change.
- Never write or restore AI collaboration history unless the user explicitly requests it.
- Report missing command references as a command-library defect; do not silently guess replacements.

## Workflow controls

- **Owners**: AICommand maintainers own catalog integrity; the task requester accepts the selected contract and resulting scope.
- **Inputs/outputs**: accept one task description or exact command path; return one verified command reference, bounded execution scope and evidence report.
- **Write boundary**: discovery and validation are read-only. Writes require the intersection of user authorization, the selected command and P0 rules; publish, delete, Git and external network remain separate permissions.
- **Recovery/idempotency**: selection may be repeated safely. If the catalog, command hash, worktree or PlanHash changes, discard the selection and re-plan; never continue a partially authorized execution.
- **Scale**: discovery returns at most six candidates and selects exactly one. No batch command aggregation, implicit retries or concurrent write contracts.
- **Required cases**: verify exact-path success, missing/malformed command, denied expansion, repeated selection with stable hash, changed-hash invalidation and interrupted execution before side effects.

## Delivery

Report: selected command, rules read, work performed, changed files, validation evidence, and remaining risks.

## Script

`scripts/Test-ESAICommands.ps1` validates the versioned catalog, navigation-role separation, strict UTF-8, required metadata, and project-relative references. It does not modify files.

`scripts/Find-ESAICommands.ps1` is the low-model-context discovery entry. It loads only the compact discovery JSON, validates each referenced path's managed-root and reparse-point boundary, returns only scored metadata for a hard-bounded maximum of six candidates, and never reads contract Markdown bodies. It is not an authorization shortcut: the selected contract must still be read in full and hashed immediately before execution.


## Specialized static acceptance

Acceptance ID: `aicommand-usage-boundary`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- AICommand
- authority
- target path
- dry-run
- developer authorization

Required specialized cases: `command-discovery, authority-match, target-path-boundary, runtime-authorization, dry-run-idempotency`
Guidance: `references/static-specialized-acceptance.md`
