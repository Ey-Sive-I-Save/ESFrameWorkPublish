[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$invoke = Join-Path $root 'ES\Automation\UserSpace\Invoke-ESUserSpace.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('es-userspace-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory((Join-Path $fixture 'ES\AISpace\Public\People')) | Out-Null
    & $invoke -Action Initialize -ProjectRoot $fixture -PersonId 'alice' -DisplayName 'Alice' -Responsibilities 'client' | Out-Null
    $valid = & $invoke -Action Validate -ProjectRoot $fixture | ConvertFrom-Json
    if ($valid.status -ne 'passed' -or $valid.count -ne 1) { throw 'InitializeValidateFailed' }
    & $invoke -Action Update -ProjectRoot $fixture -PersonId 'alice' -ExpectedRevision 1 -BranchStrategy 'topic branch + reviewed merge' | Out-Null
    $updated = & $invoke -Action Validate -ProjectRoot $fixture | ConvertFrom-Json
    if ($updated.registrations[0].revision -ne 2) { throw 'RevisionIncrementFailed' }
    $stale = $false
    try { & $invoke -Action Update -ProjectRoot $fixture -PersonId 'alice' -ExpectedRevision 1 -DisplayName 'stale' | Out-Null } catch { $stale = $_.Exception.Message -match 'RevisionConflict' }
    if (-not $stale) { throw 'StaleRevisionWasAccepted' }
    $registrationPath = Join-Path $fixture 'ES\AISpace\Public\People\alice\registration.json'
    $registration = Get-Content -Raw -Encoding UTF8 $registrationPath | ConvertFrom-Json
    $registration.ownerSubjectHash = ('0' * 64)
    $hashInput = $registration | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $hashInput.contentHash = $null
    $canonical = $hashInput | ConvertTo-Json -Depth 12 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $registration.contentHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    [IO.File]::WriteAllText($registrationPath, ($registration | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    $ownerDenied = $false
    try { & $invoke -Action Update -ProjectRoot $fixture -PersonId 'alice' -ExpectedRevision 2 -DisplayName 'other' -ErrorAction Stop | Out-Null } catch { $ownerDenied = $true }
    if (-not $ownerDenied) { throw 'CrossOwnerValidationWasAccepted' }
    & $invoke -Action Update -ProjectRoot $fixture -PersonId 'alice' -ExpectedRevision 2 -TransferOwnership -ConfirmTeamMember -ConfirmVisibility -ConfirmPreviousOwnerLockout -TakeoverReason 'team reassignment' -DisplayName 'takeover' | Out-Null
    [pscustomobject]@{ status = 'passed'; cases = @('initialize-validate','update-cas','stale-revision-denied','cross-owner-update-denied','explicit-takeover-accepted'); runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Compress
}
finally { if (Test-Path -LiteralPath $fixture) { [IO.Directory]::Delete($fixture, $true) } }
