# AISpace output contract

`es-ui-intent-authoring` only emits semantic planning artifacts. It never writes Unity
Prefabs, Scenes, runtime UI, screenshots, or business data.

## Output placement

- IntentSpec candidates are task-private files under
  `ES/AISpace/Local/<agent-or-task>/Temp/UIIntent/`.
- Static validation receipts use the same task directory and carry the candidate's
  `intentId`, schema version, source hash, and validation hash.
- The Skill does not write `ES/AISpace/Public` or `Assets/ES/AISpace/Public`.
  Promotion to a shared index, if ever needed, belongs to an explicitly authorized
  downstream owner.

## Lifecycle and authority

- The AISpace binding is a stable discovery relation; the emitted candidate and receipt
  remain disposable and task-scoped.
- Output names and references are project-relative and UTF-8. A rerun may replace only
  the current task's candidate/receipt pair after validation; it may not delete source
  files or Unity assets.
- A confirmed candidate is handed to `es-ui-prefab-authoring`; this handoff does not
  transfer runtime or asset-write permission to this Skill.
