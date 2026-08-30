[CmdletBinding()]
param([string]$OutputPath='ES/Output/WebPageStudio/bootstrap/round-byte-ledger-project-management.json')
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$paths=@(
 'ES/Output/WebPageStudio/bootstrap/round-01-intake-project-management.json',
 'ES/Output/WebPageStudio/bootstrap/round-02-focus-project-management.json',
 'ES/Output/WebPageStudio/bootstrap/round-03-task-context-project-management-r3.json',
 'ES/Output/WebPageStudio/bootstrap/round-04-knowledge-route-project-management-r3.json',
 'ES/Output/WebPageStudio/bootstrap/round-05-design-packet-project-management.json',
 'ES/Output/WebPageStudio/bootstrap/round-05-capability-design-project-management.json',
 'ES/Output/WebPageStudio/bootstrap/round-05-5-page-design-project-management.json',
 'ES/Output/WebPageStudio/bootstrap/ai-web-design-task-project-management.json',
 'ES/Output/WebPageStudio/artifacts/project-management/round-1-isolated.html',
 'ES/Output/WebPageStudio/artifacts/project-management/round-2-refactored.html',
 'ES/Output/WebPageStudio/artifacts/project-management/round-3-interaction-motion.html',
 'ES/Output/WebPageStudio/artifacts/project-management/round-4-content-locale.html',
 'ES/Output/WebPageStudio/artifacts/project-management/round-5-style-cohesion.html',
 'ES/Output/WebPageStudio/artifacts/project-management/index.html')
$rows=@();$previous=0
foreach($p in $paths){$f=[IO.Path]::GetFullPath((Join-Path $root $p));if(-not(Test-Path $f)){continue};$i=Get-Item $f;$rows+=[ordered]@{path=$p;bytes=$i.Length;deltaFromPrevious=$i.Length-$previous;sha256=(Get-FileHash $f -Algorithm SHA256).Hash.ToLowerInvariant()};$previous=$i.Length}
$out=[ordered]@{schemaVersion=1;recordType='ESWebRoundByteLedger';generatedUtc=[DateTime]::UtcNow.ToString('o');encoding='UTF-8';entries=$rows;nonClaims=@('byte size does not prove design quality','byte delta does not prove functional coverage','no browser/runtime/network/release claim')}
$full=[IO.Path]::GetFullPath((Join-Path $root $OutputPath));$dir=Split-Path $full -Parent;if(-not(Test-Path $dir)){New-Item $dir -ItemType Directory -Force|Out-Null};[IO.File]::WriteAllText($full,($out|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));$out
