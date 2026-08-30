[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SitePath,
    [switch]$Open,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $SitePath).Path
$previewExt = @('.png','.jpg','.jpeg','.gif','.bmp','.tga','.webp','.fbx','.obj','.glb','.gltf','.wav','.mp3','.ogg')
$candidate = Get-ChildItem -LiteralPath $resolved -File -Recurse |
    Where-Object { $previewExt -contains $_.Extension.ToLowerInvariant() } |
    Sort-Object FullName |
    Select-Object -First 1

$receipt = [ordered]@{
    schemaVersion = 1
    sitePath = $resolved
    previewAttempted = [bool]$Open
    previewPath = if ($candidate) { $candidate.FullName } else { $null }
    result = if (-not $candidate) { 'preview-unavailable' } elseif (-not $Open) { 'candidate-listed' } else { 'launch-requested' }
    hostAction = if ($Open -and $candidate) { 'Start-Process' } else { 'none' }
    visualSuccess = $false
    nonClaims = @('No visual success is claimed; host/window acknowledgement is outside this script.')
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
}

if ($Open -and $candidate) {
    Start-Process -FilePath $candidate.FullName | Out-Null
}

$json = $receipt | ConvertTo-Json -Depth 5
if ($ReportPath) {
    $parent = Split-Path -Parent $ReportPath
    if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $reportFull = [System.IO.Path]::GetFullPath($ReportPath)
    [System.IO.File]::WriteAllText($reportFull, $json, (New-Object System.Text.UTF8Encoding($false)))
}
$json
