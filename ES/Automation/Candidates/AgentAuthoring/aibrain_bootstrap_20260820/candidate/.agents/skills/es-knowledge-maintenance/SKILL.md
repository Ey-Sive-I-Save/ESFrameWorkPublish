---
name: es-knowledge-maintenance
description: Maintain targeted ESFramework AIKnowledge entries and route indexes with source references, content hashes, evidence levels, and stale conditions. Use when a task needs a minimal domain-specific knowledge pack, KnowledgeIndex updates, source/evidence reconciliation, or stale-entry review; do not use for ordinary summaries, direct Unity code changes, or unrestricted documentation scans.
---

# Maintain targeted AIKnowledge

1. Read `Documentation/AIKnowledge/README.md` and `KnowledgeIndex.yaml`.
2. Match the task to the smallest set of `routeKeys`; do not load the whole repository by default.
3. Read each entry's `RequiredReads` from current project paths, then verify source existence and authority.
4. Update only the declared AIKnowledge scope. Every changed entry must retain `KnowledgeId`, `Authority`, `SourceRefs`, `EvidenceRefs`, `ContentHash`, and `StaleWhen`.
5. Keep AIWarnings, AICommands, source code, and Unity evidence as external authorities. Link to them; do not copy them as a second rule system.
6. Run strict UTF-8, U+FFFD, reference-path, route-key, and scoped `git diff --check` validation.
7. Report changed entries, source hashes, evidence level, stale risks, and unavailable checks.

Non-goals: writing `Assets/`, formal `.agents/skills`, AIWarnings, Git history, Feishu, or Unity Runtime code.

For field details, read `references/knowledge-entry-contract.md`.
