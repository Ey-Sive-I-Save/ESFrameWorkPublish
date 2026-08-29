# ES TaskFocusContext

TaskFocusContext is the small, deterministic attention gate for ES automation.
Import the module and use the proposal/context transition directly:

```powershell
Import-Module "$projectRoot/ES/Automation/TaskFocusContext/ESTaskFocusContext.psm1" -Force
$proposal = New-TaskFocusProposal -Focus 'My bounded task' -Priority normal `
  -AllowedScope @('ES/Automation') `
  -ForbiddenExpansion @('Unity','Git','Network') `
  -AcceptanceSignals @('static-test-pass')
$pending = Invoke-TaskFocusProposal -Current $null -Proposal $proposal
$confirmed = Invoke-TaskFocusProposal -Current $pending -Proposal $proposal `
  -UserDecision confirm -ExpectedRevision $pending.revision
$projection = New-FocusContextProjection -Context $confirmed
```

To hand the confirmed focus into TaskContextRuntime without invoking it implicitly:

```powershell
Import-Module "$projectRoot/ES/Automation/TaskFocusContext/ESTaskFocusRuntimeAdapter.psm1" -Force
$request = New-ESTaskContextRuntimeRequestFromFocus -FocusContext $confirmed `
  -TaskId 'my-task' -RoutePlanPath 'plan.json' -GoalRevisionPath 'goal.json' `
  -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' `
  -RequiredClaims @('source-integrity') -RequestedSourceScope 'ES/Automation' `
  -IdempotencyKey 'my-task-create'
```

The adapter is a pure mapping: it requires `confirmed` focus, preserves scope,
forbidden expansion, required reads, and acceptance signals, and returns a
request object for the caller's existing TaskContextRuntime entry point. It never
creates a task or performs external side effects.

For callers that already keep runtime parameters in one object, the equivalent
short form is:

```powershell
$request = New-ESTaskContextRuntimeRequestFromFocusSpec -FocusContext $confirmed `
  -RuntimeSpec ([pscustomobject]@{
    TaskId='my-task'; RoutePlanPath='plan.json'; GoalRevisionPath='goal.json'
    AcceptanceProfileId='static'; OutcomeEvaluatorId='platform.task-context-outcome-v1'
    RequiredClaims=@('source-integrity'); RequestedSourceScope='ES/Automation'
    IdempotencyKey='my-task-create'
  })
```

The spec facade is only parameter packing: it applies the same validation and
produces the same request shape as the explicit form.

The existing `Invoke-ESTaskContextRuntime.ps1 -Action Create` command also
accepts an optional `focusContext` object in its project-relative JSON input.
When present, the command validates every requested source scope through this
adapter before creating the task; inputs without `focusContext` retain the
legacy path.

The transition is revision-checked and idempotent. Conflicting or stale proposals
return `ambiguous`; they are never silently merged. `allowedScope`,
`forbiddenExpansion`, required reads, and acceptance signals are preserved in the
projection. This module is static/deterministic; it does not start Unity, invoke
external processes, access the network, or perform Git operations.

Run `Test-ESTaskFocusContext.ps1` for the bounded regression suite.
Run `Test-ESTaskFocusRuntimeAdapter.ps1` and `Test-ESTaskFocusRuntimeIntegration.ps1`
to verify mapping and consumption by the existing TaskContextRuntime create entry.
For a bounded aggregate replay, run `Test-ESTaskFocusStaticReplay.ps1`; it writes
only its deterministic report under `ES/Output/StaticReplay/`.
