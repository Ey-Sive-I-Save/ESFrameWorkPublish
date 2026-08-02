---
name: es-worktree-audit
description: Audit the ESFramework Git worktree before edits, reviews, builds, or handoff. Use when the repository is dirty, multiple agents may be working, the user asks what changed, or a task must avoid overwriting staged, unstaged, deleted, renamed, or untracked files.
---

# Audit the ES Worktree

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

## Delivery

Report branch, HEAD, staged/unstaged/untracked/deleted counts, target overlap, and any preservation risk.
