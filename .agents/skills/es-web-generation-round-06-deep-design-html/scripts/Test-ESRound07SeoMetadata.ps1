[CmdletBinding()]
param([string]$ArtifactPath='ES/Output/WebPageStudio/artifacts/github-atlas/index.html')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$full=Join-Path $root $ArtifactPath
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'missing-html-artifact'}
$html=[IO.File]::ReadAllText($full,[Text.UTF8Encoding]::new($false,$true))
$jsonLd=[regex]::Match($html,'(?s)<script\s+type="application/ld\+json">(.*?)</script>')
$jsonLdValid=$false
if($jsonLd.Success){try{$obj=$jsonLd.Groups[1].Value|ConvertFrom-Json;$jsonLdValid=([string]$obj.'@type' -eq 'WebApplication')}catch{$jsonLdValid=$false}}
$checks=@(
 [pscustomobject]@{id='description';status=if($html -match '<meta\s+name="description"\s+content="[^"]+"'){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='open-graph';status=if($html -match 'property="og:title"' -and $html -match 'property="og:description"' -and $html -match 'property="og:type"'){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='canonical';status=if($html -match '<link\s+rel="canonical"\s+href="[^"]+"'){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='structured-data';status=if($jsonLdValid){ 'passed' }else{'blocked'}},
 [pscustomobject]@{id='encoding-sanity';status=if($html -notmatch '鈽|宸ヤ|鎸夋|鐘舵|鏈€|浼樻|鍒涘'){ 'passed' }else{'blocked'}}
)
$status=if(@($checks|Where-Object status -ne 'passed').Count -eq 0){'passed'}else{'blocked'}
[pscustomobject]@{status=$status;checks=$checks;artifactPath=$ArtifactPath;nonClaims=@('does not prove search ranking','does not prove browser rendering')}|ConvertTo-Json -Depth 10
if($status -ne 'passed'){exit 1}
