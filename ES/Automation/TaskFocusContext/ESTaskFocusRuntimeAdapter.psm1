Set-StrictMode -Version Latest
$focusModule = Join-Path $PSScriptRoot 'ESTaskFocusContext.psm1'
Import-Module (Resolve-Path -LiteralPath $focusModule).Path -Global -Force
$schemaModule = Join-Path $PSScriptRoot '..\Contracts\ESJsonSchemaLite.psm1'
Import-Module (Resolve-Path -LiteralPath $schemaModule).Path -Global -Force

function Assert-FocusRuntimeId([string]$Value,[string]$Name) {
  if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{0,159}$') {
    throw "$Name is invalid"
  }
}

function Assert-FocusRequestedScope([string]$Requested,[object[]]$Allowed) {
  $requestedNorm = ([string]$Requested).Replace('\','/').Trim('/').ToLowerInvariant()
  if ([string]::IsNullOrWhiteSpace($requestedNorm) -or $requestedNorm.Contains('..')) {
    throw 'RequestedSourceScope is outside the confirmed FocusContext scope.'
  }
  $matches = @($Allowed | ForEach-Object {
    $allowedNorm = ([string]$_).Replace('\','/').Trim('/').ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($allowedNorm) -and
        ($requestedNorm -eq $allowedNorm -or $requestedNorm.StartsWith($allowedNorm + '/', [StringComparison]::Ordinal))) { $true }
  })
  if ($matches.Count -eq 0) { throw 'RequestedSourceScope is outside the confirmed FocusContext scope.' }
}

function New-ESTaskContextRuntimeRequestFromFocus {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)]$FocusContext,
    [Parameter(Mandatory)][string]$TaskId,
    [Parameter(Mandatory)][string]$RoutePlanPath,
    [Parameter(Mandatory)][string]$GoalRevisionPath,
    [Parameter(Mandatory)][string]$AcceptanceProfileId,
    [Parameter(Mandatory)][string]$OutcomeEvaluatorId,
    [Parameter(Mandatory)][string[]]$RequiredClaims,
    [hashtable]$RequiredClaimVerifiers = @{},
    [Parameter(Mandatory)][string]$RequestedSourceScope,
    [Parameter(Mandatory)][string]$IdempotencyKey
  )
  Assert-FocusRuntimeId $TaskId 'TaskId'
  if ([string]$FocusContext.status -cne 'confirmed') {
    throw "FocusContext must be confirmed; current status is '$($FocusContext.status)'."
  }
  if ([int]$FocusContext.revision -lt 1 -or [int]$FocusContext.focusRevision -ne [int]$FocusContext.revision -or
      [string]$FocusContext.proposalHash -notmatch '^[a-f0-9]{64}$' -or [string]$FocusContext.focusProposalHash -cne [string]$FocusContext.proposalHash -or
      [string]$FocusContext.focusContextId -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{0,159}$' -or
      [string]$FocusContext.focusScopeHash -notmatch '^[a-f0-9]{64}$' -or
      (([string]$FocusContext.focusReceiptHash).Length -gt 0 -and [string]$FocusContext.focusReceiptHash -notmatch '^[a-f0-9]{64}$')) {
    throw 'FocusContext identity is invalid.'
  }
  $expectedScopeHash = Get-FocusHash ([ordered]@{allowedScope=@($FocusContext.allowedScope);forbiddenExpansion=@($FocusContext.forbiddenExpansion)})
  if ([string]$FocusContext.focusScopeHash -cne $expectedScopeHash) { throw 'FocusContext scope hash mismatch.' }
  if ([string]::IsNullOrWhiteSpace($RoutePlanPath) -or [string]::IsNullOrWhiteSpace($GoalRevisionPath) -or
      [string]::IsNullOrWhiteSpace($AcceptanceProfileId) -or [string]::IsNullOrWhiteSpace($OutcomeEvaluatorId) -or
      [string]::IsNullOrWhiteSpace($RequestedSourceScope) -or [string]::IsNullOrWhiteSpace($IdempotencyKey) -or
      @($RequiredClaims).Count -lt 1) { throw 'Runtime request contains an invalid required field.' }
  Assert-FocusRequestedScope -Requested $RequestedSourceScope -Allowed @($FocusContext.allowedScope)
  [pscustomobject][ordered]@{
    schemaVersion = 1
    requestType = 'TaskFocusRuntimeRequest'
    taskId = $TaskId
    routePlanPath = $RoutePlanPath
    goalRevisionPath = $GoalRevisionPath
    acceptanceProfileId = $AcceptanceProfileId
    outcomeEvaluatorId = $OutcomeEvaluatorId
    requiredClaims = @($RequiredClaims)
    requiredClaimVerifiers = $RequiredClaimVerifiers
    requestedSourceScope = $RequestedSourceScope
    idempotencyKey = $IdempotencyKey
    focusContextId = [string]$FocusContext.focusContextId
    focusRevision = [int]$FocusContext.focusRevision
    focusProposalHash = [string]$FocusContext.focusProposalHash
    focusReceiptHash = if([string]::IsNullOrWhiteSpace([string]$FocusContext.focusReceiptHash)){$null}else{[string]$FocusContext.focusReceiptHash}
    focusScopeHash = [string]$FocusContext.focusScopeHash
    allowedScope = @($FocusContext.allowedScope)
    forbiddenExpansion = @($FocusContext.forbiddenExpansion)
    requiredReads = @($FocusContext.requiredReads)
    acceptanceSignals = @($FocusContext.acceptanceSignals)
  }
}

function New-ESTaskContextRuntimeRequestFromFocusCheckpoint {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)]$Checkpoint,
    [Parameter(Mandatory)][string]$CheckpointTaskId,
    [Parameter(Mandatory)][string]$TaskId,
    [Parameter(Mandatory)][string]$RoutePlanPath,
    [Parameter(Mandatory)][string]$GoalRevisionPath,
    [Parameter(Mandatory)][string]$AcceptanceProfileId,
    [Parameter(Mandatory)][string]$OutcomeEvaluatorId,
    [Parameter(Mandatory)][string[]]$RequiredClaims,
    [hashtable]$RequiredClaimVerifiers = @{},
    [Parameter(Mandatory)][string]$RequestedSourceScope,
    [Parameter(Mandatory)][string]$IdempotencyKey
  )
  $checkpointSchema = Join-Path $PSScriptRoot '..\Contracts\es-task-focus-checkpoint-v1.schema.json'
  $schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $checkpointSchema -Value $Checkpoint)
  if ($schemaErrors.Count -gt 0) { throw ('Focus checkpoint schema validation failed: ' + ($schemaErrors -join '; ')) }
  $context = Restore-FocusCheckpoint -Checkpoint $Checkpoint -TaskId $CheckpointTaskId
  $params = @{
    FocusContext=$context; TaskId=$TaskId; RoutePlanPath=$RoutePlanPath
    GoalRevisionPath=$GoalRevisionPath; AcceptanceProfileId=$AcceptanceProfileId
    OutcomeEvaluatorId=$OutcomeEvaluatorId; RequiredClaims=$RequiredClaims
    RequiredClaimVerifiers=$RequiredClaimVerifiers; RequestedSourceScope=$RequestedSourceScope
    IdempotencyKey=$IdempotencyKey
  }
  New-ESTaskContextRuntimeRequestFromFocus @params
}

function New-ESTaskContextRuntimeRequestFromFocusSpec {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)]$FocusContext,
    [Parameter(Mandatory)]$RuntimeSpec
  )
  $required = @('TaskId','RoutePlanPath','GoalRevisionPath','AcceptanceProfileId','OutcomeEvaluatorId','RequiredClaims','RequestedSourceScope','IdempotencyKey')
  foreach ($name in $required) {
    if ($null -eq $RuntimeSpec.PSObject.Properties[$name] -or $null -eq $RuntimeSpec.$name) {
      throw "RuntimeSpec.$name is required."
    }
  }
  $params = @{
    FocusContext = $FocusContext
    TaskId = [string]$RuntimeSpec.TaskId
    RoutePlanPath = [string]$RuntimeSpec.RoutePlanPath
    GoalRevisionPath = [string]$RuntimeSpec.GoalRevisionPath
    AcceptanceProfileId = [string]$RuntimeSpec.AcceptanceProfileId
    OutcomeEvaluatorId = [string]$RuntimeSpec.OutcomeEvaluatorId
    RequiredClaims = @($RuntimeSpec.RequiredClaims)
    RequiredClaimVerifiers = if ($null -ne $RuntimeSpec.PSObject.Properties['RequiredClaimVerifiers']) { $RuntimeSpec.RequiredClaimVerifiers } else { @{} }
    RequestedSourceScope = [string]$RuntimeSpec.RequestedSourceScope
    IdempotencyKey = [string]$RuntimeSpec.IdempotencyKey
  }
  New-ESTaskContextRuntimeRequestFromFocus @params
}

Export-ModuleMember -Function New-ESTaskContextRuntimeRequestFromFocus,New-ESTaskContextRuntimeRequestFromFocusCheckpoint,New-ESTaskContextRuntimeRequestFromFocusSpec
