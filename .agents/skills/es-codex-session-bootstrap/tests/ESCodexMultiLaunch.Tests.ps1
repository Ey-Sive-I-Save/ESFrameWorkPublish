$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$script = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Invoke-ESCodexMultiLaunch.ps1'
$identityLines = foreach ($relative in @('AGENTS.md','ProjectSettings/ProjectVersion.txt')) {
    $full = Join-Path $projectRoot $relative
    $bytes = [IO.File]::ReadAllBytes($full)
    $h = [Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($h.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() } finally { $h.Dispose() }
    "$relative|$hash"
}
$identitySha = [Security.Cryptography.SHA256]::Create()
try { $projectIdentityFingerprint = ([BitConverter]::ToString($identitySha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($identityLines | Sort-Object) -join "`n")))).Replace('-','').ToLowerInvariant() } finally { $identitySha.Dispose() }

Describe 'ESCodex multi-launch planner' {
    It 'rejects duplicate task keys without launching' {
        $tmp = New-TemporaryFile
        try {
            @{ batchId='test'; launches=@(
                @{taskKey='same'; responsibilityKey='one'; taskPrompt='a'; mode='New'},
                @{taskKey='same'; responsibilityKey='two'; taskPrompt='b'; mode='New'}
            ) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
            $result = & $script -PlanPath $tmp -ProjectPath $projectRoot | ConvertFrom-Json
            $result.failedCount | Should Be 1
            $result.launches[1].reasonCode | Should Be 'DuplicateTaskKey'
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }

    It 'keeps a mixed New/Handoff plan bounded in dry-run' {
        $tmp = New-TemporaryFile
        try {
            @{ batchId='mixed'; launches=@(
                @{taskKey='new'; responsibilityKey='one'; taskPrompt='a'; mode='New'},
                @{taskKey='handoff'; responsibilityKey='two'; taskPrompt='b'; mode='Handoff'}
            ) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
            $result = & $script -PlanPath $tmp -ProjectPath $projectRoot -MaxParallel 1 | ConvertFrom-Json
            $result.operation | Should Be 'MultiLaunch'
            $result.partialFailure | Should Be $true
            $result.concurrencyNote | Should Match 'schedule waves'
            $result.waveCount | Should Be 2
            $result.launches[0].wave | Should Be 1
            $result.launches[1].wave | Should Be 2
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }

    It 'projects and preserves the current project identity fingerprint in dry-run' {
        $tmp = New-TemporaryFile
        try {
            @{ batchId='identity'; projectIdentityFingerprint=$projectIdentityFingerprint; launches=@(
                @{taskKey='identity'; responsibilityKey='one'; taskPrompt='a'; mode='New'}
            ) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
            $result = & $script -PlanPath $tmp -ProjectPath $projectRoot | ConvertFrom-Json
            $result.projectIdentityFingerprint | Should Be $projectIdentityFingerprint
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }

    It 'rejects a mismatched project identity before launch' {
        $tmp = New-TemporaryFile
        try {
            @{ batchId='identity-mismatch'; projectIdentityFingerprint=('0' * 64); launches=@(
                @{taskKey='identity'; responsibilityKey='one'; taskPrompt='a'; mode='New'}
            ) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
            $thrown = $false
            try { & $script -PlanPath $tmp -ProjectPath $projectRoot -Launch | Out-Null } catch { $thrown = $_.Exception.Message -like '*Project identity fingerprint does not match*' }
            $thrown | Should Be $true
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
}
