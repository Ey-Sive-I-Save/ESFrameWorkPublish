---
name: es-utf8-guard
description: Validate ESFramework text changes for strict UTF-8 decoding, Unicode replacement characters, likely mojibake, unintended broad rewrites, and git diff integrity. Use before or after editing Chinese source, Markdown, JSON, YAML, CSV, shaders, or scripts in the ES project.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Guard ES UTF-8 Text

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Execution classification

- Strict UTF-8 decoding, replacement-character scans, mojibake review, and `git diff --check` are read-only checks and may run directly.
- A current user request may modify any project text needed for its bounded goal, including source and governance. `Test-ESUserDirectedLowRiskPolicy.ps1` is a scope-closure aid, not a prerequisite or path allowlist; use `apply_patch` and preserve unrelated bytes.
- Delete, rename, Git, Unity/Runtime, external-process, network, release and credential actions require action-specific user wording; managed-channel plans and contracts are transport requirements only.
- Keep the low-risk path distinct from missing capabilities; neither permits silently widening the requested scope.

Use this skill for every task that modifies project text, especially files containing Chinese.

## Workflow

1. Read the P0 UTF-8 warning under `AIWarnings/10_P0最高约束（P0Guardrails）/编码与文本（Encoding）`.
2. Prefer `apply_patch` for targeted edits. Never read with a default code page and overwrite the original.
3. Run `scripts/Test-ESUtf8.ps1 -ProjectRoot <root>` after editing. Pass `-Path` to limit validation to known files when appropriate.
4. Review every suspicious mojibake hit manually. Do not bulk transcode or delete text based only on a marker.
5. Inspect the target diff for unexpected whole-file rewrites or line-ending drift.

## Exit codes

- `0`: strict decoding, replacement-character scan, suspicious-marker scan, and `git diff --check` passed.
- `1`: invalid UTF-8, U+FFFD, missing target, or diff-check error.
- `2`: suspicious mojibake requires manual review.

## Rules

- Never use `Get-Content file | Set-Content file` on project text.
- Never use `-Encoding Default`, ANSI, or mechanical GBK/UTF-8 conversion.
- Preserve BOM and line endings unless the task explicitly changes them.
- Repair only text whose intended content can be proven from source, history, or adjacent semantics.

## SmallTool controls

- **Scope**: read only the project root or explicit `-Path` targets; never traverse credentials, external caches or unrelated repositories.
- **Side effects**: validation is read-only. A detected encoding issue does not authorize conversion, deletion or whole-file rewrite.
- **Bounded scale**: prefer changed/target files; declare a file-count or root boundary for repository-wide scans. Stop on invalid UTF-8, unsafe target resolution or undecidable mojibake.
- **Repeatability**: identical bytes produce the same classification. File changes invalidate earlier results; concurrent writes require a fresh scan.
- **Required cases**: valid UTF-8, invalid byte sequence, U+FFFD/suspicious marker, denied conversion request and repeated unchanged scan.


## Specialized static acceptance

Acceptance ID: `utf8-integrity`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- UTF-8
- strict
- BOM
- invalid byte
- roundtrip

Required specialized cases: `strict-decode, bom-policy, invalid-byte, roundtrip-hash, powershell-write-safety`
Guidance: `references/static-specialized-acceptance.md`
