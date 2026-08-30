---
name: es-web-generation-round-05-capability-design
description: "Convert an accepted KnowledgeRoute into a reusable WebPageStudio design-system package: interaction, card and page templates, motion/effect selections, usage policy, visual style tier, and innovation limit for later requirement-specific deep design."
---

# ES Web Generation Round 05 — Design System & Effect Profile

## Purpose

Round 05 defines reusable primitives before a specific page is designed: interaction templates, card templates, page templates, advanced motion/effect selections, default effect density, visual style tier, and innovation limit. It may use bounded ABCD exploration to rank primitives, but final requirement-specific deep design moves to Round 06. It must not emit HTML/CSS or call Runtime/network/Unity.

## SmallTool controls

- Read only the accepted Round 04 receipt, selected Knowledge entries, ABCC/ABCD contracts, and the design contract.
- Require AI analysis of reusable web patterns, at least three independent template systems, explicit selection reasons, and rejected alternatives.
- Start a bounded subagent plan (interaction, component, motion, visual/a11y roles) only after the route is accepted; merge through the InnovationRun, never by untracked prose.
- Write only the design packet/receipt path. Do not silently mark `designStatus=accepted`; acceptance requires a user decision recorded in the receipt.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, the Round 04 KnowledgeRoute receipt, `es-ai-abc-core`, `es-aibrain-route-authoring`, and [`references/round-05-capability-design-contract.json`](references/round-05-capability-design-contract.json). Read `ES/Automation/ABCD/ESABCInnovationRun.psm1`, `ES/Automation/TaskCollaboration/ESTaskCollaborationContracts.psm1`, their READMEs, and the ABCC interface/core and InnovationRun contracts before authoring candidates. Load [`references/round-05-template-catalog.json`](references/round-05-template-catalog.json), [`references/round-05-motion-effect-catalog.json`](references/round-05-motion-effect-catalog.json), and [`references/round-05-style-innovation-profile.json`](references/round-05-style-innovation-profile.json) before selecting primitives.

## Workflow

1. Verify Round 04 is accepted and selected entries are current; stale Knowledge blocks this stage.
2. Analyze reusable patterns, component families, motion affordances, style constraints, and evidence gaps. Do not lock a page-specific layout.
3. Generate at least three independent template-system axes, score them against reuse value, interaction clarity, visual coherence, novelty, and risk, and record discarded reasons.
4. Open the InnovationRun with enlarged but bounded resources (12 model rounds; stage budgets for tree expansion, convergence, replay, counterplay, and tournament). Recompute branch weights before seed expansion and every model round.
5. Dispatch the subagent plan with explicit inputs/outputs and no shared mutable page files; record each branch parent, interaction delta, evidence and discard reason in the run.
6. Synthesize a reusable package containing template contracts, component slots, motion/effect catalog references, effect-density policy, style profile, innovation limit, and static acceptance assertions. Do not fill page-specific regions or content.
7. Validate the package and hash-bind it to TaskContext and KnowledgeRoute. Return `candidate` until the user explicitly accepts; Round 06 consumes it for requirement-specific deep design and HTML materialization.
8. Stop. No automatic generation or runtime execution is permitted.

## Hard controls

- ABCC capability mapping is independent from ABCD dynamic innovation; do not flatten either into generic prose.
- Creative divergence must remain visible; the host AI cannot silently choose the seed concept.
- `designStatus=accepted` is a user decision, never a script default.
- Static design evidence proves structure and traceability only, not visual quality or runtime behavior.
- A budget number is not evidence: actual stage usage, branch fan-out and subagent return receipts must be recorded.

## Engineering controls

- Use a task-scoped InnovationRun or an equivalent contract-bound candidate trace; every branch carries parent, change, interaction delta, keep/discard, and reason.
- Preserve Knowledge SourceRefs and route hashes; any drift invalidates the packet.
- Keep design and materialization separate; HTML generation begins only from an accepted packet.
- Repeat validation is deterministic and does not overwrite prior packets.

## Return contract

Return `recordType=DesignSystemProfileReceipt`, `roundId`, `stageId`, `status`, `taskId`, `taskRevision`, `knowledgeRouteHash`, `resourceBudget`, `resourceUsage`, `subagentPlan`, `subagentReceipts`, `innovationRun`, `templateLibrary`, `motionEffectLibrary`, `effectUsagePolicy`, `styleProfile`, `innovationProfile`, `acceptanceAssertions`, and `nonClaims`.

## Expected use

Round 05 prevents the previous failure mode where a fixed GitHub dashboard/template is emitted regardless of the request. It forces AI-led interpretation and deep design before any concrete page is generated.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
