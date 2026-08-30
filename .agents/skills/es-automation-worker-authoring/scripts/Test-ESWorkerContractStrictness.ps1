[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$validator=Join-Path $PSScriptRoot 'Test-ESWorkerContractPacket.ps1'
$fixtureRoot=Join-Path $root ('ES/Output/.worker-strict-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force|Out-Null
$cases=[Collections.Generic.List[object]]::new()
function Run-Case([string]$Id,[scriptblock]$Body){try{&$Body;[void]$cases.Add([pscustomobject]@{case=$Id;status='passed';finding=$null})}catch{[void]$cases.Add([pscustomobject]@{case=$Id;status='failed';finding=$_.Exception.Message})}}
function Write-Packet([string]$Name,$Packet){$path=Join-Path $fixtureRoot $Name;[IO.File]::WriteAllText($path,($Packet|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));return 'ES/Output/'+(Split-Path -Leaf $fixtureRoot)+'/'+$Name}
function Copy-Packet($Packet){return ($Packet|ConvertTo-Json -Depth 20|ConvertFrom-Json)}
$plan=[ordered]@{taskContract='catalog-audit-contract';steps=@('read','validate')}
$packet=[ordered]@{WorkerId='worker.strict';Version='1';TaskContract='catalog-audit-contract';Plan=$plan;PlanHash='0c81d38f13707bd88d026d3544ef7bb6bc722062fce1013c2478094db3093f60';AllowedRoots=@('ES/Output');ArgumentsSchema='schema-v1';Environment='isolated';SecretsPolicy='deny';Timeout=30;Concurrency=1;Artifacts='ES/Output/worker-report.json';Cancel='cooperative';Recovery='resume';RunRecord='run-record-v1';Owner='Automation owner';StaleWhen='Plan changes'}
Run-Case 'positive' { $path=Write-Packet 'positive.json' $packet;& powershell -NoProfile -File $validator -ProjectRoot $root -PacketPath $path|Out-Null;if($LASTEXITCODE-ne0){throw 'positive packet rejected'} }
Run-Case 'path-traversal-denied' { $x=Copy-Packet $packet;$x.AllowedRoots=@('../Outside');$path=Write-Packet 'traversal.json' $x;$denied=$false;try{& $validator -ProjectRoot $root -PacketPath $path|Out-Null}catch{$denied=$true};if(-not$denied){throw 'path traversal accepted'} }
Run-Case 'artifact-outside-root-denied' { $x=Copy-Packet $packet;$x.Artifacts='Documentation/AIKnowledge/report.json';$path=Write-Packet 'artifact.json' $x;$denied=$false;try{& $validator -ProjectRoot $root -PacketPath $path|Out-Null}catch{$denied=$true};if(-not$denied){throw 'artifact outside root accepted'} }
Run-Case 'plan-hash-tamper-denied' { $x=Copy-Packet $packet;$x.Plan=[ordered]@{taskContract='catalog-audit-contract';steps=@('read','delete')};$path=Write-Packet 'plan-tamper.json' $x;$denied=$false;try{& $validator -ProjectRoot $root -PacketPath $path|Out-Null}catch{$denied=$true};if(-not$denied){throw 'plan hash tamper accepted'} }
Run-Case 'duplicate-root-denied' { $x=Copy-Packet $packet;$x.AllowedRoots=@('ES/Output','ES/Output');$path=Write-Packet 'duplicate-root.json' $x;$denied=$false;try{& $validator -ProjectRoot $root -PacketPath $path|Out-Null}catch{$denied=$true};if(-not$denied){throw 'duplicate roots accepted'} }
$failed=@($cases|Where-Object status -eq 'failed')
try { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction Stop } catch { Write-Verbose ("fixture cleanup failed: {0}" -f $_.Exception.Message) }
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESWorkerContractStrictness';status=if($failed.Count){'failed'}else{'passed'};caseCount=$cases.Count;passedCount=($cases.Count-$failed.Count);failedCount=$failed.Count;cases=@($cases);staticStatus=if($failed.Count){'static-failed'}else{'static-passed'};runtimeStatus='runtime-not-run';evidenceLevel='S1';capturedUtc=[DateTime]::UtcNow.ToString('o');claimsNotProven=@('Worker process runtime','external service behavior','release acceptance')}|ConvertTo-Json -Depth 20
if($failed.Count){exit 1}
