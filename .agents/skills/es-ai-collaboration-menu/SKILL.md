---
name: es-ai-collaboration-menu
description: >-
  Show a bounded, context-aware ES collaboration menu when the user asks for “菜单”,
  “协作菜单”, or a guided ES creation/iteration menu. Use it to select a domain
  workflow, validation path, knowledge route, or session-support option; it only
  presents choices and never executes a project action.
---

# ES AI Collaboration Menu

Use this Skill as the independent guidance surface for the short request “菜单”.
It interprets natural language first, treats structured context as optional hints,
and turns the request into a small, stable menu for creating content, iterating an
existing feature, governing the framework, validating evidence, finding
Knowledge/Skills, or managing collaboration.

## Trigger and discovery

- The exact Chinese trigger `菜单` is the primary route. `协作菜单`, `ES菜单`,
  `给我菜单`, and `协作引导菜单` are supported aliases.
- A menu request must route here before a domain Skill is selected.
- If the user names a concrete domain as well, keep the menu as the chooser and
  mark the matching option as recommended; do not silently execute that domain.
- Numeric replies are selections only. Resolve them through the current menu output;
  never infer a missing number or dispatch an action in this Skill.

## Inputs

The deterministic renderer accepts natural-language PromptText and optional JSON
context signals:

```json
{
  "taskKind": "create|iterate|govern|validate|discover|collaborate|unknown",
  "projectArea": "gamecore|resource|entity|input|editor|ui|shader|graph|session|unknown",
  "routeStatus": "resolved|ambiguous|missing|unknown",
  "contextFreshness": "fresh|stale|unknown",
  "riskLevel": "low|high|unknown"
}
```

Malformed signals are rejected. Missing signals become `unknown`; they never grant
write, Runtime, network, Git, release, or credential authority.
Users do not need to provide these signals. The renderer extracts bounded intent
evidence from natural language, reports confidence and candidates, and falls back to
context discovery when the wording is ambiguous.

## Menu behavior

The menu always contains the complete bounded option set from
`references/menu-options.json`, in stable order, with at most one recommended option.
The current context can change labels/reasons and ordering only through the declared
deterministic rules. Every item includes a stable `id`, number, label, reason, risk,
required Skill/Knowledge route, and `requiresUserChoice=true`.

The six options are:

1. `create-content` — create ES content or an asset-facing feature;
2. `iterate-feature` — diagnose and improve an existing implementation;
3. `govern-framework` — review architecture, contracts, lifecycle, or boundaries;
4. `validate-evidence` — compile, static, runtime, acceptance, or release evidence;
5. `discover-context` — select bounded Skills, Knowledge, and AIWarnings routes;
6. `coordinate-session` — session, window handoff, mailbox, or collaboration routing.

Every visible sequence number uses the full-width bracket form `【n】`. This applies
to both the main menu and the coordination submenu; numeric replies remain choices,
never direct execution commands.

The coordination submenu must keep these capabilities separate: `Fork` copies a
confirmed session context; `Handoff` transfers a bounded task to a new window through
`Complete-ESCodexHandoff.ps1`. Selecting either only routes to the session Skill; this
Skill cannot execute either operation. The submenu is defined in
`references/session-submenu.json` and must keep `window-handoff` visibly distinct from
`session-fork`.

`discover-context` is recommended for ambiguous/missing routes or stale/unknown
context. High-risk create/iterate/govern tasks recommend `validate-evidence` or
`discover-context` according to the declared signal priority. A fresh, resolved,
low-risk task may show no recommendation. The renderer never reads the whole
repository and never invokes the selected Skill. Every main option exposes a bounded
second-level submenu; submenu choices remain route descriptors and require a user
choice.

## Boundaries and recovery

- This Skill is presentation and routing only; it does not write files, run Unity,
  start processes, send messages, use network, change Git, or publish.
- A selected item is a bounded next step, not permission. The caller must route to
  the selected Skill and obtain any action-specific user authorization.
- If context is ambiguous, show the menu and include `discover-context`; do not guess
  a domain. If the menu input is invalid, fail closed with the schema error.
- Repeating the same prompt and signals returns byte-stable JSON. An interrupted
  render can be rerun without state or cleanup.

## SmallTool controls

- Fast path only: read the bundled menu contract and option table; do not scan the
  repository or invoke external tools.
- The only output is a deterministic read-only menu. No child Skill, AICommand,
  Runtime, process, network, Git, or release action is started.
- Capability policy is explicit: this Skill may present, recommend, and route; it may
  not dispatch session operations. Submenu metadata is descriptive constraints, not
  permission.
- Each invocation includes a non-persistent decision receipt with prompt, intent-rule
  and menu-schema hashes, inferred intent, confidence, negated intents, compound
  stages and `runtime-not-run`; it is not project truth.
- Invalid input fails closed. Repeated input is idempotent and interruption requires
  no cleanup because the renderer has no persistent state.

## Static evidence

Run `scripts/Test-ESCollaborationMenu.ps1` for positive, invalid-input,
denied-expansion, repeat/idempotency, and deterministic-output cases. Run
`scripts/Test-es-ai-collaboration-menu-StaticReplay.ps1` for the required
StaticDeepReplay artifact. These checks prove routing and presentation only; they do
not prove user choice quality or Unity/editor/runtime behavior.

## Skill 使用披露

This Skill follows the project disclosure rules in `AGENTS.md` and `.agents/README.md`.
An AI using it must list `es-ai-collaboration-menu` in its first progress update and
final closeout. Disclosure is not authorization or acceptance evidence.
