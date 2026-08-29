# weapon-abc-part-static

Required cases: `core-binding`, `weapon-mapping`, `authoring-toolchain`,
`route-stage-closure`, `canonical-authority`, `dual-track`, `explicit-fallback`,
`evidence-boundary`, `deterministic-replay`.

Source assertions: `coreRef`, `Weapon ABCP`, `ABCC-backed`, `canonical owner`,
`no silent merge`, `ABCD.Dynamic`, `runtime-not-run`.

The authoring toolchain is `scripts/New-ESAbcPartContract.ps1` plus
`scripts/Test-ESAbcPartContract.ps1`. The validator must invoke the ABCC Core
replay and verify that every declared Part capability and A intent has exactly
one deterministic A-to-B mapping, and that every route template stage is
present in the RouteStage registry. A Part with a missing producer, an unbound
capability, a many-to-one mapping that the current replay format cannot
represent, or a silent Dynamic fallback is blocked before any Unity or Runtime
step.

Static evidence cannot prove Unity Prefab import, firing, collision, damage,
input, performance, Player or release behavior.
