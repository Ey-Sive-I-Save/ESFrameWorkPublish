# SO表格工具 AI 协作说明

Last updated: 2026-07-18

Responsibility: this file is specifically for the SO table tool centered on `ESSoTableDataRule`.

It records verified engineering context for AI agents modifying the SO table import/export system, its editor UI, its safety checks, and the namespace boundaries directly touched by that tool. It is project memory, not product documentation.

Do not treat this as a general guide for all ES systems. For state, IK, input, player architecture, or other systems, use their own notes under `Assets/Plugins/ES/AIWarnings`.

## Core Rule

Do not make broad rewrites in `Assets/Plugins/ES` without compiling after each coherent step. This project contains Unity serialization, Odin Inspector metadata, reflection-based type lookup, editor windows, and generated/asset-bound workflows. A change that compiles in C# can still break Unity editor behavior, but a change that does not compile is immediately unacceptable.

Minimum verification command:

```powershell
dotnet build "F:\aaProject\ESFrameWorkPublish\ES_Stand.csproj" --no-restore -v:minimal
```

Also run targeted searches after namespace or text changes:

```powershell
rg -n "using ES\.ES|namespace ES\.ESInstaller|ES\.ES\." "F:\aaProject\ESFrameWorkPublish\Assets" --glob "*.cs"
```

## Namespace Policy Relevant To The Table Tool

The project is moving toward four namespace layers, but there is no asmdef-level split yet. Be conservative.

`ES`
: Stable public API. Keep types here when gameplay/business developers may reference, implement, inherit, serialize, or configure them.

`ES.Internal`
: Runtime/editor-independent implementation details. Use for helpers that are not user contracts.

`ES.Editor`
: Public editor extension API, only when external developers are expected to inherit or call it.

`ES.EditorInternal`
: Concrete editor windows, drawers, visual elements, installers, menu builders, and implementation-only editor utilities.

Already migrated:

- `ES.Internal.ESSoTableRuleTypeUtility`
- `ES.EditorInternal.ESSoTableDataRuleEditor`
- `ES.EditorInternal.Installer.ESInstaller`
- `ES.EditorInternal.Installer.MenuItemPathDefine`
- Concrete drawer implementations under `Assets/Plugins/ES/Editor/ESDrawer`
- GraphView editor implementation types under `Assets/Plugins/ES/Editor/ESGraphView`, except `NodeRunner.cs` which is effectively only a placeholder/comment file.

Do not blindly migrate these yet:

- `ESSoTableDataRule`
- `ESSoTableRuleUseBatch`
- `ESSoTableRuleSourceBinding`
- `ESTable...` configuration enums
- `ESRowContainerAttribute`
- `IESRowBindingProvider`
- `ESWindowPageBase`
- `ESMenuTreeWindowAB`
- TrackView and SODataInfoWindow types

Reason: these are either public configuration/API surfaces or high-coupling editor systems with UXML, static references, nested page classes, and menu state. Move them only in a dedicated pass.

## SO Table Tool Safety Invariants

The SO table tool is centered on `ESSoTableDataRule` and its partial files under:

```text
Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/EditorOnly/InfoType/
```

Important safety behaviors currently expected:

- Direct export calls plan precheck before writing.
- Direct import records the active table path, runs assertion/risk checks, then writes.
- Batch execution shows a choice: cancel, execute, or generate plan.
- Plan generation is read-only: no SO writes, no table writes, no `SaveAssets`.
- High-risk operations require confirmation:
  - delete SO
  - delete child row
  - clear field
  - overwrite non-empty field
  - rebuild table
- CSV/XLSX writes use temp-file replacement and create backups under `_backups`.
- Delete operations clear execution cache to avoid later batches using deleted objects.
- Compile after any changes to import/export/plan logic.

Do not remove these checks to simplify flow.

## 2026-07-18 Corrections And Warnings

These points correct stale assumptions from earlier work. Future AI agents should treat them as current requirements, not optional polish.

### Do Not Make Users Hand-Type Discoverable Values

Batch-level field filtering must not rely on developers typing comma-separated field names in normal UI. The UI should expose selectable fields from the current mapping.

Current expectation:

- Field filtering is checkbox/select based.
- Slice column selection uses a dropdown from mapped table columns.
- Row key column selection uses a dropdown from mapped table columns.
- List field path uses a dropdown from reflected List/Dictionary fields on the current owner type.
- Element key field path uses a dropdown from reflected key-like fields on the element type.
- Target Group/Info selection uses dropdown candidates from the current batch source when available.

The underlying string fields still exist for serialization and super-batch table compatibility. Do not present them as the primary workflow.

### Serial Child Tables Should Be Sparse By Default

For List-row serial tables, the user should not have to repeat parent/SO fields on every child row.

Current expectation:

- First child row of an owner writes parent columns.
- Subsequent child rows under the same owner leave parent columns empty.
- Import inherits the previous explicit owner when parent key columns are empty.
- Inherited child rows must not write blank parent cells back to the SO owner.
- The `owner` row directive still exists, but it is a special row for writing only owner fields; it is not the normal way to group child rows.

Do not regress to the old dense-table behavior where every child row repeats full parent data.

### Mapping Stability

`soFieldPath` is the stable field identity. Table column names are display/matching entry points.

Current import matching accepts aliases:

- active exported table name
- `columnName`
- `displayName`
- `soFieldPath`

Export-to-existing-table should avoid creating duplicate columns when the user switches between English column names and Chinese display names. It should use header/comment compatibility where possible.

Still unsafe:

- duplicate aliases across different fields
- changing `soFieldPath` to point at another field
- manually replacing headers with unrelated names and removing matching comments

### Error Reporting Must Be Structured

Do not add new bare `Debug.LogWarning("some text")` for table import/export failures when row/column context is available.

Current structured error format should include:

- stage
- batch
- table path
- row
- column
- column name
- field path
- target type
- target asset
- reason
- suggestion

Use the table error helpers in `ESSoTableDataRule.ErrorReport.cs` for new import/export/plan errors. Existing direct logs can be migrated gradually, but new work should not add more unstructured table errors.

### Super Batch Is Not Yet Commercial-Complete

Super batch is a useful direction, but do not claim it is fully commercial-grade yet. It still needs stronger visualized parsed results, relation-table validation, failure isolation, and a clearer preflight report for derived child batches.

## Table Format Facts

Current standard generated table structure:

1. `##var`
2. `##type`
3. `##group`
4. `##comment`
5. `##assert`
6. `##rowDirective...`

Data starts at row 7.

XLSX supports:

- row directive dropdown in data column A
- column assertion dropdown on `##assert` row
- enum value dropdowns for enum data columns
- comments/styles/freezing

CSV cannot support real comments/dropdowns/styles; it can only encode extra rows/cells.

Implemented column assertions:

- `required`
- `unique`
- `json`
- `asset`
- `range:min..max`
- `regex:pattern`

Do not claim other assertions are implemented unless code is added and verified.

## Serial Child Row Rules

Serial child rows are not inferred from every `List<T>`. They are controlled by row binding/bridge rules. Be careful not to reintroduce broad automatic expansion of all lists.

Key points:

- Object key and child row key are separate concepts.
- `owner` row directive writes SO owner fields only; it must not create a child element.
- Empty child row key is allowed only when row binding allows it and the active child sync/write mode supports keyless rebuild/order behavior.
- In keyed modes, empty child key should warn/skip rather than silently creating ambiguous data.
- Deleting a child uses explicit delete directive and should be treated as high risk in plan output.

## Group / Info Rules

Group key is a container locator, not just an ordinary writable field.

Current import behavior:

- If table row has Group key, use it.
- If row Group key is empty, use active batch target Group if configured.
- If still empty and source resolves exactly one Group, use that single Group.
- If multiple Groups exist and no Group key/target Group is provided, do not guess.
- Creating a Group requires a non-empty effective key.
- Empty-key Group auto-creation is intentionally blocked to avoid writing Info into the wrong container.

When creating Info inside a Group:

- Info key must be non-empty.
- If Info implements `ISoDataInfo`, set key.
- If Info implements `IString`, set string key.

Group internal key synchronization may need future improvement if Group types expose a formal key interface. Do not assume it is already complete.

## Multi-Batch Performance Context

A lightweight execution cache exists:

```text
ESSoTableDataRule.ExecutionCache.cs
```

It caches within a single plan/execution lifecycle:

- table by path
- owners by source signature
- groups by source signature

It is intentionally not a persistent project cache. It is cleared after delete operations. Avoid changing it into global/static long-lived cache unless you also handle asset mutation, folder refresh, and domain reload.

Known remaining performance hotspots:

- `AssetDatabase` operations
- `ImportAsset`
- `SaveAssets`
- XLSX zip/XML writing
- repeated compile-column work in complex plan/execute paths

The table/owner/group read cache reduces repeated work across multiple batches using the same source/table, but it is not a full performance solution.

## Encoding And UI Text

There were historical mojibake strings in table tool plan/report/UI text. Do not add non-readable fallback strings.

After large text edits, scan the touched area for common mojibake fragments and replacement characters. Keep the actual search pattern in command history or local notes; do not paste unreadable sample text into source-controlled documentation.

This scan is a warning check, not a proof. Human review is still required for UI labels, tooltips, logs, and plan reports.
## Editing Discipline

- Do not revert unrelated dirty files.
- Prefer small namespace migrations with compile verification.
- Keep public API stable unless the user explicitly requests migration.
- Editor implementation classes can move to `ES.EditorInternal` more safely than runtime serialized/configuration types.
- Avoid moving types referenced by Unity serialized type names, Odin value dropdown strings, UXML factories, or reflection-based type names without a focused migration plan.

## Current High-Risk Areas For Future AI Agents

- `ESTrackViewWindow` and related TrackView UI classes: large, highly coupled, currently partly global namespace. Migrate only as a dedicated task.
- `ESSODataInfoWindow` and nested page/helper classes: large, Odin-heavy, menu-state-heavy. Migrate only as a dedicated task.
- `ESSoTableDataRule` partial system: many features share state through partial methods/fields; avoid isolated "cleanup" edits without searching all partial files.
- asmdef splitting: not currently done. Namespace changes alone do not enforce assembly boundaries.
