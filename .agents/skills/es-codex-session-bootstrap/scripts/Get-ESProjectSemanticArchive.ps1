[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectKey,
    [Parameter(Mandatory = $true)][string]$ArchiveId
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESProjectSemanticArchive.ps1')
$result = Read-ESSemanticArchive $ProjectKey $ArchiveId
[pscustomobject][ordered]@{ operation = 'read-semantic-archive'; storageLocator = $result.storageLocator; archive = $result.archive }
