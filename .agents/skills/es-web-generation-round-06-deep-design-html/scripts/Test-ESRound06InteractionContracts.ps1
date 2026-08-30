[CmdletBinding()]
param(
 [string]$DesignPacketPath='ES/Output/WebPageStudio/bootstrap/round-06-deep-design-packet-github.json',
 [string]$ArtifactPath='ES/Output/WebPageStudio/artifacts/github-atlas/index.html'
)
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
function ReadJ([string]$p){$full=Join-Path $root $p;if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "missing:$p"};[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json}
$d=ReadJ $DesignPacketPath;$html=[IO.File]::ReadAllText((Join-Path $root $ArtifactPath),[Text.UTF8Encoding]::new($false,$true))
$node=(Get-Command node -ErrorAction SilentlyContinue);if($null -eq $node){throw 'node-runtime-required-for-js-syntax-gate'};$scriptMatch=[regex]::Match($html,'(?s)<script>(.*?)</script>');if(-not $scriptMatch.Success){throw 'interaction-script-missing'};$tmp=[IO.Path]::GetTempFileName()+'.js';try{[IO.File]::WriteAllText($tmp,$scriptMatch.Groups[1].Value,[Text.UTF8Encoding]::new($false));& $node.Source '--check' $tmp;if($LASTEXITCODE -ne 0){throw 'interaction-script-syntax-invalid'}}finally{Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue}
$checks=@()
foreach($c in @($d.interactionContracts)){
 $terms=switch([string]$c.action){'search' {@('command-search','loading','empty','error','success','addEventListener','retry')};'branch-select' {@('data-branch','repo-title','addEventListener')};'work-item-open' {@('detail-dialog','showModal','close-detail')};'watch-toggle' {@('watch','toast','addEventListener')};'work-filter' {@('work-filter','status-filter','hidden')};'comment-save' {@('comment-draft','save-comment','notify','data-comment-edit','data-comment-delete','renderComments','atlas-comments')};'status-toggle' {@('toggle-status','notify','ATLAS_SIMULATE_CONFLICT','atlas-revision','status-conflict-detected','atlas-audit')};'list-pagination' {@('page-prev','page-next','page-indicator','addEventListener')};'list-sort' {@('sort-filter','addEventListener')};default {@([string]$c.action)}}
 $missing=@($terms|Where-Object{$html -notmatch [regex]::Escape($_)})
 $checks+=[pscustomobject]@{action=$c.action;status=if($missing.Count -eq 0){'passed'}else{'blocked'};missing=$missing}
}
$global=@('prefers-reduced-motion','aria-live','role="tablist"','role="dialog"','application/json' -replace 'application/json','data-state="loading"')
$globalMissing=@($global|Where-Object{$html -notmatch [regex]::Escape($_)})
if(@($checks|Where-Object status -eq 'blocked').Count -gt 0 -or $globalMissing.Count -gt 0){$checks|ConvertTo-Json -Depth 10;throw 'interaction-contract-replay-failed'}
[pscustomobject]@{status='passed';contractCount=$checks.Count;checks=$checks;globalChecks='passed';runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 20
