# Step 06 — Visual and responsive design

## AI analysis

AI converts visual direction into tokens, hierarchy, spacing, motion and reduced-motion behavior, then defines desktop/mobile layouts and loading/empty/error/success/offline states. Deep design returns these arrays for HTML materialization.

## Execution

Read the visual requirements and responsive profiles from the accepted design spec; materialize tokens as CSS variables and state classes.

## Return

Return `visual-responsive` with token IDs, breakpoint rules, state variants, contrast/focus decisions, and reduced-motion policy. Missing a mobile or failure state is `blocked.visual.incomplete-state-model`.
