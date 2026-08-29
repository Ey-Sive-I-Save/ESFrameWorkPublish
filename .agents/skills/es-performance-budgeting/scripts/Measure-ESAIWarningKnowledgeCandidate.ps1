[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$WarningPath,
    [int]$Iterations = 5,
    [string]$OutputPath = 'ES/Output/AIWarningKnowledge/candidate-baseline.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$generator=Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/New-ESAIWarningKnowledgeCandidate.ps1'
$utf8=[Text.UTF8Encoding]::new($false,$true)
if($Iterations -lt 1 -or $Iterations -gt 100){throw 'ITERATIONS_OUT_OF_RANGE'}
$times=[Collections.Generic.List[double]]::new()
for($i=0;$i -lt $Iterations;$i++){
    $sw=[Diagnostics.Stopwatch]::StartNew()
    $null=& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $generator -ProjectRoot $root -WarningPath $WarningPath | ConvertFrom-Json
    if($LASTEXITCODE -ne 0){throw "CANDIDATE_RUN_FAILED:$LASTEXITCODE"}
    $sw.Stop();$times.Add($sw.Elapsed.TotalMilliseconds)
}
$sorted=@($times|Sort-Object)
$sha=[Security.Cryptography.SHA256]::Create();try{$generatorHash=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($generator)))).Replace('-','').ToLowerInvariant();$indexPath=Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml';$indexHash=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($indexPath)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$warningFull=Join-Path $root ($WarningPath.Replace('/',[IO.Path]::DirectorySeparatorChar));$indexBytes=[IO.File]::ReadAllBytes($indexPath);$reversePath=Join-Path $root 'ES/Automation/Candidates/AIWarningKnowledge/knowledge-reverse-index.json';$entryCount=if(Test-Path -LiteralPath $reversePath){([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($reversePath))|ConvertFrom-Json).entryCount}else{'unknown'}
$result=[ordered]@{
 schemaVersion=1; recordType='ESAIWarningKnowledgeCandidatePerformanceBaseline'; skillName='es-performance-budgeting'; case='candidate-orchestration'; status='measured'; evidenceLevel='S3'; declaredOutcome='measured';
 timestampUtc=(Get-Date).ToUniversalTime().ToString('O'); platform="Windows PowerShell $($PSVersionTable.PSVersion)"; scenario='single Warning, KnowledgeIndex metadata match'; inputSize='one Warning plus current KnowledgeIndex'; iterations=$Iterations; warmup='none';
 metrics=@([ordered]@{ metric='candidate-orchestration-elapsed'; unit='ms'; threshold='initial-baseline+20%'; comparator='lte'; phase='steady-state'; baseline='initial-baseline'; inputSize='one Warning plus current KnowledgeIndex'; warmup='none'; measurementArtifact=$OutputPath.Replace('\','/'); owner='ES AI governance'; staleWhen='candidate generator or KnowledgeIndex contract changes'; firstRunMs=$sorted[0]; medianMs=$sorted[[int][Math]::Floor(($sorted.Count-1)/2)]; peakMs=$sorted[-1] });
 baseline='This run is the initial baseline; no before/after regression claim.'; sourceHashes=[ordered]@{generator=$generatorHash;knowledgeIndex=$indexHash}; resourceObservations=[ordered]@{warningBytes=([IO.File]::ReadAllBytes($warningFull)).Length;knowledgeIndexBytes=$indexBytes.Length;knowledgeIndexEntryCount=$entryCount;peakMemory='not-measured';actualIo='file-size proxy only'}; nonClaims=@('Does not measure Unity, Runtime, Profiler, Player, IL2CPP or Apply.','Does not prove performance improvement or absence of regression.','Runs the existing candidate generator only and does not write formal Knowledge or Index.')
}
$full=Join-Path $root ($OutputPath.Replace('/',[IO.Path]::DirectorySeparatorChar));[IO.Directory]::CreateDirectory((Split-Path -Parent $full))|Out-Null;[IO.File]::WriteAllText($full,($result|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));$result|ConvertTo-Json -Depth 20
