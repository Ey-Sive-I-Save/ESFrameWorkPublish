# ai-abc-core-static

Required cases: `core-independence`, `abcd-parity`, `a-to-b-negotiation`,
`normalized-evidence`, `failure-replan`, `permission-boundary`,
`deterministic-replay`, `generation-modes`, `creative-visibility`,
`audit-separation`, `innovation-run-stage-closure`, `tree-branch-rejoin`,
`adaptive-branch-weighting`, `player-first-use-gate`.

Source assertions: `ABCC independent`, `ABCD parity`, `A-to-B`,
`normalized evidence`, `explicit-only`, `deterministic-replay`.

Generation assertions: `three generation objectives`, `visible creative
divergence`, and `generation-before-audit separation`. Creative candidates are
kept visible with explicit risk labels; `core-high-risk` is a final audit
profile, not an early creative pruning rule.

InnovationRun assertions: `task-scoped InnovationRun`, `global convergence`,
`adaptive branch weighting`, and `player-first-use gate`. The run must create
an ordered stage plan, carry retained parent content into the next tree round,
record interaction deltas and recompute branch weights from observed gaps.

Static evidence is limited to JSON/Markdown contracts and hashes. It does not
prove Unity, Player, Runtime, performance, network or release behavior.
