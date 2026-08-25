$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
$newScript = Join-Path $scriptsRoot 'New-ESProjectSemanticArchive.ps1'
$getScript = Join-Path $scriptsRoot 'Get-ESProjectSemanticArchive.ps1'
$listScript = Join-Path $scriptsRoot 'Get-ESProjectSemanticArchives.ps1'
$restoreScript = Join-Path $scriptsRoot 'Restore-ESProjectSemanticArchive.ps1'
. (Join-Path $scriptsRoot 'ESProjectSemanticArchive.ps1')

Describe 'ES semantic archive' {
    It 'creates, lists, reads, and restores a partial semantic packet without window or transcript recovery' {
        $projectKey = 'pester-' + ([Guid]::NewGuid().ToString('N'))
        $archiveId = 'archive-' + ([Guid]::NewGuid().ToString('N'))
        try {
            $created = & $newScript -ProjectKey $projectKey -ArchiveId $archiveId -Objective 'locate editor root cause' -ArchiveReason 'context switch' -ImportantDataJson '{"rootCause":"pending","risk":"editor"}' -StateJson '{"phase":"investigation","verified":false}' -Expected 'find root cause' -GapSummary 'root cause not verified' -GapStatus open -RecentScope 'Assets/Editor/Target.cs'
            $created.conversationRequired | Should Be $false
            $created.windowRestored | Should Be $false
            $listed = & $listScript -ProjectKey $projectKey
            $listed.count | Should Be 1
            $listed.archives[0].archiveId | Should Be $archiveId
            $read = & $getScript -ProjectKey $projectKey -ArchiveId $archiveId
            $read.archive.archiveReason | Should Be 'context switch'
            $restored = & $restoreScript -ProjectKey $projectKey -ArchiveId $archiveId
            $restored.restoreMode | Should Be 'new-semantic-context'
            $restored.conversationRestored | Should Be $false
            $restored.windowRestored | Should Be $false
            $restored.contextInheritance | Should Be 'partial-semantic'
            $restored.sourceVerification | Should Be 'not-run'
            $restored.staleStatus | Should Be 'unknown-unchecked'
            (Get-Content -LiteralPath (Get-ESSemanticArchivePath $projectKey $archiveId) -Raw -Encoding UTF8) | Should Not Match 'C:'
            (Get-Content -LiteralPath (Get-ESSemanticArchivePath $projectKey $archiveId) -Raw -Encoding UTF8) | Should Not Match 'sourceAbsolutePath'
        }
        finally {
            $path = Get-ESSemanticArchivePath $projectKey $archiveId
            if (Test-Path -LiteralPath $path -PathType Leaf) { [IO.File]::Delete($path) }
        }
    }

    It 'rejects absolute path content and create collisions' {
        $projectKey = 'pester-' + ([Guid]::NewGuid().ToString('N'))
        $archiveId = 'archive-' + ([Guid]::NewGuid().ToString('N'))
        try {
            { & $newScript -ProjectKey $projectKey -ArchiveId $archiveId -Objective 'x' -ArchiveReason 'y' -ImportantDataJson "{`"bad`":`"C:\\secret`"}" } | Should Throw
            & $newScript -ProjectKey $projectKey -ArchiveId $archiveId -Objective 'x' -ArchiveReason 'y' | Out-Null
            { & $newScript -ProjectKey $projectKey -ArchiveId $archiveId -Objective 'x' -ArchiveReason 'y' } | Should Throw
        }
        finally {
            $path = Get-ESSemanticArchivePath $projectKey $archiveId
            if (Test-Path -LiteralPath $path -PathType Leaf) { [IO.File]::Delete($path) }
        }
    }

    It 'reports missing scope when explicitly checking a current project root' {
        $projectKey = 'pester-' + ([Guid]::NewGuid().ToString('N'))
        $archiveId = 'archive-' + ([Guid]::NewGuid().ToString('N'))
        try {
            & $newScript -ProjectKey $projectKey -ArchiveId $archiveId -Objective 'x' -ArchiveReason 'y' -RecentScope 'does-not-exist/file.txt' | Out-Null
            $restored = & $restoreScript -ProjectKey $projectKey -ArchiveId $archiveId -ProjectRoot $TestDrive
            $restored.sourceVerification | Should Be 'checked'
            $restored.staleStatus | Should Be 'drifted'
            (@($restored.missingScope) -contains 'does-not-exist/file.txt') | Should Be $true
        }
        finally {
            $path = Get-ESSemanticArchivePath $projectKey $archiveId
            if (Test-Path -LiteralPath $path -PathType Leaf) { [IO.File]::Delete($path) }
        }
    }
}
