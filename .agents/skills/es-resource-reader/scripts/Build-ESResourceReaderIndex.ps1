[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [string]$ProjectRoot='.',
    [int]$MaxFiles=256,
    [int]$MaxParallel=4,
    [string]$OutputPath='ES/Output/ResourceReader/resource-index.json'
)
$ErrorActionPreference='Stop'
$scanRoot=(Resolve-Path -LiteralPath $Root).Path
$batch=Join-Path $PSScriptRoot 'Invoke-ESResourceReaderBatch.ps1'
if(-not (Test-Path -LiteralPath $batch -PathType Leaf)){ throw 'Batch reader is missing.' }
$batchJson=& $batch -Root $scanRoot -MaxFiles $MaxFiles -MaxParallel $MaxParallel | Out-String
$batchResult=$batchJson | ConvertFrom-Json -ErrorAction Stop
$items=@(
    foreach($item in @($batchResult.results)) {
        if($null -eq $item){ continue }
        $summary=$item.summary
        $sourcePath=[string]$item.sourcePath
        if($sourcePath.StartsWith($scanRoot,[StringComparison]::OrdinalIgnoreCase)) {
            $sourcePath=$sourcePath.Substring($scanRoot.Length).TrimStart('\','/').Replace('\','/')
        }
        [ordered]@{
            sourcePath=$sourcePath
            sourceSha256=$item.sourceSha256
            detectedFormat=$item.detectedFormat
            parserId=$item.parserId
            parserVersion=if($null -ne $item.parserVersion){$item.parserVersion}else{'1'}
            byteCount=if($null -ne $summary -and $null -ne $summary.sizeBytes){$summary.sizeBytes}else{0}
            entryCount=@($item.entries).Count
            objectCount=if($null -ne $summary -and $null -ne $summary.objectCount){$summary.objectCount}else{0}
            dependencyCount=if($null -ne $summary -and $null -ne $summary.dependencyCount){$summary.dependencyCount}else{0}
            warningCount=@($item.warnings).Count
            errorCount=@($item.errors).Count
            status=if(@($item.errors).Count -gt 0){'error'}else{'ready'}
        }
    }
)
$items=@($items | Sort-Object sourcePath)
$out=[ordered]@{
    schemaVersion=1
    indexId='es-resource-reader.resource-index.v1'
    projectRoot=$scanRoot
    capturedUtc=[DateTime]::UtcNow.ToString('o')
    fileCount=$items.Count
    maxFiles=$MaxFiles
    maxParallel=$MaxParallel
    items=$items
    nonClaims=@('Unity import','runtime loading','network retrieval','release readiness')
}
$destination=Join-Path (Resolve-Path -LiteralPath $ProjectRoot).Path $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination)|Out-Null
$json=$out|ConvertTo-Json -Depth 12
[IO.File]::WriteAllText($destination,$json,(New-Object Text.UTF8Encoding($false)))
$out|ConvertTo-Json -Depth 12
