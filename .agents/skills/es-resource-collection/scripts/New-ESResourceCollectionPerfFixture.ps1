[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet(60,256,1024)][int]$Count,
    [string]$Root='ES/Output/ResourceCollection/PerfFixtures',
    [int]$BytesPerFile=4096,
    [ValidateSet('txt','json','md','yaml','csv','tsv','html')][string]$Format='txt'
)
$ErrorActionPreference='Stop'
if($BytesPerFile -lt 64 -or $BytesPerFile -gt 1048576){throw 'BytesPerFile must be between 64 and 1048576'}
$target=Join-Path (Get-Location) (Join-Path $Root ("batch-{0}-{1}B-{2}" -f $Count,$BytesPerFile,$Format))
if(!(Test-Path -LiteralPath $target)){New-Item -ItemType Directory -Path $target -Force|Out-Null}
$seed=('ESResourceCollectionFixture|' + $BytesPerFile)
$payload=New-Object Text.StringBuilder
while($payload.Length -lt $BytesPerFile){[void]$payload.Append($seed);[void]$payload.Append("`n")}
$text=$payload.ToString().Substring(0,$BytesPerFile)
if($Format -eq 'json'){$text='{"fixture":true,"index":0,"padding":"' + ('x' * [Math]::Max(1,$BytesPerFile-40)) + '"}'}
elseif($Format -eq 'md'){$text=(('# ES Resource Fixture' + "`n`n" + $text)).Substring(0,$BytesPerFile)}
elseif($Format -eq 'yaml'){$text=("fixture: true`nindex: 0`npadding: " + $text).Substring(0,$BytesPerFile)}
elseif($Format -eq 'csv'){$text=("index,padding`n0," + $text).Substring(0,$BytesPerFile)}
elseif($Format -eq 'tsv'){$text=("index`tpadding`n0`t" + $text).Substring(0,$BytesPerFile)}
elseif($Format -eq 'html'){$text=("<html><body><p>" + $text + "</p></body></html>").Substring(0,$BytesPerFile)}
for($i=0;$i -lt $Count;$i++){
    $path=Join-Path $target ("resource-{0:D5}.{1}" -f $i,$Format)
    if(!(Test-Path -LiteralPath $path)){[IO.File]::WriteAllText($path,$text,(New-Object Text.UTF8Encoding($false)))}
}
[ordered]@{fixtureId='es-resource-collection.perf-fixture.v1';root=$target;count=$Count;bytesPerFile=$BytesPerFile;format=$Format;totalBytes=([int64]$Count*$BytesPerFile);deterministic=$true;nonClaims=@('Unity import','runtime loading','release')}|ConvertTo-Json -Depth 5
