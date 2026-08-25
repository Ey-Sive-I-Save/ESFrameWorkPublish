# IntentSpec v1

`IntentSpec` is a semantic candidate between a user brief and ScreenSpec v3. It is not a runtime
command and has no authority over business data.

Required fields:

```json
{
  "schemaVersion": 1,
  "status": "confirmed",
  "intentId": "equip-item",
  "primaryAction": "equip",
  "secondaryActions": ["inspect", "compare", "cancel"],
  "screenFamilies": ["collection"],
  "informationPriority": ["item-list", "selected-item", "stats", "action-bar"],
  "requiredStates": ["default", "selected", "disabled", "empty", "loading", "error", "long-content"],
  "layoutPreferences": {
    "wide": {"composition": "grid-detail", "detailVisibility": "expanded"},
    "narrow": {"composition": "list-detail", "detailVisibility": "collapsed"}
  },
  "inputModalities": ["pointer", "keyboard", "gamepad"],
  "businessBridge": "equipment-domain",
  "visualOnly": true,
  "confidence": 0.92,
  "missingInputs": [],
  "blockedWhen": []
}
```

Allowed status values are `confirmed`, `needs-clarification`, and `blocked`. `primaryAction` is
one registry action. `screenFamilies` and all state IDs must exist in the current UI registry.
`businessBridge` is a stable future integration label, not a data source. `missingInputs` and
`blockedWhen` must explain why a plan is not ready. A blocked or clarification plan must not be
passed to materialization.

The validator rejects unknown fields, duplicate actions, confidence outside 0..1, non-visual
plans, multiple primary actions, runtime component names and business-shaped payloads such as
items, prices, quantities, stats or inventory records.
