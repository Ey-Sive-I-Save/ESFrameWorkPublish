---
name: es-skill-quality-loop
description: Validate and iterate ESFramework project Skills through structure checks, trigger and non-trigger examples, representative task forward-tests, permission-boundary review, and evidence reporting. Use when creating or revising a Skill, diagnosing inaccurate Skill routing, or preparing a Skill for Unity Diff Review; do not use to authorize formal import or claim Unity/runtime acceptance.
---

# Validate and iterate a Skill

1. Read the target Skill, its one-level references, and `es-generate-agent-artifacts` constraints.
2. Check frontmatter, `agents/openai.yaml`, direct project references, UTF-8, U+FFFD, and scoped diff integrity.
3. Build a small matrix of trigger, non-trigger, permission, failure, and evidence cases.
4. Run a representative task in a clean context when the required tool is available. Do not leak the intended answer into the test prompt.
5. Record failures and the smallest correction; never widen permissions to make a test pass.
6. Re-run structural checks and the representative task after each material revision.
7. Report `Passed`, `Failed`, or `NotRun` separately for each gate. Unity Diff Review and human approval remain required for formal import.

Non-goals: direct writes to `.agents/skills`, AIWarnings, AICommands, Assets, Git, or external services.

Read `references/quality-matrix.md` for the minimum test matrix.
