# AI Web Generation Step Contract

Every WebPageStudio run is a sequence of explicit `AI analysis -> execution -> return` steps. A step is not accepted because a prompt was written; its return receipt must name inputs, output hashes, decision, non-claims and recovery. The detailed contract for each step is split into the independently readable references below.

1. [Intent analysis](step-01-intent-analysis.md)
2. [Knowledge analysis](step-02-knowledge-analysis.md)
3. [Source evidence](step-03-source-evidence-analysis.md)
4. [Capability compilation](step-04-capability-compilation.md)
5. [Prompt planning](step-05-prompt-planning.md)
6. [Deep design](step-06-deep-design.md)
7. [HTML materialization](step-07-html-materialization.md)
8. [Quality and closeout](step-08-quality-closeout.md)

| Step | AI analysis | Execution | Required return |
|---|---|---|---|
| 1 Intent | identify objective, audience, page kind, action, states and constraints | `Invoke-ESWebPageStudioPreflight.ps1` normalizes the request | `intent-review` with requestId and decision |
| 2 Knowledge | select only matched AIBrain/Knowledge and source references; mark stale hashes | read manifest-bound entries | five Knowledge read receipts or a bounded `NoKnowledgeRoute` |
| 3 Source evidence | compare six pinned repositories, license and source SHA-256 | consume `open-source-source-manifest.json`; never use mutable sourceAbsolutePath | six source evidence records |
| 4 Capability compile | map observed mechanisms to render, boundaries, data, state, enhancement, resumability and budgets | `Invoke-ESWebOpenSourceCapabilityCompiler.ps1` | `compiledCapabilities.status=accepted` and strategy hashes |
| 5 Prompt | turn compiled strategies into generation directives | preflight prompt planner | promptPlan and generatedPrompt |
| 6 Deep design | choose regions, components, states, responsive rules and HTML directives | `Invoke-ESWebPageStudioDeepDesign.ps1` | accepted design spec with executionPlan |
| 7 Materialize | ensure every capability has a concrete DOM marker/attribute and deterministic content | static generator | HTML plus design and capability receipts |
| 8 Quality | check intent coverage, artifact integrity, UTF-8 and deterministic replay | intent/artifact/static validators | pass counts and receipt hashes |

## Decision rules

- Missing source file, license, hash mismatch or path escape is `blocked.source-evidence`; do not silently fall back to mutable repository paths.
- A compiler may adapt patterns but must not copy a framework runtime into the project. The external snapshot directory is provenance only.
- `accepted` means static strategy compilation. It does not prove framework runtime, browser, network, Unity, release or production behavior.
- Each stage returns a machine-readable receipt even when blocked. Recovery must identify the exact stage and rerun inputs.
