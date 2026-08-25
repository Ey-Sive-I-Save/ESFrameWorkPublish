[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$AssessmentPath,
  [Parameter(Mandatory=$true)][ValidatePattern('^[1-9][0-9]*$')][string]$Selection,
  [string]$ExpectedTaskKey=''
)
$ErrorActionPreference='Stop'
if(-not (Test-Path -LiteralPath $AssessmentPath -PathType Leaf)){throw "assessment not found: $AssessmentPath"}
$assessment=Get-Content -LiteralPath $AssessmentPath -Raw -Encoding UTF8|ConvertFrom-Json
if($null -eq $assessment.nextSteps){throw 'assessment has no nextSteps menu'}
if($ExpectedTaskKey -and [string]$assessment.taskKey -and [string]$assessment.taskKey -cne $ExpectedTaskKey){throw 'assessment task key mismatch'}
$items=@($assessment.nextSteps);$match=@($items|Where-Object {[int]$_.number -eq [int]$Selection})
if($match.Count -ne 1){throw "selection is not present in the current menu: $Selection"}
$item=$match[0]
if(-not [bool]$item.requiresUserChoice){throw 'selected next step is not user-selectable'}
[ordered]@{schemaVersion=1;resolver='es-next-step-selection';assessmentHash=(Get-FileHash -LiteralPath $AssessmentPath -Algorithm SHA256).Hash.ToLowerInvariant();selectionNumber=[int]$Selection;selectedId=[string]$item.id;label=[string]$item.label;reason=[string]$item.reason;requiresUserChoice=$true;execution='not-executed';nonClaims=@('Selection does not authorize writes, Runtime, network, or external processes','Caller must dispatch selectedId through an authorized task handler')}|ConvertTo-Json -Depth 8
