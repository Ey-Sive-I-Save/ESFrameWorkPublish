# ES AI ABC 适配核心（ABCC）

`KnowledgeId`: `es.architecture.ai-abc-core.v1`
`Authority`: `ABCC contract files and current ES governance; research Skill is provenance only`
`RouteKeys`: `ai-abc`, `abc-core`, `semantic-adapter`, `analysis`, `design`, `evidence`, `closed-loop`, `route-stage`, `static-replay`, `knowledge`
`HashSchema`: `v2`
`ContentHash`: `5d769f2c1ce483035b677fddaf0aec48b47afdc6fc48f3c3c129b1d5cf42d7dc`
`SourceSetHash`: `5d769f2c1ce483035b677fddaf0aec48b47afdc6fc48f3c3c129b1d5cf42d7dc`
`EntryBodyHash`: `65f306fee9f011ddd42a08f20ac96a77c7b4169240ba45e45376fd7ce42cdc8c`
`EvidenceLevel`: `S1`
`StaleWhen`: `ABCC interface/core contract, mode registry, RouteStage registry, AIBrain route, Skill, aliases or any SourceRef hash changes.`

## Scope

ABCC (Core) is an independent semantic adapter. ABCD (Dynamic) remains the
original independent generalist profile. ABCP (Part) references this Core by
stable IDs and does not copy its prose.

## Formal naming

The `namingAuthority` object in `ES/Automation/Contracts/es-ai-abc-mode.registry.json`
is the canonical name source for all three modes. `ABC` means
`Agent–Behavior–Collaborator`: A is the intent/process side, B is the
independent mechanism/capability side (not BehaviorTree-specific), and C is the
human or AI collaborator. `Dynamic`, `Core` and `Part` are mode suffixes, not
fourth roles. The stable machine IDs remain `ABCD.Dynamic`, `ABCC.Core` and
`ABCP.Part`; localized names are display aliases only.

## Canonical loop

`C goal/authorization -> A intent -> ABCC negotiation -> B capability offer ->
normalized result/evidence -> audit/completion -> C acceptance`.

ABCC provides the six ABCD kernel capability interfaces: bounded action,
failure recovery, branch evaluation, state-transition guard, environment trust
gate and audit-evidence chain. Missing capability blocks; semantic mismatch
replans; missing evidence caps the claim; unauthorized effects block.

## Authority and non-claims

The interface schema and Core instance are the authority. The old
`es-agent-mechanism-replication` Skill and its Knowledge are research
provenance only and are not a runtime dependency. This entry supports static
discovery and contract design; it does not prove Unity, Runtime, Player,
Profiler, IL2CPP, network or release behavior.

## SourceRefs

- `ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json` (`7c8fe8695dc4ed52485662f7861833bb817ea158a1c1658c315f602a7ede6bc3`)
- `ES/Automation/Contracts/es-ai-abc-core-v1.json` (`20a10dc81762e61c4dc946bc6e6ea11fc830bc5f5e11cfe65def43997f613dbc`)
- `ES/Automation/Contracts/es-ai-abc-mode.registry.json` (`5950220db01715980e2456fdea26a80f8f816c5e61cb47f99c03739a8510e95e`)
- `.agents/skills/es-ai-abc-core/SKILL.md` (`b2dcf4c76cfb8e5abaf1832b4d46cccb99a9d54e931224a09de3abad8e2a4e29`)
- `.agents/skills/es-ai-abc-core/governance.json` (`a9eae44527cfd418f812b354d4180fa2ec85c09075a4e279ad317da3a8b7ba92`)
- `ES/Automation/Contracts/es-route-stage.registry.json` (`4f67cd468ef4d64c04eb219da7fcb1cbdab10a62bf0590328409470b8a0fb82d`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`8e3f621daa078c047311f28dede7e839aae4fd34d3062a259561604fdbd2f2f4`)
- `Documentation/AIKnowledge/ExternalSources/ai-abc-open-source-provenance.v1.json` (`e70f194b127de4e57a9d01049291f5aa812373e56c203258811e6e5c72512074`)
