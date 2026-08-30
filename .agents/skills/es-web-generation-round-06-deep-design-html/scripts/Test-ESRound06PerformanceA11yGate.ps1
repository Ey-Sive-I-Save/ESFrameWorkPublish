[CmdletBinding()]
param(
 [string]$DesignPacketPath='ES/Output/WebPageStudio/bootstrap/round-06-deep-design-packet-github.json',
 [string]$ArtifactPath='ES/Output/WebPageStudio/artifacts/github-atlas/index.html'
)
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$d=Get-Content -Raw -Encoding UTF8 (Join-Path $root $DesignPacketPath)|ConvertFrom-Json
$html=[IO.File]::ReadAllText((Join-Path $root $ArtifactPath),[Text.UTF8Encoding]::new($false,$true));$bytes=(Get-Item (Join-Path $root $ArtifactPath)).Length
$p=$d.performanceBudget;$checks=@()
$checks+=[pscustomobject]@{id='html-bytes';status=if($bytes -le ($p.cssBytesMax+$p.jsBytesMax+12000)){'passed'}else{'blocked'};actual=$bytes;limit=($p.cssBytesMax+$p.jsBytesMax+12000)}
$checks+=[pscustomobject]@{id='dom-nodes';status=if(([regex]::Matches($html,'<[^!/][^>]*>')).Count -le [int]$p.domNodesMax){'passed'}else{'blocked'};actual=([regex]::Matches($html,'<[^!/][^>]*>')).Count;limit=$p.domNodesMax}
$checks+=[pscustomobject]@{id='document-language';status=if($html -match '<html[^>]+lang='){ 'passed' }else{'blocked'}}
$checks+=[pscustomobject]@{id='landmarks';status=if($html -match '<header' -and $html -match '<main' -and $html -match '<aside'){ 'passed' }else{'blocked'}}
$checks+=[pscustomobject]@{id='focus-and-motion';status=if($html -match 'focus-visible' -and $html -match 'prefers-reduced-motion'){ 'passed' }else{'blocked'}}
$checks+=[pscustomobject]@{id='live-and-dialog';status=if($html -match 'aria-live' -and $html -match 'aria-modal'){ 'passed' }else{'blocked'}}
$checks+=[pscustomobject]@{id='button-names';status=if(@([regex]::Matches($html,'<button[^>]*>(\s*)</button>')).Count -eq 0){'passed'}else{'blocked'}}
if(@($checks|Where-Object status -eq 'blocked').Count -gt 0){$checks|ConvertTo-Json -Depth 10;throw 'performance-a11y-gate-failed'}
[pscustomobject]@{status='passed';checks=$checks;runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 20
