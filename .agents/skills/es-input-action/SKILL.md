---
name: es-input-action
description: Add, bind, diagnose, or migrate ESFramework input actions across ESInputActionId, action metadata, Input System or virtual bindings, profiles, RuntimeMode filtering, services, player control requests, and self-tests. Use for missing bindings, new controls, rebinding, runtime-mode blocks, virtual input, or player input routing.
---

# Work on ES Input Actions

Maintain one traceable path from stable action identity through binding and profile resolution to runtime consumption.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Select the strong write command for adding an action, or a read-only diagnostic command for inspection.
3. Trace the action through ID, metadata, binding definition, source type, profile baking, scheme resolution, runtime service, RuntimeMode filter, and player consumer.
4. Preserve stable action IDs and binding keys. Determine whether the source is Unity Input System or an ES virtual control.
5. Update all required catalogs and defaults without creating a parallel direct-input path.
6. Validate missing bindings, duplicate IDs, value type, trigger policy, runtime-mode allow/block behavior, rebinding persistence, and player control arbitration.
7. Run the built-in ES input self-tests and relevant Unity tests. Use `$es-unity-compile` for authoritative editor evidence.
8. Run `$es-utf8-guard` and report untested devices, schemes, or PlayMode behavior.

## Required boundaries

- Do not read Unity input directly from gameplay code when `ESInputService` is the intended authority.
- Do not assign or renumber stable action IDs without verifying serialized and runtime consumers.
- Do not bypass RuntimeMode filtering or active control-request arbitration.
- Do not treat editor drawer display success as runtime binding success.
- Keep Input System bindings and virtual-control bindings explicit.

## Delivery

Report the action ID, metadata, binding source, profile and RuntimeMode path, consumer chain, self-test and Unity results, and unsupported devices or schemes.
