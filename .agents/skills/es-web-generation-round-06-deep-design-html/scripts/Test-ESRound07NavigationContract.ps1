[CmdletBinding()]
param([string]$DesignPacketPath='ES/Output/WebPageStudio/bootstrap/round-06-deep-design-packet-github.json',[string]$ArtifactPath='ES/Output/WebPageStudio/artifacts/github-atlas/index.html')
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$d=Get-Content -Raw -Encoding UTF8 (Join-Path $root $DesignPacketPath)|ConvertFrom-Json;$h=[IO.File]::ReadAllText((Join-Path $root $ArtifactPath),[Text.UTF8Encoding]::new($false,$true));$checks=@()
foreach($r in @($d.navigationContract.routes)){$checks+=[pscustomobject]@{id=[string]$r.id;status=if($h.Contains([string]$r.href.Split('#')[-1])){'passed'}else{'blocked'}}}
$checks+=[pscustomobject]@{id='breadcrumb-contract';status=if(@($d.navigationContract.breadcrumb).Count -ge 3){'passed'}else{'blocked'}}
$checks+=[pscustomobject]@{id='keyboard-order';status=if(@($d.navigationContract.keyboardOrder).Count -ge 5){'passed'}else{'blocked'}}
$status=if(@($checks|Where-Object status -ne 'passed').Count -eq 0){'passed'}else{'blocked'};[pscustomobject]@{status=$status;checks=$checks;nonClaims=@('does not prove browser navigation','does not prove server routing')}|ConvertTo-Json -Depth 10;if($status -ne 'passed'){exit 1}
