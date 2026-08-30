# AISpace output contract

`es-editor-tooling` may produce diagnostics and visual-development evidence, but AISpace
must not become a second Unity asset or Automation authority.

## Output placement

- Transient previews, captures, screenshots, logs, and reload snapshots belong under
  `ES/AISpace/Local/<agent-or-task>/Temp/EditorTooling/<category>/`.
- A retained user-facing report or handoff index may be written under
  `ES/AISpace/Public/EditorTooling/<topic-or-task>/`. It contains project-relative
  pointers, hashes, status, and guarded quick-open information; it is an index, not a
  replacement for the referenced artifact.
- Unity-imported assets remain under `Assets/ES/AISpace/Public/<domain>/` only when a
  separate, explicitly authorized asset workflow owns them. Editor tooling does not
  obtain blanket asset-publishing permission from this binding.
- Existing `ES/Output`, `ES/Automation`, and Codex-history authorities remain authoritative
  for their own reports, run records, contracts, and session history.

## Lifecycle and authority

- Local captures and caches are disposable and task-scoped. Public entries are retained
  and versioned indexes only; raw binary evidence is not silently promoted there.
- Every user-facing entry records a stable project-relative path, artifact hash, producer,
  and guarded open/copy-path action when the host supports it.
- Cache keys must not persist live Unity object references across domain reload. Cleanup is
  limited to the current task's temporary subtree and never mutates source or Git state.
