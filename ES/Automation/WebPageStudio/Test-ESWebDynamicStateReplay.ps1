[CmdletBinding()]param([Parameter(Mandatory=$true)][string]$ReplayPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
$f=(Resolve-Path $ReplayPath).Path
if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ReplayPath must remain under project root.'}
$r=Get-Content $f -Raw -Encoding UTF8|ConvertFrom-Json
$issues=[Collections.Generic.List[string]]::new()
if($r.recordType -ne 'WebPageStudioDynamicStateReplay'){$issues.Add('RECORD_TYPE')}
if($r.runtimeStatus -ne 'runtime-not-run'){$issues.Add('RUNTIME_CLAIM')}
$seq=@($r.events|ForEach-Object {[int]$_.sequence});$sorted=@($seq|Sort-Object)
if($seq.Count -lt 4 -or (($seq -join ',') -ne ($sorted -join ',')) -or (($seq|Select-Object -Unique).Count -ne $seq.Count)){$issues.Add('SEQUENCE_INVALID')}
foreach($s in @('idle','loading','success','empty','error')){if(@($r.events|Where-Object state -eq $s).Count -eq 0){$issues.Add("STATE_MISSING_$($s.ToUpperInvariant())")}}
if(@($r.events|Where-Object cache -eq 'serve-stale').Count -eq 0){$issues.Add('STALE_FALLBACK_MISSING')}
$o=[ordered]@{validator='web-dynamic-state-replay';status=if($issues.Count){'blocked'}else{'passed'};findingCount=$issues.Count;findings=@($issues);runtimeStatus=[string]$r.runtimeStatus;nonClaims=@('Offline replay does not prove network/backend behavior')}
$o|ConvertTo-Json -Depth 8
if($issues.Count){exit 1}
