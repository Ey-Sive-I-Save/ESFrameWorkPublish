---
name: es-release-acceptance
description: Plan, execute, and report ESFramework acceptance across source checks, generated .csproj builds, Unity Editor compilation, ReloadDomain, EditMode and PlayMode tests, runtime observation, Profiler, Player and IL2CPP builds, resource providers, manifests, downloading, and real release workflows. Use when deciding whether an ES change is actually ready to publish or when assembling a release evidence matrix.
---

# Run ES Release Acceptance

Build an evidence matrix from the actual risk surface. Never upgrade a lower evidence layer into a higher release claim.

## Workflow

1. Read the AIWarnings start files and `references/evidence-matrix.md`.
2. Inspect the changed paths and classify affected assemblies, runtime systems, editor tools, resources, providers, platforms, and release artifacts.
3. Define the required evidence rows before running checks. Mark unavailable rows as blocked or not run, never implicit pass.
4. Capture the existing Unity Console and editor state before clearing or changing anything.
5. Run static and generated-project checks, then Unity import and domain reload, then focused EditMode/PlayMode tests.
6. Run runtime observation, Profiler, Player/IL2CPP, resource plan, provider, download, and release checks only where the change requires them.
7. Archive exact commands, Unity jobs, test names, target platform, logs, outputs, hashes, and timestamps needed to reproduce the conclusion.
8. Separate task-related failures from unrelated existing failures. Stop release approval when a required row fails or lacks evidence.
9. Run `$es-utf8-guard` and update the local documentation ledger without advancing HTML before `ready-for-html`.

## Decision rule

Approve only the explicitly requested scope whose required evidence rows passed. Use `conditional` when accepted gaps are documented. Use `blocked` when required Unity, platform, provider, or publishing evidence could not run.

## Required boundaries

- Source presence is not compilation.
- `.csproj` build is not Unity Editor compilation.
- Unity Console success is not Test Runner or PlayMode success.
- PlayMode observation is not Profiler, Player, IL2CPP, provider, or release success.
- A generated manifest is not a successfully downloaded, verified, loaded, and rolled-back release.
- An external AI report is not local evidence unless independently reproduced.

## Delivery

Return the evidence matrix, decision, exact passed scope, failures, blocked rows, artifacts, and the smallest next action required for approval.
