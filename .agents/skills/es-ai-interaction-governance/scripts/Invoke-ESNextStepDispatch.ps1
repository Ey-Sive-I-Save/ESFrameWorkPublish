[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$AssessmentPath,
  [Parameter(Mandatory=$true)][ValidatePattern('^[1-9][0-9]*$')][string]$Selection,
  [string]$ProjectRoot='',
  [string]$TaskKey='',
  [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$PlanHash='',
  [ValidateSet('skill-only','knowledge-only','aiwarnings-only','skill-knowledge','skill-knowledge-aiwarnings')][string]$CollectionSelection='skill-knowledge-aiwarnings',
  [string[]]$ReadPaths=@(),
  [string]$OutputPath='ES/Output/Interaction/context-collection-receipt.json'
)
$ErrorActionPreference='Stop'
$skillRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolver=(Resolve-Path (Join-Path $PSScriptRoot 'Resolve-ESNextStepSelection.ps1')).Path
$selected=@(& $resolver -AssessmentPath $AssessmentPath -Selection $Selection)|ConvertFrom-Json
switch([string]$selected.selectedId){
  'clarify-objective' {
    [ordered]@{schemaVersion=1;dispatcher='es-next-step-dispatch';selectionNumber=[int]$Selection;selectedId=$selected.selectedId;dispatch='request-user-clarification';execution='not-executed';prompt='Provide target, scope, constraints, and acceptance criteria before continuing.';requiresUserInput=$true;nonClaims=@('No write, Runtime, collection, or external process was executed')}|ConvertTo-Json -Depth 8
  }
  'offer-context-collection' {
    if([string]::IsNullOrWhiteSpace($ProjectRoot) -or [string]::IsNullOrWhiteSpace($TaskKey) -or [string]::IsNullOrWhiteSpace($PlanHash)){throw 'context collection dispatch requires ProjectRoot, TaskKey and PlanHash'}
    if(@($ReadPaths).Count -eq 0){throw 'context collection dispatch requires caller-resolved ReadPaths'}
    $collector=(Resolve-Path (Join-Path $PSScriptRoot 'Invoke-ESContextCollection.ps1')).Path
    & $collector -ProjectRoot $ProjectRoot -TaskKey $TaskKey -PlanHash $PlanHash -Selection $CollectionSelection -ReadPaths $ReadPaths -OutputPath $OutputPath
  }
  'run-static-validation' {
    [ordered]@{schemaVersion=1;dispatcher='es-next-step-dispatch';selectionNumber=[int]$Selection;selectedId=$selected.selectedId;dispatch='request-static-validation';execution='not-executed';requiresUserChoice=$true;nonClaims=@('Target Skill and validator were not inferred','No write, Runtime, or external process was executed')}|ConvertTo-Json -Depth 8
  }
  default { throw "selected next step has no authorized dispatcher: $($selected.selectedId)" }
}
