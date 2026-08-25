[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectKey,
    [ValidateRange(1,256)][int]$Limit = 100
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESProjectSemanticArchive.ps1')
Assert-ESSemanticArchiveKey $ProjectKey 'ProjectKey'
$root = Join-Path (Get-ESSemanticArchiveRoot) $ProjectKey
$rows = [Collections.Generic.List[object]]::new()
if (Test-Path -LiteralPath $root -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $root -Filter '*.json' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First $Limit)) {
        try {
            $read = Read-ESSemanticArchive $ProjectKey ([IO.Path]::GetFileNameWithoutExtension($file.Name))
            $a = $read.archive
            [void]$rows.Add([ordered]@{
                archiveId = [string]$a.archiveId
                projectKey = [string]$a.projectKey
                objective = [string]$a.objective
                archiveReason = [string]$a.archiveReason
                gapStatus = [string]$a.expectation.gapStatus
                createdUtc = [string]$a.createdUtc
                recentScopeCount = @($a.recentScope).Count
                storageLocator = $read.storageLocator
            })
        } catch { continue }
    }
}
[pscustomobject][ordered]@{
    operation = 'list-semantic-archives'
    projectKey = $ProjectKey
    storageRoot = 'ESFrameworkSemanticArchives/{projectKey}'
    count = $rows.Count
    archives = @($rows.ToArray())
}
