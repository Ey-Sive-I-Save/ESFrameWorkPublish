---
name: es-web-generation-round-06-deep-design-html
description: Orchestrate an AI agent that consumes an accepted Round 05 capability profile plus a real AI-instantiated page solution (Round 05.5) and user requirement, produces competing page designs, writes real HTML/CSS/JS or React, and iterates from executable evidence; scripts only execute checks and validate receipts.
---

# ES Web Generation Round 06 — Deep Design & HTML

## Purpose

Round 06 is the AI Design-and-Code Orchestrator. It consumes the accepted TaskContext, KnowledgeRoute, Round 05 DesignSystemProfile, and the AI-owned Round 05.5 page solution, dispatches AI design branches, and lets the AI agent write and repair the page. Templates and scripts are evidence/primitive tools only; they cannot invent page structure, copy, or claim an AI turn.

## SmallTool controls

- Read only accepted upstream receipts, the current user requirement, and the referenced motion/style resources. Templates are optional primitives and never a page solution.
- Require AI analysis before writing: objective, audience, primary action, content model, information architecture, state graph, responsive matrix, motion mapping, a11y and acceptance assertions.
- Write only the declared WebPageStudio artifact path and deterministic receipt; never overwrite outside the approved output or claim runtime/browser proof.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, Round 03 TaskContext, Round 04 KnowledgeRoute, accepted Round 05 DesignSystemProfile, [`references/round-06-deep-design-html-contract.json`](references/round-06-deep-design-html-contract.json), and the existing WebPageStudio static-generation contracts. A real `AiWebDesignModelResponse` for `page-design-instantiation` is required before producing a page-level design; without it the adapter writes `review` and blocks. Read any selected motion/effect and style profile entries only as evidence for the AI decision.

## Workflow

1. Verify every upstream receipt is accepted, hash-bound and current. Any stale or missing receipt blocks this round.
2. Interpret the actual user requirement; write an objective brief and do not infer a GitHub/dashboard page from a generic title.
3. Instantiate Round 05 primitives into concrete page regions, content examples, component inventory, interaction state graph, responsive matrix, motion timeline, a11y checks and traceability links.
4. Run a design review gate. The design packet must be explicitly accepted before HTML materialization; otherwise return `design-review` with no page write.
5. Admit a real `AiWebDesignTask` and one or more `AiWebDesignRevisionReceipt` records using `Test-ESRound06AiDesignAgentReceipt.ps1`. Each receipt must prove reads, writes, changed files/regions, concrete design decisions, hashes, and requested checks.
6. Feed real tool failures back to the AI agent. A revision without a failure report, concrete decision, and changed-file manifest is not a revision.
5. Let the AI/ABCD-selected candidate own the page design. Materialize only a complete AI-authored HTML/CSS/JS candidate; do not synthesize a fallback layout from fixed strings. The candidate must cover real content, loading/empty/error/success states, keyboard paths, reduced-motion fallback, responsive behavior and selected effects.
6. Run the five-round orchestration defined in [`references/round-06-five-round-orchestration.json`](references/round-06-five-round-orchestration.json): Round 1 uses an isolated subagent with the complete accepted design packet and at least five minutes of active generation, aiming to finish the whole page. Rounds 2–5 are high-authority handoff repair rounds: each may radically restructure HTML/CSS/JS to complete anything Round 1 missed, while using the DesignPacket as the quality oracle; their named focus is a gate, not a limit on repair scope. Each round must consume a distinct AI-authored revision artifact, record the before/after hash and a semantic DOM/interaction diff, then pass the interaction and layout gates. Waiting, hash-only mutation, metadata-only mutation, or non-AI content is an immediate failure.
7. Run static checks for required DOM markers, contract-to-DOM traceability, UTF-8, deterministic artifact hash, and AI-authored candidate provenance. Emit a receipt separating `static-generated`, `static-validated`, and `release-not-run`.
8. Stop. Browser, Unity, network, visual, performance and release claims require separate authorization and evidence.

## Maximum bounded resource profile

Use the legal project maximums for the design/ABCD portion: `maxRounds=24`, `maxBranches=256`, `maxModelCalls=128`, `maxEvaluations=512`. Allocate explicit stage usage to objective analysis, information architecture, component detailing, interaction/state coverage, responsive design, motion choreography, HTML materialization and static verification. Every call/evaluation is charged to both its stage and the global budget; exceeding either stops the run with a receipt. “Budget max” never authorizes Runtime, browser, network, Unity, Git or release actions.

## Hard controls

- No HTML write before design acceptance.
- A template cannot supply the objective, content, state logic or visual decision by itself; templates are optional primitives only.
- `Invoke-ESWebPageStudioDeepDesign.ps1` must not synthesize capabilities, regions, data contracts, or visual tokens from the objective. It only maps fields supplied by the AI response and records the response hash.
- The orchestrator must fail with `blocked.round-06.ai-agent-contract-required` or `blocked.round-06.ai-agent-not-admitted` when the task/receipts are absent or invalid. It must never claim AI analysis from counters, timers, hashes, templates, or generated prose.
- A receipt with only `modelCalls`, byte changes, or a hash change is invalid. Writes must remain inside `allowedWriteRoots`, and `sourceHashAfter` must match the real artifact.
- Every primary capability maps to a region, component, interaction, effect and assertion.
- Every generation round must demonstrably advance detail and must re-check interaction behavior and layout geometry; interaction/layout are mandatory gates, not optional post-checks.
- Preserve all required states and accessibility semantics; dense effects must degrade under reduced-motion or constrained profiles.

## Engineering controls

- Keep design packet and HTML artifact hashes separate and immutable.
- Repeat generation with unchanged inputs is deterministic; changed upstream hashes invalidate the output.
- Materialization is project-local and bounded; no network, Unity, Git, release, or resident process is started.
- Static evidence does not prove browser rendering, timing, visual quality or Runtime behavior.

## Return contract

Return `recordType=DeepDesignHtmlReceipt`, `roundId`, `stageId`, `status`, `taskId`, upstream hashes, `designPacket`, `artifactPath`, `artifactHash`, `domTraceability`, `staticChecks`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`. The receipt must also bind `aiMaterialization.agentTaskPath` and the admitted `revisionReceiptPaths`; without those bindings the result is not a real AI coding round.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
