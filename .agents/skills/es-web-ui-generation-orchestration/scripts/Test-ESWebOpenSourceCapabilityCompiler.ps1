[CmdletBinding()]
param([string]$ProfilePath,[string]$ManifestPath)
$ErrorActionPreference='Stop'
$here=$PSScriptRoot
if(!$ProfilePath){$ProfilePath=Join-Path $here '..\references\open-source-capability-profile.json'}
if(!$ManifestPath){$ManifestPath=Join-Path $here '..\references\open-source-source-manifest.json'}
$compiler=Join-Path $here 'Invoke-ESWebOpenSourceCapabilityCompiler.ps1'
$positive=& $compiler -ProfilePath $ProfilePath -ManifestPath $ManifestPath | ConvertFrom-Json
if($positive.status -ne 'accepted'){throw "positive compiler case failed: $($positive.blockedReasons -join ',')"}
$tmp=Join-Path ([IO.Path]::GetTempPath()) ("es-open-source-manifest-negative-{0}.json" -f [guid]::NewGuid())
$raw=Get-Content -Raw -Encoding UTF8 $ManifestPath
$m=[regex]::Match($raw,'"sourceSha256":"[0-9a-f]{64}"')
if(!$m.Success){throw 'manifest has no sourceSha256 field'}
$raw=$raw.Remove($m.Index,$m.Length).Insert($m.Index,'"sourceSha256":"0000000000000000000000000000000000000000000000000000000000000000"')
[IO.File]::WriteAllText($tmp,$raw,[Text.UTF8Encoding]::new($false))
$negative=& $compiler -ProfilePath $ProfilePath -ManifestPath $tmp 2>$null | ConvertFrom-Json
if($negative.status -ne 'blocked' -or @($negative.blockedReasons) -notcontains 'source-hash-mismatch:nextjs'){throw 'negative compiler case did not detect source hash drift'}
[ordered]@{status='passed';positive='accepted';negative='blocked';negativeReason='source-hash-mismatch:nextjs';frameworkCount=@($positive.frameworks).Count}|ConvertTo-Json
