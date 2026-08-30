# Step 05 — Prompt planning

## AI analysis

Turn the accepted intent, knowledge decisions, source-derived strategies, and constraints into concrete generation directives. Specify structure, interaction semantics, visual hierarchy, responsive behavior, accessibility, and forbidden substitutions.

## Execution

Use the preflight prompt planner to produce `promptPlan` and `generatedPrompt`; include strategy IDs and required HTML markers, not generic framework names.

## Return

Return `prompt-generation` with `analysis`, `directives`, `promptHash`, `inputs`, and `decision`. A static template copied without request-derived directives returns `blocked.prompt.template-only`.

