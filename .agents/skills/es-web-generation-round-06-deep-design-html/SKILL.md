---
name: es-web-generation-round-06-deep-design-html
description: Consume an accepted Round 05 design-system profile and a real user requirement to create a requirement-specific deep WebPageStudio design, then materialize high-detail semantic HTML/CSS/JS with traceable interactions, responsive states, motion usage, accessibility, and deterministic static evidence.
---

# ES Web Generation Round 06 — Deep Design & HTML

## Purpose

Round 06 is the first page-specific implementation stage. It consumes the accepted TaskContext, KnowledgeRoute, and Round 05 DesignSystemProfile, then designs and materializes the requested page. It must preserve the user's objective, use templates as primitives rather than as a substitute for analysis, and produce a detailed static artifact.

## SmallTool controls

- Read only accepted upstream receipts, the current user requirement, and the referenced template/motion/style resources.
- Require AI analysis before writing: objective, audience, primary action, content model, information architecture, state graph, responsive matrix, motion mapping, a11y and acceptance assertions.
- Write only the declared WebPageStudio artifact path and deterministic receipt; never overwrite outside the approved output or claim runtime/browser proof.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, Round 03 TaskContext, Round 04 KnowledgeRoute, accepted Round 05 DesignSystemProfile, [`references/round-06-deep-design-html-contract.json`](references/round-06-deep-design-html-contract.json), and the existing WebPageStudio static-generation contracts. Read the selected template, motion/effect and style profile entries before materialization.

## Workflow

1. Verify every upstream receipt is accepted, hash-bound and current. Any stale or missing receipt blocks this round.
2. Interpret the actual user requirement; write an objective brief and do not infer a GitHub/dashboard page from a generic title.
3. Instantiate Round 05 primitives into concrete page regions, content examples, component inventory, interaction state graph, responsive matrix, motion timeline, a11y checks and traceability links.
4. Run a design review gate. The design packet must be explicitly accepted before HTML materialization; otherwise return `design-review` with no page write.
5. Materialize semantic HTML/CSS/JS with real content, loading/empty/error/success states, keyboard paths, reduced-motion fallback, responsive behavior and the selected high-density effects.
6. Run 5+ bounded self-review rounds. Every round must make an auditable artifact/design increment, pass the interaction gate (search, detail, comments, status/conflict and keyboard path) and pass the layout gate (grid/flex structure, responsive breakpoints, minmax constraints and focus geometry). A round that only waits or recomputes a hash without an increment fails.
7. Run static checks for required DOM markers, contract-to-DOM traceability, UTF-8, deterministic artifact hash, and no placeholder/template-only output. Emit a receipt separating `static-generated`, `static-validated`, and `release-not-run`.
8. Stop. Browser, Unity, network, visual, performance and release claims require separate authorization and evidence.

## Maximum bounded resource profile

Use the legal project maximums for the design/ABCD portion: `maxRounds=24`, `maxBranches=256`, `maxModelCalls=128`, `maxEvaluations=512`. Allocate explicit stage usage to objective analysis, information architecture, component detailing, interaction/state coverage, responsive design, motion choreography, HTML materialization and static verification. Every call/evaluation is charged to both its stage and the global budget; exceeding either stops the run with a receipt. “Budget max” never authorizes Runtime, browser, network, Unity, Git or release actions.

## Hard controls

- No HTML write before design acceptance.
- A template cannot supply the objective, content, state logic or visual decision by itself.
- Every primary capability maps to a region, component, interaction, effect and assertion.
- Every generation round must demonstrably advance detail and must re-check interaction behavior and layout geometry; interaction/layout are mandatory gates, not optional post-checks.
- Preserve all required states and accessibility semantics; dense effects must degrade under reduced-motion or constrained profiles.

## Engineering controls

- Keep design packet and HTML artifact hashes separate and immutable.
- Repeat generation with unchanged inputs is deterministic; changed upstream hashes invalidate the output.
- Materialization is project-local and bounded; no network, Unity, Git, release, or resident process is started.
- Static evidence does not prove browser rendering, timing, visual quality or Runtime behavior.

## Return contract

Return `recordType=DeepDesignHtmlReceipt`, `roundId`, `stageId`, `status`, `taskId`, upstream hashes, `designPacket`, `artifactPath`, `artifactHash`, `domTraceability`, `staticChecks`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
