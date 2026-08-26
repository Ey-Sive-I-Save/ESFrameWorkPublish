[CmdletBinding()]
param(
    [string]$SchemaPath,
    [string]$ModulePath
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SchemaPath)) { $SchemaPath = Join-Path $scriptRoot '..\Contracts\es-goal-v1.schema.json' }
if ([string]::IsNullOrWhiteSpace($ModulePath)) { $ModulePath = Join-Path $scriptRoot 'ESTaskContextRuntime.psm1' }
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)

function Read-StrictJson([string]$Path) {
    return $strictUtf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)) | ConvertFrom-Json -ErrorAction Stop
}

function Write-StrictJson([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
}

$cases = [Collections.Generic.List[object]]::new()
function Add-Case([string]$Name, [bool]$Passed, [string[]]$Findings=@()) {
    [void]$cases.Add([pscustomobject]@{case=$Name;status=if($Passed){'passed'}else{'failed'};findings=@($Findings)})
}

function Test-Rejected([string]$Name, [scriptblock]$Action) {
    try {
        & $Action | Out-Null
        Add-Case $Name $false @('invalid GoalRevision was accepted')
    } catch {
        Add-Case $Name $true
    }
}

$schema = Read-StrictJson $SchemaPath
$expectedRequired = @('schemaVersion','goalId','goalRevision','scope','acceptanceIntent','status','budget','parentGoalRef','revisionHash')
$actualRequired = @($schema.required | ForEach-Object { [string]$_ })
$schemaFindings = [Collections.Generic.List[string]]::new()
if ([string]$schema.'$id' -cne 'es://automation/contracts/goal/v1') { [void]$schemaFindings.Add('schema identity drifted') }
if ($schema.additionalProperties -ne $false) { [void]$schemaFindings.Add('additionalProperties must be false') }
if (@($actualRequired).Count -ne $expectedRequired.Count -or @($expectedRequired | Where-Object { $actualRequired -cnotcontains $_ }).Count -gt 0) { [void]$schemaFindings.Add('required property set drifted') }
if ([int]$schema.properties.schemaVersion.const -ne 1) { [void]$schemaFindings.Add('schemaVersion const drifted') }
if ([string]$schema.properties.status.const -cne 'frozen') { [void]$schemaFindings.Add('status must remain frozen') }
if ([string]$schema.properties.goalRevision.pattern -cne '^r[1-9][0-9]{0,8}$') { [void]$schemaFindings.Add('goalRevision pattern drifted') }
if ([int]$schema.properties.scope.minItems -ne 1 -or $schema.properties.scope.uniqueItems -ne $true -or [int]$schema.properties.scope.items.minLength -ne 1) { [void]$schemaFindings.Add('scope closure drifted') }
if ([string]$schema.properties.revisionHash.pattern -cne '^[a-f0-9]{64}$') { [void]$schemaFindings.Add('revisionHash pattern drifted') }
Add-Case 'schema-contract-closure' ($schemaFindings.Count -eq 0) @($schemaFindings)

Import-Module (Resolve-Path -LiteralPath $ModulePath).Path -Force
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-goal-v1-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

$intent = [ordered]@{profile='static-review';requiredClaims=@('source-valid')}
$budget = [ordered]@{maxReads=8;maxDepth=2}
$goal = New-ESGoalRevision -ProjectRoot $fixtureRoot -StoreRoot 'state' -GoalId 'goal-platform' -GoalRevision 'r1' -Scope @('ES/Automation','Documentation/AIKnowledge') -AcceptanceIntent $intent -Budget $budget
$resolved = Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath $goal.path
Add-Case 'valid-frozen-goal' ($resolved.goalId -ceq 'goal-platform' -and $resolved.goalRevision -ceq 'r1' -and [string]$resolved.goalRevisionHash -match '^[a-f0-9]{64}$')

$original = Read-StrictJson (Join-Path $fixtureRoot ($goal.path.Replace('/',[IO.Path]::DirectorySeparatorChar)))
$reordered = [ordered]@{
    revisionHash=$original.revisionHash
    parentGoalRef=$original.parentGoalRef
    budget=[ordered]@{maxDepth=2;maxReads=8}
    status='frozen'
    acceptanceIntent=[ordered]@{requiredClaims=@('source-valid');profile='static-review'}
    scope=@('ES/Automation','Documentation/AIKnowledge')
    goalRevision='r1'
    goalId='goal-platform'
    schemaVersion=1
}
Write-StrictJson (Join-Path $fixtureRoot 'reordered.json') $reordered
$reorderedResult = Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'reordered.json'
Add-Case 'canonical-property-order' ([string]$reorderedResult.goalRevisionHash -ceq [string]$goal.goalRevisionHash)

$extra = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$extra | Add-Member -NotePropertyName producerOutcome -NotePropertyValue 'accepted'
Write-StrictJson (Join-Path $fixtureRoot 'extra.json') $extra
Test-Rejected 'additional-property-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'extra.json' }

$forged = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$forged.revisionHash = '0' * 64
Write-StrictJson (Join-Path $fixtureRoot 'forged.json') $forged
Test-Rejected 'forged-hash-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'forged.json' }

$mutable = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$mutable.status = 'active'
Write-StrictJson (Join-Path $fixtureRoot 'mutable.json') $mutable
Test-Rejected 'non-frozen-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'mutable.json' }

$emptyScope = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$emptyScope.scope = @('')
Write-StrictJson (Join-Path $fixtureRoot 'empty-scope.json') $emptyScope
Test-Rejected 'empty-scope-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'empty-scope.json' }

$wrongIntent = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$wrongIntent.acceptanceIntent = 42
Write-StrictJson (Join-Path $fixtureRoot 'wrong-intent.json') $wrongIntent
Test-Rejected 'acceptance-intent-type-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'wrong-intent.json' }

$wrongBudget = $original | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$wrongBudget.budget = @('unbounded')
Write-StrictJson (Join-Path $fixtureRoot 'wrong-budget.json') $wrongBudget
Test-Rejected 'budget-type-negative' { Resolve-ESGoalRevision -ProjectRoot $fixtureRoot -GoalRevisionPath 'wrong-budget.json' }

Test-Rejected 'duplicate-scope-negative' {
    New-ESGoalRevision -ProjectRoot $fixtureRoot -StoreRoot 'state' -GoalId 'goal-duplicate' -GoalRevision 'r1' -Scope @('ES/Automation','es/automation') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=1})
}

Test-Rejected 'immutable-revision-negative' {
    New-ESGoalRevision -ProjectRoot $fixtureRoot -StoreRoot 'state' -GoalId 'goal-platform' -GoalRevision 'r1' -Scope @('different-scope') -AcceptanceIntent $intent -Budget $budget
}

$failed = @($cases | Where-Object status -eq 'failed')
[pscustomobject][ordered]@{
    schemaVersion=1
    validator='Test-ESGoalV1'
    status=if($failed.Count){'failed'}else{'passed'}
    caseCount=$cases.Count
    passedCount=@($cases | Where-Object status -eq 'passed').Count
    failedCount=$failed.Count
    cases=@($cases)
    schemaPath=(Resolve-Path -LiteralPath $SchemaPath).Path
    fixtureRoot=$fixtureRoot
    runtimeStatus='runtime-not-run'
    claimsNotProven=@('production route integration','Unity or Worker Runtime','release acceptance')
} | ConvertTo-Json -Depth 12
if ($failed.Count) { exit 1 }
