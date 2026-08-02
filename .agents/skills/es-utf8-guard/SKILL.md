---
name: es-utf8-guard
description: Validate ESFramework text changes for strict UTF-8 decoding, Unicode replacement characters, likely mojibake, unintended broad rewrites, and git diff integrity. Use before or after editing Chinese source, Markdown, JSON, YAML, CSV, shaders, or scripts in the ES project.
---

# Guard ES UTF-8 Text

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
