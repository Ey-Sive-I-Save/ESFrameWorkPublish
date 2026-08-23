# UTF-8 strict decoding and encoding guard

## Purpose

This is a responsibility-specific static acceptance plan for **es-utf8-guard**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `engineering`
- Acceptance id: `utf8-integrity`
- Runtime boundary: Static byte-level checks are sufficient; no Runtime claim is made.
- Static assertions: UTF-8; strict; BOM; invalid byte; roundtrip

## Required specialized cases

- `strict-decode`: replay the strict-decode contract from source/configuration and record pass or blocked evidence.
- `bom-policy`: replay the bom-policy contract from source/configuration and record pass or blocked evidence.
- `invalid-byte`: replay the invalid-byte contract from source/configuration and record pass or blocked evidence.
- `roundtrip-hash`: replay the roundtrip-hash contract from source/configuration and record pass or blocked evidence.
- `powershell-write-safety`: replay the powershell-write-safety contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/Test-ESUtf8.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`
- `static-replay.manifest.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
