[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$HtmlPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [ValidateRange(0,10)][int]$WarmupRuns = 1,
    [ValidateRange(1,20)][int]$SampleRuns = 5
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
$html=(Resolve-Path $HtmlPath).Path
if(-not $html.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'HtmlPath must remain under project root.'}
$out=Join-Path $root $OutputPath
New-Item -ItemType Directory (Split-Path $out) -Force|Out-Null
$measure='ES/Automation/WebPageStudio/Measure-ESWebPageStudioPerformance.ps1'
$all=@(); $warmup=@()
function Invoke-Probe([int]$Index,[bool]$IsWarmup){
  $tag=if($IsWarmup){'warmup'}else{'sample'}
  $path=Join-Path (Split-Path $out) ("performance-$tag-$Index.json")
  $relative=$path.Substring($root.Length)
  & $measure -HtmlPath $html -OutputPath $relative *> (Join-Path (Split-Path $out) ("performance-$tag-$Index.log"))
  if(-not (Test-Path $path)){throw "Performance probe did not produce $path"}
  return (Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json)
}
for($i=1;$i -le $WarmupRuns;$i++){ $warmup += Invoke-Probe $i $true }
for($i=1;$i -le $SampleRuns;$i++){ $all += Invoke-Probe $i $false }
function Percentile([double[]]$Values,[double]$P){
  if(-not $Values -or $Values.Count -eq 0){return $null}
  $s=@($Values|Sort-Object);$idx=[Math]::Max(0,[Math]::Ceiling($P*$s.Count)-1);return [double]$s[$idx]
}
$wall=@($all|ForEach-Object{[double]$_.probeWallClockMs})
$cls=@($all|ForEach-Object{if($null -ne $_.metrics.cls){[double]$_.metrics.cls}})
$js=@($all|ForEach-Object{[double]$_.resourceBytes.jsBytes})
$font=@($all|ForEach-Object{[double]$_.resourceBytes.fontBytes})
$image=@($all|ForEach-Object{[double]$_.resourceBytes.imageBytes})
$lcp=@($all|ForEach-Object{if($null -ne $_.metrics.lcpMs){[double]$_.metrics.lcpMs}})
$inp=@($all|ForEach-Object{if($null -ne $_.metrics.inpMs){[double]$_.metrics.inpMs}})
$edge=($all|Select-Object -First 1).browser
$metrics=[ordered]@{wallClockMs=[ordered]@{p50=(Percentile $wall .5);p75=(Percentile $wall .75);samples=$wall};lcpMs=[ordered]@{p75=(Percentile $lcp .75);samples=$lcp};inpMs=[ordered]@{p75=(Percentile $inp .75);samples=$inp};cls=[ordered]@{p75=(Percentile $cls .75);samples=$cls};resourceBytes=[ordered]@{jsBytes=[ordered]@{p75=(Percentile $js .75);samples=$js};fontBytes=[ordered]@{p75=(Percentile $font .75);samples=$font};imageBytes=[ordered]@{p75=(Percentile $image .75);samples=$image}}}
$missing=@();if($lcp.Count -eq 0){$missing+='lcpMs'};if($inp.Count -eq 0){$missing+='inpMs'}
$o=[ordered]@{schemaVersion=1;recordType='WebPageStudioPerformanceBaseline';status='review';runtimeStatus='runtime-passed';browser=$edge;htmlPath=$html;htmlSha256=(Get-FileHash $html -Algorithm SHA256).Hash.ToLowerInvariant();warmupRuns=$WarmupRuns;sampleRuns=$SampleRuns;metrics=$metrics;measurementMode='repeated-disposable-instrumented-file-preview';budgetReference='ES/Automation/WebPageStudio/performance-budget.yaml';missingMetrics=$missing;nonClaims=@('wall-clock samples are not LCP/INP','no Lighthouse or field p75 evidence','file preview does not prove staging/deployment budgets');sampleReceipts=@($all|ForEach-Object{$_.screenshotHash})}
[IO.File]::WriteAllText($out,($o|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$o|ConvertTo-Json -Depth 12
