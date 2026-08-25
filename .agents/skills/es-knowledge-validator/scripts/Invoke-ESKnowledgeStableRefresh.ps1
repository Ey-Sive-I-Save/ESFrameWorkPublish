[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$PlanPath = 'ES/Output/KnowledgeValidation/refresh-plan.json',
    [string]$OutputPath = 'ES/Output/KnowledgeValidation/stable-refresh-receipt.json',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$planFile = Join-Path $root ($PlanPath.Replace('/', '\'))
if (-not (Test-Path -LiteralPath $planFile -PathType Leaf)) { throw "Refresh plan not found: $PlanPath" }
$plan = Get-Content -LiteralPath $planFile -Raw -Encoding UTF8 | ConvertFrom-Json
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$changes = [Collections.Generic.List[object]]::new()
$entryTargets = @($plan.findings | Where-Object { $_.snapshotStable -eq $true -and $_.action -eq 'review-and-refresh-source-ref' } | Group-Object entry | ForEach-Object { $_.Group[0] })
$staleAtApply = [Collections.Generic.List[string]]::new()

function Get-Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-ContentHash([string[]]$hashes) {
    $joined = (@($hashes | Sort-Object -CaseSensitive) -join '')
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($joined)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

foreach ($finding in $entryTargets) {
    $entryRelative = [string]$finding.entry
    $entryPath = Join-Path $root $entryRelative.Replace('/', '\')
    if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf)) { continue }
    $entryFindings = @($plan.findings | Where-Object { $_.entry -eq $entryRelative -and $_.snapshotStable -eq $true })
    $sourceChangedSincePlan = $false
    foreach ($planned in $entryFindings) {
        $plannedSource = Join-Path $root ([string]$planned.source).Replace('/', '\')
        if ((Test-Path -LiteralPath $plannedSource -PathType Leaf) -and (Get-Hash $plannedSource) -cne [string]$planned.currentHash) { $sourceChangedSincePlan = $true }
    }
    if ($sourceChangedSincePlan) { $staleAtApply.Add($entryRelative); continue }
    $text = [IO.File]::ReadAllText($entryPath, $strictUtf8)
    $sourcePattern = '(?m)^-\s+(`?[^\(\r\n]+?`?)\s+\((?:`)?(?:[0-9a-f]{64}|\$current)(?:`)?\)'
    $hashes = [Collections.Generic.List[string]]::new()
    $updated = [regex]::Replace($text, $sourcePattern, {
        param($match)
        $relative = $match.Groups[1].Value.Trim('`')
        $sourcePath = Join-Path $root ($relative.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { return $match.Value }
        $current = Get-Hash $sourcePath
        $hashes.Add($current)
        return ('- `' + $relative.Trim('`', ' ') + '` (`' + $current + '`)')
    })
    if ($hashes.Count -eq 0) { continue }
    $newContentHash = Get-ContentHash $hashes.ToArray()
    $updated = [regex]::Replace($updated, '(?m)^(`ContentHash`\s*:\s*`)[0-9a-f]{64}(`\s*$)', "`${1}$newContentHash`${2}", 1)
    if ($updated -ceq $text) { continue }
    $changes.Add([pscustomobject]@{ entry = $entryRelative; contentHash = $newContentHash; sourceCount = $hashes.Count; applied = [bool]$Apply })
    if ($Apply -and $PSCmdlet.ShouldProcess($entryRelative, 'Refresh stable SourceRef hashes and ContentHash')) {
        $temporary = "$entryPath.tmp-$([Guid]::NewGuid().ToString('N'))"
        try { [IO.File]::WriteAllText($temporary, $updated, $strictUtf8); Move-Item -LiteralPath $temporary -Destination $entryPath -Force }
        finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
        $indexPath = Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
        $indexText = [IO.File]::ReadAllText($indexPath, $strictUtf8)
        $escapedFile = [regex]::Escape($entryRelative.Replace('Documentation/AIKnowledge/', ''))
        $indexPattern = "(?ms)(^\s*- knowledgeId:.*?^\s+file: $escapedFile\s*$.*?^\s+contentHash:\s*)[0-9a-f]{64}"
        $updatedIndex = [regex]::Replace($indexText, $indexPattern, "`${1}$newContentHash", 1)
        if ($updatedIndex -cne $indexText) {
            $indexTemporary = "$indexPath.tmp-$([Guid]::NewGuid().ToString('N'))"
            try { [IO.File]::WriteAllText($indexTemporary, $updatedIndex, $strictUtf8); Move-Item -LiteralPath $indexTemporary -Destination $indexPath -Force }
            finally { if (Test-Path -LiteralPath $indexTemporary) { Remove-Item -LiteralPath $indexTemporary -Force } }
        }
    }
}

$output = Join-Path $root ($OutputPath.Replace('/', '\'))
$receipt = [ordered]@{ schemaVersion = 1; toolId = 'es-knowledge-stable-refresh'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('o'); mutatesSources = $false; mutatesKnowledge = [bool]$Apply; mode = if ($Apply) { 'apply-stable-only' } else { 'preview' }; sourcePlan = $PlanPath; planHash = if ($plan.PSObject.Properties['planHash']) { [string]$plan.planHash } else { '' }; staleAtApplyCount = $staleAtApply.Count; staleAtApply = @($staleAtApply); changeCount = $changes.Count; changes = $changes }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
[IO.File]::WriteAllText($output, ($receipt | ConvertTo-Json -Depth 8), $strictUtf8)
$receipt | ConvertTo-Json -Depth 8
