---
name: es-feishu-cli
description: Design and invoke a read-first Feishu/Lark external adapter through ESAutomationCenter and registered TaskContracts with DryRun, credential isolation, timeout, cancellation, RunRecord, and explicit confirmation. Use when searching or synchronizing Feishu knowledge, checking auth, preparing a dry-run publish, or sending an approved message; do not use for direct ProcessRunner calls, arbitrary scripts, credential handling in prompts, or Unity Runtime integration.
---

# Use the managed Feishu adapter

1. Read `Documentation/AIKnowledge/entries/feishu-adapter-boundary.md` and the current ESAutomationCenter contract.
2. Resolve one registered TaskContract and verify its version, entry fingerprint, capability, and `allowAiInvoke` policy.
3. Keep credentials in environment variables or Windows Credential Manager. Never place them in command arguments, JSON, logs, Knowledge, or Git.
4. Default to read-only operations: `auth status`, `knowledge search`, and `knowledge pull`.
5. For publish or send operations, create a DryRun first and require explicit confirmation before the real request.
6. Enforce timeout, cancellation, bounded stdout/stderr, exit-code capture, input/output hashes, and RunRecord reporting.
7. If process-tree termination is not confirmed, report failure; never claim Cancelled or success.
8. Mark returned Feishu material as external/derived knowledge. It cannot override source, AIWarnings, or Unity evidence.

Non-goals: direct CLI paths, arbitrary ProcessRunner calls, Unity Asset writes, Runtime dependencies, or implicit publish.

Read `references/adapter-contract.md` for the operation boundary.
