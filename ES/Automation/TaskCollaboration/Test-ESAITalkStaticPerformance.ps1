[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$SessionsRoot,
    [ValidateRange(1,20)][int]$Iterations = 5,
    [ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,
    [ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144,
    [string]$BudgetPath = 'ES/Output/Performance/aitalk-performance-budget.json',
    [string]$ReportPath = 'ES/Output/Performance/aitalk-static-benchmark.json'
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
if ([string]::IsNullOrWhiteSpace($SessionsRoot)) { $SessionsRoot = Join-Path $root 'Assets/Plugins/ES/AITalk/Sessions' }
Import-Module (Join-Path $PSScriptRoot 'ESAITalkProjectAggregation.psm1') -Force
$process = Get-Process -Id $PID
$warmupWatch = [Diagnostics.Stopwatch]::StartNew()
$warmup = Invoke-ESAITalkProjectAggregation -SessionsRoot $SessionsRoot -MaxMessagesPerSession $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes
$warmupWatch.Stop()
$samples = [Collections.Generic.List[object]]::new()
for ($i = 1; $i -le $Iterations; $i++) {
    [GC]::Collect()
    $beforeBytes = [GC]::GetTotalMemory($false)
    $beforeWorkingSet = (Get-Process -Id $PID).WorkingSet64
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $result = Invoke-ESAITalkProjectAggregation -SessionsRoot $SessionsRoot -MaxMessagesPerSession $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes
    $watch.Stop()
    $afterBytes = [GC]::GetTotalMemory($false)
    $afterWorkingSet = (Get-Process -Id $PID).WorkingSet64
    [void]$samples.Add([pscustomobject][ordered]@{ iteration=$i; elapsedMilliseconds=[Math]::Round($watch.Elapsed.TotalMilliseconds,3); managedBytesBefore=$beforeBytes; managedBytesAfter=$afterBytes; managedBytesDelta=($afterBytes-$beforeBytes); workingSetBytesBefore=$beforeWorkingSet; workingSetBytesAfter=$afterWorkingSet; workingSetBytesDelta=($afterWorkingSet-$beforeWorkingSet); status=[string]$result.status; aggregationHash=[string]$result.aggregationHash; messageCount=[int]$result.messageCount; sessionCount=[int]$result.sessionCount })
}
$avg = [Math]::Round((@($samples | Measure-Object elapsedMilliseconds -Average).Average),3)
$max = [Math]::Round((@($samples | Measure-Object elapsedMilliseconds -Maximum).Maximum),3)
$peakManagedDelta = [int64](@($samples | Measure-Object managedBytesDelta -Maximum).Maximum)
$peakWorkingSetDelta = [int64](@($samples | Measure-Object workingSetBytesDelta -Maximum).Maximum)
$budgetFullPath = if ([IO.Path]::IsPathRooted($BudgetPath)) { $BudgetPath } else { Join-Path $root $BudgetPath }
$budget = [Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $budgetFullPath).Path)) | ConvertFrom-Json
$timeBudget = @($budget.metrics | Where-Object { [string]$_.metric -eq 'host steady-state aggregation elapsed time for current baseline' })[0]
$memoryBudget = @($budget.metrics | Where-Object { [string]$_.metric -eq 'host peak managed-memory delta for current baseline' })[0]
$budgetChecks = [ordered]@{ elapsedMilliseconds = [pscustomobject]@{ threshold=[double]$timeBudget.threshold; observed=$max; passed=($max -le [double]$timeBudget.threshold) }; managedBytesDelta = [pscustomobject]@{ threshold=[int64]$memoryBudget.threshold; observed=$peakManagedDelta; passed=($peakManagedDelta -le [int64]$memoryBudget.threshold) } }
$status = if ($budgetChecks.elapsedMilliseconds.passed -and $budgetChecks.managedBytesDelta.passed) { 'passed' } else { 'failed' }
$report = [ordered]@{ schemaVersion=1; benchmark='Test-ESAITalkStaticPerformance'; status=$status; platform='Windows PowerShell host'; scenario='AITalk project aggregation'; warmupIterations=1; steadyStateIterations=$Iterations; inputSessionCount=[int]$warmup.sessionCount; inputMessageCount=[int]$warmup.messageCount; configuredLimits=[ordered]@{maxSessions=256;maxMessagesPerSession=$MaxMessagesPerSession;maxMessageBytes=$MaxMessageBytes}; budgetPath=$BudgetPath; budgetChecks=$budgetChecks; warmupElapsedMilliseconds=[Math]::Round($warmupWatch.Elapsed.TotalMilliseconds,3); averageSteadyStateMilliseconds=$avg; peakSteadyStateMilliseconds=$max; peakManagedBytesDelta=$peakManagedDelta; peakWorkingSetBytesDelta=$peakWorkingSetDelta; samples=@($samples); runtimeStatus='runtime-not-run'; claimsNotProven=@('Unity/Player/IL2CPP latency or GC','cross-process mailbox cost','release performance') }
$fullReport = Join-Path $root $ReportPath; $parent = Split-Path -Parent $fullReport; if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }; [IO.File]::WriteAllText($fullReport,($report|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false)); $report | ConvertTo-Json -Depth 30
if ($status -ne 'passed') { exit 1 }
