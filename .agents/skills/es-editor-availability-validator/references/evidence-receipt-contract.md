# Editor availability evidence receipt

Each evidence manifest must be project-relative and identify `toolId`, `targetPath`, `unityVersion`, `capturedUtc`, `planHash`, `sourceRefs`, and `sourceRefHashes`.

Each check row must contain `id`, `status`, `evidenceLevel`, `receiptPath`, and `details`. `status` is one of `passed`, `failed`, `blocked`, or `not-run`. A passed row must point to an existing artifact or test output. The `visual` row must contain:

```json
{
  "id": "visual",
  "status": "passed",
  "bounds": {
    "minimum": "640x480",
    "maximum": "1920x1200",
    "adaptive": false,
    "strategy": "fixed"
  },
  "viewports": ["narrow", "wide", "high-dpi", "extreme-resolution"],
  "receiptPath": "ES/EditorEvidence/example-visual.json",
  "details": "..."
}
```

`strategy` must be `fixed`, `adaptive-resolve`, `content-adaptive`, `host-bounded`, or `unbounded-flexible`. Fixed bounds require `maximum`; adaptive strategies require `adaptive: true` and evidence of the corresponding policy. Missing, stale, contradictory, or out-of-scope evidence blocks the corresponding dimension.

The manifest is evidence of a validation run, not permission to edit assets or execute Unity operations.
