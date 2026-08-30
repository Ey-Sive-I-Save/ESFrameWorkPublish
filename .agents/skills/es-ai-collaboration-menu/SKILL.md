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
- The output also exposes `routeDirectory`, a stable categorized directory for
  超级语义、路由、能力与边界. Users may select a visible main/submenu number, or
  an `Rcategory.item` directory number (for example `R2.4`); selection only returns
  a route descriptor and never executes it.

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

The seven options are:

1. `create-content` — create ES content or an asset-facing feature;
2. `iterate-feature` — diagnose and improve an existing implementation;
3. `govern-framework` — review architecture, contracts, lifecycle, or boundaries;
4. `validate-evidence` — compile, static, runtime, acceptance, or release evidence;
5. `discover-context` — select bounded Skills, Knowledge, and AIWarnings routes;
6. `coordinate-session` — session, window handoff, mailbox, or collaboration routing;
7. `ai-mechanism-atlas` — super-semantics, available capabilities, permission boundaries,
   evidence levels, and public Agent mechanism adapters.

The `ai-mechanism-atlas` submenu is a read-only guide. It may explain trigger phrases,
route to a Skill, or describe evidence and permission boundaries, but it never executes
the described capability. Its entries are defined in `references/menu-submenus.json`.

When the user selects `create-content` → `create-resource`, expose the dedicated
`resource-collection` submenu before routing. It is the authoritative user-facing
feature set for `es-resource-collection`: search/candidate queue, download/extract
verification, read-only preview, site/type layout, deduplication and fuzzy comparison,
child-agent coordination, and site registration/removal. These entries are choices
only; they do not grant network, file-write, process, or deletion permission.

Every visible sequence number uses the full-width bracket form `【n】`. This applies
to both the main menu and the coordination submenu; numeric replies remain choices,
never direct execution commands.

The categorized route directory is always emitted, including when the user only
says `菜单`; this makes the AI menu discoverable instead of requiring the user to
know the hidden atlas submenu first.

The menu also emits a bounded `contextSummary` and three `quickAccess` choices:
`A` uses the case-insensitive `AG` child-agent shortcut, `B` continues with the
current context, and `C` expands the complete menu. These are navigation choices;
they never grant execution permission. At most one quick-access item is marked
recommended.

For host/UI presentation, the output also emits a stable `display` model
(`es-menu-display.v1`). It supplies a compact-card layout, section labels, one
icon per main option, Chinese subtitles, two bounded examples per main option, a
single recommendation marker, and a short footer. Hosts should render this model
instead of dumping the full JSON; the machine-readable `options` and
`routeDirectory` remain unchanged and authoritative for selection. Menu aliases
and examples are maintained in the route-alias registry and
`references/menu-examples.json`; examples illustrate phrasing only and never
execute or authorize an action.

For a terminal-only view, use
`scripts/Show-ESCollaborationMenu.ps1 -PromptText '菜单' -NoColor`; add
`-Animate` for a short type-in animation. This is terminal text rendering, not a
graphical window, and it has no project side effects.

For a reusable rich-text block, use
`scripts/Render-ESCollaborationMenuRichText.ps1 -PromptText '菜单'`. The fixed
builder consumes `display` plus `references/menu-theme.json`, emits stable
Markdown with a boxed header, sections, examples and semantic color tokens, and
supports `-HtmlColor` for hosts that allow inline color spans. It does not invent
menu entries or execute actions.

The default framed output is intentionally compact: one responsibility line and
one example per option. Pass `-AllExamples` only for a detailed reference view;
the outer frame and section boundaries remain present in both modes.

User-facing Skill disclosure must use stable names only (for example,
`es-ai-collaboration-menu`). Relative paths, `SKILL.md`, `references/`, and
`scripts/` are internal evidence and must not be rendered as responsibilities or
menu labels. The `display.skillDisclosurePolicy` field makes this boundary
machine-readable.

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
