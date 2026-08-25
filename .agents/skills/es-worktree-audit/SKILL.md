---
name: es-worktree-audit
description: Audit the ESFramework Git worktree before edits, reviews, builds, or handoff. Use when the repository is dirty, multiple agents may be working, the user asks what changed, or a task must avoid overwriting staged, unstaged, deleted, renamed, or untracked files.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Audit the ES Worktree

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The audit itself is read-only. The current explicit user request directly authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; managed-channel contracts are not secondary approval.

## Execution classification

- This audit is a read-only repository inspection and may run directly without `planTask` or an AICommand.
- After the audit, the current explicit user request may proceed across the project paths strictly necessary for its goal. The compatibility scope validator records containment and expansion; its path classes and size thresholds do not veto user-directed source, Assets, governance or audit-state changes.
- Delete, rename, Git, Unity/Runtime, external-process, network, release and credential actions require action-specific user wording. If an AIBrain/Worker channel is selected, satisfy its plan and contract protocol without requesting a second user approval.

Establish ownership and overlap before changing files. Git remains the source-state authority.

## Workflow

1. Run `scripts/Get-ESWorktreeImpact.ps1 -ProjectRoot <root> -Json`.
2. Inspect the target paths with `git diff -- <paths>` and `git diff --cached -- <paths>`.
3. Classify changes as task-related, overlapping, or unrelated. Existing changes belong to the user unless proven otherwise.
4. Continue around unrelated changes. Stop and ask only when the requested edit would overwrite or invalidate overlapping work.
5. After editing, rerun the audit and verify only intended files changed.

## Rules

- Do not use destructive reset, checkout, clean, delete, or broad restore commands.
- Do not infer authorship from timestamps or formatting.
- Do not stage, commit, amend, or push unless the user explicitly asks.
- Do not use a summary in place of inspecting overlapping diffs.
- Keep the local documentation ledger synchronized when the project workflow requires it, but do not advance HTML integration prematurely.

## SmallTool controls

- **Scope**: read one repository and the explicitly named target paths; do not scan unrelated repositories or user directories.
- **Side effects**: none. The script and Git inspection are read-only; staging, restoration, cleanup and history mutation are prohibited.
- **Bounded scale**: report aggregate counts first and inspect diffs only for target overlaps. Stop when the root is invalid, Git is unavailable or output would exceed the declared path scope.
- **Repeatability**: reruns are safe snapshots, but a changed HEAD/worktree invalidates the previous result. Concurrent changes must be reported, not merged or attributed.
- **Required cases**: clean tree, dirty target overlap, invalid root, denied destructive expansion and repeated audit with unchanged state.

## Delivery

Report branch, HEAD, staged/unstaged/untracked/deleted counts, target overlap, and any preservation risk.


## Specialized static acceptance

Acceptance ID: `worktree-boundary`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- worktree
- scope
- untracked
- generated output
- reversible

Required specialized cases: `tracked-untracked, scope-expansion, generated-output, encoding-drift, reversible-recovery`
Guidance: `references/static-specialized-acceptance.md`
