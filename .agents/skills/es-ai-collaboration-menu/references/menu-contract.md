# ES AI Collaboration Menu Contract

## Authority

The menu is a derived navigation surface. Current source, AIWarnings, schemas,
tests, and real receipts remain authoritative. Menu labels and route suggestions do
not grant permission and do not replace the selected Skill's contract.

## Input contract

`PromptText` is required. `ContextJson` is optional JSON and may contain only
`taskKind`, `projectArea`, `routeStatus`, `contextFreshness`, and `riskLevel`.
Unknown enum values are rejected; missing fields normalize to `unknown`.
The renderer also interprets natural-language PromptText. Structured signals are
optional hints and do not need to be supplied by the user. High-confidence intent
language wins when no explicit safety signal contradicts it; low-confidence or
ambiguous language falls back to `discover-context` rather than guessing execution.

## Deterministic recommendation rules

1. `routeStatus=ambiguous|missing` or `contextFreshness=stale|unknown` recommends
   `discover-context`.
2. `taskKind=validate` recommends `validate-evidence`.
3. `taskKind=create` recommends `create-content`.
4. `taskKind=iterate` recommends `iterate-feature`.
5. `taskKind=govern` or `riskLevel=high` recommends `govern-framework`, unless rule 1
   already selected `discover-context`.
6. `taskKind=collaborate` recommends `coordinate-session`.
7. No recommendation is emitted when the supplied context is explicitly
   `routeStatus=resolved`, `contextFreshness=fresh`, `taskKind=unknown`, and
   `riskLevel=low`. With no context supplied, `unknown` freshness intentionally
   recommends `discover-context` so the short `菜单` trigger remains useful and
   does not pretend that project facts are fresh.

Only one option can be recommended. All seven options remain visible. The
`coordinate-session` option must expose its explicit session submenu, including
separate `session-fork` and `window-handoff` entries. Fork copies context; it never
means handoff. Window handoff is a route to `es-codex-session-bootstrap` and must use
`Complete-ESCodexHandoff.ps1`, private per-launch snapshots, and a new acceptance
envelope; this menu Skill only presents that route.

## Output and safety

Output contains `menuId`, normalized signals, a capability policy, stable options,
the complete session submenu, one optional `recommendedOptionId`,
`decisionSource=derived`, `requiresUserChoice=true`, and `nonClaims`. No output
action is executed. The renderer must not read or write project files beyond its
bundled menu data.

Output also contains `routeDirectory.categories`. Each category and item has a
stable number and route key. `-Selection Rcategory.item` resolves a directory item;
`-Selection n` resolves a main option and `-Selection n.m` resolves its submenu.
All three forms are route descriptors only and retain `requiresUserChoice=true`.

All main options expose bounded submenus. Intent output reports primary intent,
confidence, candidates, negated intents, compound stages, inferred project area,
evidence terms and a non-persistent decision receipt. A negated action must not be
recommended. Multiple positive stages are ordered by language position and only the
first stage is recommended until the user chooses to continue.

## Required static cases

Positive create, ambiguous-route context discovery, invalid JSON/enum, denied
expansion (a prompt cannot authorize writes or Runtime), repeat idempotency, and
deterministic output for identical input, natural-language handoff intent,
natural-language iteration intent, and bracketed numbering.
