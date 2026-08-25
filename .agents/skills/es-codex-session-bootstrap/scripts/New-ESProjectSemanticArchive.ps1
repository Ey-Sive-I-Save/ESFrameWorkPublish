[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectKey,
    [Parameter(Mandatory = $true)][string]$Objective,
    [Parameter(Mandatory = $true)][string]$ArchiveReason,
    [string]$ResponsibilityKey = '',
    [string]$ImportantDataJson = '{}',
    [string]$StateJson = '{}',
    [string[]]$Expected = @(),
    [string]$GapSummary = 'gap not provided',
    [string]$GapStatus = 'unknown',
    [string[]]$RecentScope = @(),
    [string]$ProjectRoot = '',
    [string]$ArchiveId = ''
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESProjectSemanticArchive.ps1')
Assert-ESSemanticArchiveKey $ProjectKey 'ProjectKey'
if ([string]::IsNullOrWhiteSpace($ArchiveId)) { $ArchiveId = [Guid]::NewGuid().ToString('N') }
Assert-ESSemanticArchiveKey $ArchiveId 'ArchiveId'
if ([string]::IsNullOrWhiteSpace($Objective) -or $Objective.Length -gt 8000) { throw 'Objective must be 1-8000 characters.' }
if ([string]::IsNullOrWhiteSpace($ArchiveReason) -or $ArchiveReason.Length -gt 1000) { throw 'ArchiveReason must be 1-1000 characters.' }
if ($GapStatus -notin @('unknown', 'open', 'partial', 'aligned', 'blocked')) { throw 'GapStatus is invalid.' }
try {
    $importantData = $ImportantDataJson | ConvertFrom-Json
    $state = $StateJson | ConvertFrom-Json
}
catch {
    throw 'ImportantDataJson and StateJson must be valid JSON.'
}
if ($importantData -isnot [pscustomobject] -or $state -isnot [pscustomobject]) { throw 'ImportantDataJson and StateJson must be JSON objects.' }
Assert-ESArchiveNoAbsolutePaths $importantData 'importantData'
Assert-ESArchiveNoAbsolutePaths $state 'state'
$observed = Get-ESRecentWorktreeScope $ProjectRoot
$scope = [Collections.Generic.List[object]]::new()
foreach ($item in @($observed.items)) { [void]$scope.Add($item) }
foreach ($candidate in @($RecentScope)) {
    $relative = ConvertTo-ESArchiveRelativePath $candidate
    if (@($scope | Where-Object relativePath -eq $relative).Count -eq 0) { [void]$scope.Add([ordered]@{ relativePath = $relative; changeKind = 'observed' }) }
    if ($scope.Count -ge 64) { break }
}
$evidenceSource = $observed.source
if ($observed.source -eq 'worktree-observed' -and $RecentScope.Count -gt 0) { $evidenceSource = 'mixed' }
$archive = [ordered]@{
    schemaVersion = 1
    archiveKind = 'es-semantic-archive'
    archiveId = $ArchiveId
    projectKey = $ProjectKey
    responsibilityKey = $ResponsibilityKey
    objective = $Objective
    state = $state
    importantData = $importantData
    archiveReason = $ArchiveReason
    recentScope = @($scope.ToArray() | Select-Object -First 64)
    expectation = [ordered]@{ expected = @($Expected | Select-Object -First 32); gapSummary = $GapSummary; gapStatus = $GapStatus }
    evidence = [ordered]@{ branch = $observed.branch; headSha = $observed.headSha; source = $evidenceSource }
    createdUtc = [DateTime]::UtcNow.ToString('o')
    updatedUtc = [DateTime]::UtcNow.ToString('o')
}
Assert-ESArchiveNoAbsolutePaths $archive
$path = Get-ESSemanticArchivePath $ProjectKey $ArchiveId
Write-ESSemanticArchiveCreateOnly $path $archive
[pscustomobject][ordered]@{
    operation = 'create-semantic-archive'
    archiveId = $ArchiveId
    projectKey = $ProjectKey
    storageLocator = "$ProjectKey/$ArchiveId.json"
    archivePath = $path
    recentScopeCount = @($archive.recentScope).Count
    gapStatus = [string]$archive.expectation.gapStatus
    conversationRequired = $false
    windowRestored = $false
}
