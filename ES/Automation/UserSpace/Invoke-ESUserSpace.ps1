[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Initialize','Update','Discover','Validate')][string]$Action,
    [string]$PersonId,
    [string]$DisplayName,
    [ValidateSet('User','Agent')][string]$Kind = 'User',
    [string[]]$Responsibilities = @(),
    [string]$BranchStrategy = 'task branch + reviewed merge',
    [string]$MergePolicy = 'no direct main push; require evidence before merge',
    [string]$WorkingHours = '',
    [string]$Language = 'zh-CN',
    [string]$Contact = '',
    [string[]]$DiscoverableRoutes = @('workspace','task','evidence'),
    [ValidateSet('ProjectIgnored','External')][string]$PrivateStorageClass = 'ProjectIgnored',
    [string]$PrivateLocator = '',
    [int]$ExpectedRevision = 0,
    [switch]$TransferOwnership,
    [switch]$ConfirmTeamMember,
    [switch]$ConfirmVisibility,
    [switch]$ConfirmPreviousOwnerLockout,
    [string]$TakeoverReason = '',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$publicRoot = Join-Path $root 'ES\AISpace\Public\People'
$schemaPath = Join-Path $root 'ES\Automation\UserSpace\es-user-registration-v1.schema.json'

function Read-Json([string]$path) { $utf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $path).Path)) | ConvertFrom-Json }
function Write-Json([string]$path, [object]$value) {
    $dir = Split-Path -Parent $path; [IO.Directory]::CreateDirectory($dir) | Out-Null
    [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 12), $utf8)
}
function Get-RegistrationHash([object]$r) {
    $copy = $r | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $copy.contentHash = $null
    $canonical = $copy | ConvertTo-Json -Depth 12 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Assert-Id([string]$id) { if ($id -notmatch '^[a-z][a-z0-9-]{1,47}$') { throw "PersonId 无效：$id" } }
function Get-OwnerSubjectHash {
    $subject = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($subject)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Registration([string]$id) {
    Assert-Id $id
    $locator = if ($PrivateLocator) { $PrivateLocator } else { "ES/AISpace/Local/$id" }
    [ordered]@{
        schemaVersion=1; spaceId=([Guid]::NewGuid().ToString('N')); personId=$id; kind=$Kind; displayName=$DisplayName; ownerSubjectHash=(Get-OwnerSubjectHash); ownerId=$id; visibility='PublicMetadata'; membershipPolicy='OpenReadOwnerWrite'; storageLocator=$locator
        responsibilities=@($Responsibilities); preferences=[ordered]@{language=$Language;branchStrategy=$BranchStrategy;mergePolicy=$MergePolicy;workingHours=$WorkingHours}
        public=[ordered]@{contact=$Contact;discoverableRoutes=@($DiscoverableRoutes)}
        private=[ordered]@{storageClass=$PrivateStorageClass;locator=$locator}
        status='Active'; revision=1; contentHash=''; updatedUtc=[DateTimeOffset]::UtcNow.ToString('O')
    }
}
function Validate-Registration([object]$r, [string]$path) {
    if ($null -eq $r -or [int]$r.schemaVersion -ne 1) { throw "InvalidSchema:$path" }
    if ([string]$r.spaceId -notmatch '^[a-f0-9]{32}$' -or [string]$r.ownerId -notmatch '^[a-z][a-z0-9-]{1,47}$' -or [string]::IsNullOrWhiteSpace([string]$r.storageLocator)) { throw "SpaceIdentityMissing:$path" }
    Assert-Id ([string]$r.personId)
    if ([string]$r.kind -notin @('User','Agent')) { throw "InvalidKind:$path" }
    if ([string]::IsNullOrWhiteSpace([string]$r.displayName)) { throw "MissingDisplayName:$path" }
    if ([string]$r.private.storageClass -eq 'ProjectIgnored' -and [string]$r.private.locator -notmatch '^ES/AISpace/Local/[a-z][a-z0-9-]{1,47}$') { throw "PrivateLocatorMustBeLocal:$path" }
    if ([string]$r.private.locator -match '(^|[/\\])\.\.([/\\]|$)') { throw "Traversal:$path" }
    if ([string]$r.contentHash -ne (Get-RegistrationHash $r)) { throw "ContentHashMismatch:$path" }
    [pscustomobject][ordered]@{ path=$path; personId=[string]$r.personId; kind=[string]$r.kind; status=[string]$r.status; revision=[int]$r.revision; valid=$true }
}

if ($Action -eq "Initialize") {
        if ([string]::IsNullOrWhiteSpace($PersonId) -or [string]::IsNullOrWhiteSpace($DisplayName)) { throw "Initialize requires PersonId and DisplayName." }
        $path = Join-Path (Join-Path $publicRoot $PersonId) "registration.json"
        if (Test-Path -LiteralPath $path) { throw "AlreadyExists:$path" }
        $r = Registration $PersonId; $r.contentHash = Get-RegistrationHash $r; Write-Json $path $r
        if ($PrivateStorageClass -eq 'ProjectIgnored') {
            $privatePath = Join-Path $root (($r.private.locator -replace '/', '\'))
            [IO.Directory]::CreateDirectory($privatePath) | Out-Null
            $marker = Join-Path $privatePath '.gitkeep'
            if (-not (Test-Path -LiteralPath $marker)) { [IO.File]::WriteAllText($marker, "", $utf8) }
        }
        $privateLocator = [string]$r.private.locator
        Write-Output $path
}
if ($Action -eq "Update") {
        if ([string]::IsNullOrWhiteSpace($PersonId)) { throw "Update requires PersonId." }
        if ($ExpectedRevision -lt 1) { throw 'ExpectedRevisionRequired' }
        if ($TransferOwnership -and (-not $ConfirmTeamMember -or -not $ConfirmVisibility -or -not $ConfirmPreviousOwnerLockout -or [string]::IsNullOrWhiteSpace($TakeoverReason))) { throw 'TakeoverConfirmationRequired' }
        $path = Join-Path (Join-Path $publicRoot $PersonId) "registration.json"; if (-not (Test-Path $path)) { throw "NotFound:$path" }
        $mutex = [Threading.Mutex]::new($false, 'ESUserSpace-' + ($root -replace '[^a-zA-Z0-9]', '_') + '-' + $PersonId)
        if (-not $mutex.WaitOne(30000)) { $mutex.Dispose(); throw 'UpdateLockTimeout' }
        try {
        $r = Read-Json $path
        if ([string]$r.ownerSubjectHash -ne (Get-OwnerSubjectHash) -and -not $TransferOwnership) { throw "OwnerSubjectMismatch:$path (use -TransferOwnership to take over)" }
        if ([int]$r.revision -ne $ExpectedRevision) { throw "RevisionConflict:$($r.revision)" }
        if ($PSBoundParameters.ContainsKey('DisplayName')) { $r.displayName = $DisplayName }
        if ($PSBoundParameters.ContainsKey('Kind')) { $r.kind = $Kind }
        if ($PSBoundParameters.ContainsKey('Responsibilities')) { $r.responsibilities = @($Responsibilities) }
        if ($PSBoundParameters.ContainsKey('BranchStrategy')) { $r.preferences.branchStrategy = $BranchStrategy }
        if ($PSBoundParameters.ContainsKey('MergePolicy')) { $r.preferences.mergePolicy = $MergePolicy }
        if ($PSBoundParameters.ContainsKey('WorkingHours')) { $r.preferences.workingHours = $WorkingHours }
        if ($PSBoundParameters.ContainsKey('Language')) { $r.preferences.language = $Language }
        if ($PSBoundParameters.ContainsKey('Contact')) { $r.public.contact = $Contact }
        if ($PSBoundParameters.ContainsKey('DiscoverableRoutes')) { $r.public.discoverableRoutes = @($DiscoverableRoutes) }
        if ($TransferOwnership) { $r.ownerSubjectHash = Get-OwnerSubjectHash }
        $r.revision = [int]$r.revision + 1; $r.contentHash = ''; $r.updatedUtc=[DateTimeOffset]::UtcNow.ToString('O'); $r.contentHash = Get-RegistrationHash $r; Write-Json $path $r; Validate-Registration $r $path | ConvertTo-Json -Depth 8
        } finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
}
if ($Action -eq "Discover") {
    $items=@(Get-ChildItem -LiteralPath $publicRoot -Recurse -File -Filter 'registration.json' -ErrorAction SilentlyContinue | ForEach-Object { Validate-Registration (Read-Json $_.FullName) $_.FullName })
    if (@($items | Group-Object personId | Where-Object Count -gt 1).Count -gt 0) { throw 'DuplicatePersonId' }
    [pscustomobject][ordered]@{action=$Action;count=$items.Count;registrations=$items} | ConvertTo-Json -Depth 8
}
if ($Action -eq "Validate") {
    $items=@(Get-ChildItem -LiteralPath $publicRoot -Recurse -File -Filter 'registration.json' -ErrorAction SilentlyContinue | ForEach-Object { Validate-Registration (Read-Json $_.FullName) $_.FullName })
    if (@($items | Group-Object personId | Where-Object Count -gt 1).Count -gt 0) { throw 'DuplicatePersonId' }
    [pscustomobject][ordered]@{action=$Action;status="passed";count=$items.Count;registrations=$items;schemaPath=$schemaPath;runtimeStatus="runtime-not-run"} | ConvertTo-Json -Depth 8
}
