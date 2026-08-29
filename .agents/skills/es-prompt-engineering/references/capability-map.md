# Capability map

This Skill independently implements ES-specific contracts. External projects are design references only (`external-source-not-bound`); no source code, prompt corpus, dependency, service, or trademark is bundled.

| Reference | Mechanism adapted | ES boundary |
|---|---|---|
| PromptSource | versioned templates and variables | JSON envelope; no Jinja execution |
| promptfoo | assertions, fixtures, regression and red-team cases | deterministic local assertions; no provider or custom-code execution |
| Guidance | constrained output shape | JSON Schema and enumerations; no model grammar runtime |
| DSPy | compare candidates against metrics | candidate comparison is optional and evidence-bound; no optimizer |
| TypeChat | schema-first intent translation and validation | validate derived envelope; repair requires a new candidate |
| Guardrails AI | input/output validation and explicit on-fail state | `expanded`, `review`, or `blocked`; no validator hub |
| NeMo Guardrails | input/dialog/retrieval/execution/output rail separation | authority, read, action, and output boundaries remain distinct |

Official project pages and licenses must be rechecked before any future code import. This file records mechanisms, not license approval for copying implementation.

