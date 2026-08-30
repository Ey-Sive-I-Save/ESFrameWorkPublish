[CmdletBinding()]
param([string]$ArtifactPath='ES/Output/WebPageStudio/artifacts/github-atlas/index.html',[string]$DesignPacketPath='ES/Output/WebPageStudio/bootstrap/round-06-deep-design-packet-github.json')
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path;$h=[IO.File]::ReadAllText((Join-Path $root $ArtifactPath),[Text.UTF8Encoding]::new($false,$true));$d=Get-Content -Raw -Encoding UTF8 (Join-Path $root $DesignPacketPath)|ConvertFrom-Json;$checks=@(
 [pscustomobject]@{id='no-eval';status=if($h -notmatch '\beval\s*\('){'passed'}else{'blocked'}},
 [pscustomobject]@{id='no-document-write';status=if($h -notmatch 'document\.write\s*\('){'passed'}else{'blocked'}},
 [pscustomobject]@{id='comment-escape';status=if($h -match 'replace\(/\[&<>\]/'){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='remote-query-encoding';status=if($h -match 'encodeURIComponent\('){'passed'}else{'blocked'}},
 [pscustomobject]@{id='no-inline-handler';status=if($h -notmatch '\son[a-z]+\s*='){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='contract-csp';status=if($d.securityContract.cspStrategy -and $d.securityContract.releaseRequirement){'passed'}else{'blocked'}},
 [pscustomobject]@{id='contract-gates';status=if(@($d.securityContract.securityGates).Count -ge 5){'passed'}else{'blocked'}},
 [pscustomobject]@{id='dependency-inventory-alignment';status=if(@($d.securityContract.dependencyInventory.external).Count -eq 0 -and $h -notmatch '<(script|link)[^>]+(?:src|href)="https?://'){ 'passed' }else{'blocked'}}
);$status=if(@($checks|Where-Object status -ne 'passed').Count -eq 0){'passed'}else{'blocked'};[pscustomobject]@{status=$status;checks=$checks;nonClaims=@('does not replace CSP headers','does not prove server-side authorization')}|ConvertTo-Json -Depth 10;if($status -ne 'passed'){exit 1}
