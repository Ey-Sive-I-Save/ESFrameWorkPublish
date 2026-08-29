[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [int]$DurationSeconds = 60,
    [int]$DebounceMilliseconds = 150,
    [int]$QueueLimit = 32
)
$ErrorActionPreference='Stop'
if($DurationSeconds -lt 1 -or $DurationSeconds -gt 3600){throw 'DURATION_OUT_OF_RANGE'}
if($QueueLimit -lt 1 -or $QueueLimit -gt 1024){throw 'QUEUE_LIMIT_OUT_OF_RANGE'}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$warnings=Join-Path $root 'Assets/Plugins/ES/AIWarnings'
if(!(Test-Path -LiteralPath $warnings -PathType Container)){throw 'AIWARNINGS_ROOT_NOT_FOUND'}
$observer=Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/Invoke-ESAIWarningSaveObserver.ps1'
$watcher=[IO.FileSystemWatcher]::new($warnings,'*.md');$watcher.IncludeSubdirectories=$true;$watcher.NotifyFilter=[IO.NotifyFilters]::LastWrite
$queue=[Collections.Concurrent.ConcurrentQueue[string]]::new();$queued=[Collections.Concurrent.ConcurrentDictionary[string,bool]]::new();$handler={param($s,$e);if($queued.Count -lt $QueueLimit -and $queued.TryAdd($e.FullPath,$true)){$queue.Enqueue($e.FullPath)}}
$sub=Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $handler
$watcher.EnableRaisingEvents=$true;$deadline=[DateTime]::UtcNow.AddSeconds($DurationSeconds);$seen=0
try{while([DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100;$path=$null;while($queue.TryDequeue([ref]$path)){$discard=$false;$null=$queued.TryRemove($path,[ref]$discard);if(!(Test-Path -LiteralPath $path -PathType Leaf)){continue};$relative=$path.Substring($root.Length).TrimStart('\','/').Replace('\','/');& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $observer -ProjectRoot $root -WarningPath $relative -QueueLimit $QueueLimit -DebounceMilliseconds $DebounceMilliseconds | Out-Null;$seen++}}}
finally{$watcher.EnableRaisingEvents=$false;Unregister-Event -SubscriptionId $sub.Id -ErrorAction SilentlyContinue;if($sub.Action -and $sub.Action.Id){Remove-Job -Id $sub.Action.Id -Force -ErrorAction SilentlyContinue};$watcher.Dispose()}
[pscustomobject]@{schemaVersion=1;recordType='AIWarningSaveObserverHostReceipt';status='completed';durationSeconds=$DurationSeconds;eventsObserved=$seen;candidateOnly=$true;transactionExecuted=$false;formalRegistration='not-run';runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 10
