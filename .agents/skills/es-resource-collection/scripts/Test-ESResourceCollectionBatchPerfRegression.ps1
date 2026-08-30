[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselinePath,
    [Parameter(Mandatory=$true)][string]$CandidatePath,
    [ValidateRange(0.1,1.0)][double]$MinThroughputRatio=0.8,
    [ValidateRange(0.1,1.0)][double]$MinSpeedupRatio=0.8,
    [switch]$RequirePackageMatch
)
$ErrorActionPreference='Stop';$errors=[Collections.Generic.List[string]]::new();$checks=[Collections.Generic.List[object]]::new()
try {
    $b=Get-Content -LiteralPath (Resolve-Path $BaselinePath) -Raw -Encoding UTF8|ConvertFrom-Json
    $c=Get-Content -LiteralPath (Resolve-Path $CandidatePath) -Raw -Encoding UTF8|ConvertFrom-Json
    if($RequirePackageMatch){
        if([string]::IsNullOrWhiteSpace([string]$b.packageId) -or [string]::IsNullOrWhiteSpace([string]$c.packageId)){[void]$errors.Add('packageId required when RequirePackageMatch is enabled')}
        elseif([string]$b.packageId -ne [string]$c.packageId){[void]$errors.Add("packageId mismatch: baseline=$($b.packageId), candidate=$($c.packageId)")}
    }
    foreach($phase in 'cold','incremental'){
        $bv=[double]$b.$phase.filesPerSecond;$cv=[double]$c.$phase.filesPerSecond
        $ratio=if($bv -gt 0){$cv/$bv}else{1};$ok=$ratio -ge $MinThroughputRatio
        [void]$checks.Add([ordered]@{metric="$phase.filesPerSecond";baseline=$bv;candidate=$cv;ratio=[Math]::Round($ratio,4);threshold=$MinThroughputRatio;passed=$ok})
        if(!$ok){[void]$errors.Add("$phase throughput ratio $([Math]::Round($ratio,4)) below $MinThroughputRatio")}
    }
    $speedRatio=if([double]$b.speedupRatio -gt 0){[double]$c.speedupRatio/[double]$b.speedupRatio}else{1};$speedOk=$speedRatio -ge $MinSpeedupRatio
    [void]$checks.Add([ordered]@{metric='speedupRatio';baseline=$b.speedupRatio;candidate=$c.speedupRatio;ratio=[Math]::Round($speedRatio,4);threshold=$MinSpeedupRatio;passed=$speedOk})
    if(!$speedOk){[void]$errors.Add("speedup ratio $([Math]::Round($speedRatio,4)) below $MinSpeedupRatio")}
} catch {[void]$errors.Add($_.Exception.Message)}
[ordered]@{validator='Test-ESResourceCollectionBatchPerfRegression';baselinePath=$BaselinePath;candidatePath=$CandidatePath;requirePackageMatch=[bool]$RequirePackageMatch;minThroughputRatio=$MinThroughputRatio;minSpeedupRatio=$MinSpeedupRatio;valid=($errors.Count -eq 0);errorCount=$errors.Count;errors=@($errors);checks=@($checks);runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
if($errors.Count){exit 1}
