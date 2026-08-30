Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\TaskCollaboration\ESTaskCollaborationContracts.psm1') -Force

function Get-ESWebUiStoreMutexName([string]$Path) {
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes(([IO.Path]::GetFullPath($Path)).ToLowerInvariant())
    $sha=[Security.Cryptography.SHA256]::Create(); try { $hex=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    'ESFramework_WebUiLeaseStore_' + $hex
}
function Invoke-ESWebUiStoreMutex([string]$Path,[scriptblock]$Body) {
    $mutex=[Threading.Mutex]::new($false,(Get-ESWebUiStoreMutexName $Path));$acquired=$false
    try { $acquired=$mutex.WaitOne([TimeSpan]::FromSeconds(15)); if(-not $acquired){throw 'WEB_UI_LEASE_STORE_LOCK_TIMEOUT'}; & $Body }
    finally { if($acquired){$mutex.ReleaseMutex()|Out-Null};$mutex.Dispose() }
}
function Assert-ESWebUiStorePath([string]$StorePath) {
    if([string]::IsNullOrWhiteSpace($StorePath) -or -not [IO.Path]::IsPathRooted($StorePath)){throw 'WEB_UI_LEASE_STORE_PATH_MUST_BE_ABSOLUTE'}
    $full=[IO.Path]::GetFullPath($StorePath);$parent=Split-Path -Parent $full
    if(-not (Test-Path -LiteralPath $parent -PathType Container)){throw 'WEB_UI_LEASE_STORE_PARENT_MISSING'}
    $item=Get-Item -LiteralPath $parent -Force;if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint)-ne 0){throw 'WEB_UI_LEASE_STORE_REPARSE_PARENT'}
    if(Test-Path -LiteralPath $full -PathType Leaf){$storeItem=Get-Item -LiteralPath $full -Force;if(($storeItem.Attributes -band [IO.FileAttributes]::ReparsePoint)-ne 0){throw 'WEB_UI_LEASE_STORE_REPARSE_FILE'}}
    $full
}
function Read-ESWebUiPersistentLeaseState([string]$StorePath) {
    $full=Assert-ESWebUiStorePath $StorePath
    if(-not (Test-Path -LiteralPath $full -PathType Leaf)){return [ordered]@{schemaVersion=1;recordType='WebPageStudioPersistentLeaseState';revision=0;claims=@{};events=@()}}
    $text=[IO.File]::ReadAllText($full,[Text.UTF8Encoding]::new($false,$true));$state=$text|ConvertFrom-Json
    if([int]$state.schemaVersion -ne 1 -or [string]$state.recordType -cne 'WebPageStudioPersistentLeaseState'){throw 'WEB_UI_LEASE_STATE_IDENTITY_INVALID'}
    $state
}
function Write-ESWebUiPersistentLeaseState([string]$StorePath,$State) {
    $full=Assert-ESWebUiStorePath $StorePath;$tmp=$full+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    $json=$State|ConvertTo-Json -Depth 20;[IO.File]::WriteAllText($tmp,$json,[Text.UTF8Encoding]::new($false))
    try { if(Test-Path -LiteralPath $full -PathType Leaf){[IO.File]::Replace($tmp,$full,$null,$true)}else{[IO.File]::Move($tmp,$full)} } finally { if(Test-Path -LiteralPath $tmp){[IO.File]::Delete($tmp)} }
    $State
}
function New-ESWebUiPersistentLeaseStore { param([Parameter(Mandatory)][string]$StorePath) $full=Assert-ESWebUiStorePath $StorePath;[pscustomobject]@{schemaVersion=1;recordType='WebPageStudioPersistentLeaseStore';storePath=$full;runtimeStatus='runtime-not-run';nonClaims=@('file-backed-static-adapter','does-not-prove-cross-process-runtime') } }
function Claim-ESWebUiPersistentLease {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$TaskId,[Parameter(Mandatory)][string]$WorkerId,[int]$TaskRevision=1,[int]$ContextVersion=1,[int]$LeaseSeconds=300)
    if($LeaseSeconds -lt 1 -or $LeaseSeconds -gt 3600){throw 'WEB_UI_LEASE_DURATION_INVALID'}
    Invoke-ESWebUiStoreMutex $Store.storePath {
        $state=Read-ESWebUiPersistentLeaseState $Store.storePath
        if($state.claims.PSObject.Properties.Name -contains $TaskId -and [string]$state.claims.$TaskId.status -eq 'active'){throw 'WEB_UI_LEASE_ALREADY_CLAIMED'}
        $now=[DateTime]::UtcNow;$lease=New-ESLeaseClaim -TaskId $TaskId -WorkerId $WorkerId -ExpectedTaskRevision $TaskRevision -ExpectedContextVersion $ContextVersion -IssuedUtc $now
        $lease.expiresUtc=$now.AddSeconds($LeaseSeconds).ToString('o');$state.revision=[int]$state.revision+1;$state.claims|Add-Member -NotePropertyName $TaskId -NotePropertyValue ([pscustomobject]@{status='active';lease=$lease;claimedUtc=$now.ToString('o')}) -Force;$state.events=@($state.events)+@([ordered]@{event='claimed';taskId=$TaskId;leaseId=$lease.leaseId;revision=$state.revision;utc=$now.ToString('o')});Write-ESWebUiPersistentLeaseState $Store.storePath $state|Out-Null;$lease
    }
}
function Complete-ESWebUiPersistentLease {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)]$Lease,[ValidateSet('candidate','failed','cancelled')][string]$Status='candidate',[int]$TaskRevision=1,[int]$ContextVersion=1)
    Invoke-ESWebUiStoreMutex $Store.storePath {
        $state=Read-ESWebUiPersistentLeaseState $Store.storePath;$claim=$state.claims.PSObject.Properties[[string]$Lease.taskId].Value
        if($null -eq $claim){throw 'WEB_UI_LEASE_NOT_FOUND'};$cas=Test-ESLeaseCas -LeaseClaim $claim.lease -CurrentTaskRevision $TaskRevision -CurrentContextVersion $ContextVersion
        if(-not $cas.canSubmitResult -or [string]$claim.lease.leaseId -cne [string]$Lease.leaseId){throw 'WEB_UI_LEASE_CAS_REJECTED'}
        $claim.status=$Status;$claim.completedUtc=[DateTime]::UtcNow.ToString('o');$state.revision=[int]$state.revision+1;$state.events=@($state.events)+@([ordered]@{event='completed';taskId=$Lease.taskId;status=$Status;revision=$state.revision;utc=[DateTime]::UtcNow.ToString('o')});Write-ESWebUiPersistentLeaseState $Store.storePath $state|Out-Null;$claim
    }
}
function Recover-ESWebUiPersistentLeaseState {
    param([Parameter(Mandatory)]$Store,[DateTime]$NowUtc=([DateTime]::UtcNow))
    Invoke-ESWebUiStoreMutex $Store.storePath {
        $state=Read-ESWebUiPersistentLeaseState $Store.storePath;$recovered=0
        foreach($property in @($state.claims.PSObject.Properties)) {
            $claim=$property.Value
            if([string]$claim.status -eq 'active' -and [DateTime]::Parse([string]$claim.lease.expiresUtc).ToUniversalTime() -lt $NowUtc) {
                $claim.status='orphaned';$claim.recoveryStatus='recovery-pending';$claim.recoveredUtc=$NowUtc.ToString('o');$recovered++
                $state.events=@($state.events)+@([ordered]@{event='lease-expired-orphaned';taskId=[string]$property.Name;leaseId=[string]$claim.lease.leaseId;utc=$NowUtc.ToString('o')})
            }
        }
        if($recovered -gt 0){$state.revision=[int]$state.revision+1;Write-ESWebUiPersistentLeaseState $Store.storePath $state|Out-Null}
        [ordered]@{recoveredCount=$recovered;revision=[int]$state.revision;runtimeStatus='runtime-not-run';nonClaims=@('state-recovery-only','does-not-terminate-external-process','does-not-prove-crash-recovery')}
    }
}
Export-ModuleMember -Function New-ESWebUiPersistentLeaseStore,Read-ESWebUiPersistentLeaseState,Claim-ESWebUiPersistentLease,Complete-ESWebUiPersistentLease,Recover-ESWebUiPersistentLeaseState
