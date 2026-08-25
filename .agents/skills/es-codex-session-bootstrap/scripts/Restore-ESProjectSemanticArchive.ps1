[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectKey,
    [Parameter(Mandatory = $true)][string]$ArchiveId,
    [string]$ProjectRoot = ''
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESProjectSemanticArchive.ps1')
$result = Read-ESSemanticArchive $ProjectKey $ArchiveId
$archive = $result.archive
$drift = [Collections.Generic.List[object]]::new()
$missing = [Collections.Generic.List[string]]::new()
$sourceVerification = 'not-run'
if (-not [string]::IsNullOrWhiteSpace($ProjectRoot) -and (Test-Path -LiteralPath $ProjectRoot -PathType Container)) {
    $sourceVerification = 'checked'
    foreach ($item in @($archive.recentScope)) {
        $relative = ConvertTo-ESArchiveRelativePath ([string]$item.relativePath)
        $full = Join-Path $ProjectRoot ($relative.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { [void]$missing.Add($relative); continue }
        $current = Get-ESArchiveSha256 $full
        if (-not [string]::IsNullOrWhiteSpace([string]$item.contentSha256) -and $current -ne [string]$item.contentSha256) { [void]$drift.Add([ordered]@{ relativePath = $relative; expectedSha256 = [string]$item.contentSha256; currentSha256 = $current }) }
    }
}
[pscustomobject][ordered]@{
    operation = 'restore-semantic-archive'
    storageLocator = $result.storageLocator
    archiveId = [string]$archive.archiveId
    projectKey = [string]$archive.projectKey
    objective = [string]$archive.objective
    state = $archive.state
    importantData = $archive.importantData
    archiveReason = [string]$archive.archiveReason
    recentScope = @($archive.recentScope)
    expectation = $archive.expectation
    restoreMode = 'new-semantic-context'
    conversationRestored = $false
    windowRestored = $false
    contextInheritance = 'partial-semantic'
    sourceVerification = $sourceVerification
    sourceDrift = @($drift.ToArray())
    missingScope = @($missing.ToArray())
    staleStatus = if ($sourceVerification -eq 'checked') { if ($drift.Count -gt 0 -or $missing.Count -gt 0) { 'drifted' } else { 'current-at-check' } } else { 'unknown-unchecked' }
    nextAction = 'Create a new task/session from this semantic packet after rechecking current project facts; do not resume the archived conversation.'
}
