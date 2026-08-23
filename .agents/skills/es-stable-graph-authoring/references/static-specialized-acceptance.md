# Stable graph authoring and deterministic projection

## Purpose

This is a responsibility-specific static acceptance plan for **es-stable-graph-authoring**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `authoring`
- Acceptance id: `stable-graph-contract`
- Runtime boundary: Static graph packet proof does not claim Unity GraphView rendering.
- Static assertions: stable graph; node ID; edge; duplicate node; packet hash

## Required specialized cases

- `graph-identity`: replay the graph-identity contract from source/configuration and record pass or blocked evidence.
- `node-id-stability`: replay the node-id-stability contract from source/configuration and record pass or blocked evidence.
- `edge-closure`: replay the edge-closure contract from source/configuration and record pass or blocked evidence.
- `duplicate-node-rejection`: replay the duplicate-node-rejection contract from source/configuration and record pass or blocked evidence.
- `packet-hash`: replay the packet-hash contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/graph-contract.md`
- `scripts/Test-ESStableGraphPacket.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
