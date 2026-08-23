[CmdletBinding()]
param(
    [ValidateSet('Build','Verify')][string]$Mode = 'Build',
    [string]$ProjectRoot = '.',
    [Parameter(Mandatory=$true)][string]$TaskId,
    [string[]]$Path,
    [string]$ParserVersion = '1',
    [string]$SnapshotPath,
    [int]$MaxReadRetries = 2,
    [int]$MaxFiles = 256,
    [int64]$MaxTotalBytes = 536870912,
    [int64]$MaxFileBytes = 104857600,
    [switch]$AllowDuplicateContent
)
$ErrorActionPreference = 'Stop'
if ($MaxReadRetries -lt 0 -or $MaxReadRetries -gt 5) { throw 'MaxReadRetries must be between 0 and 5.' }
if ($MaxFiles -lt 1 -or $MaxFiles -gt 4096) { throw 'MaxFiles must be between 1 and 4096.' }
if ($MaxTotalBytes -lt 1 -or $MaxTotalBytes -gt 4294967296) { throw 'MaxTotalBytes is outside the safe bound.' }
if ($MaxFileBytes -lt 1 -or $MaxFileBytes -gt $MaxTotalBytes) { throw 'MaxFileBytes must be positive and no greater than MaxTotalBytes.' }
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\','/')
function Assert-ProjectRelativePath([string]$Value,[string]$Name){if([string]::IsNullOrWhiteSpace($Value)-or[IO.Path]::IsPathRooted($Value)-or$Value-match '(^|[\\/])\.\.([\\/]|$)'-or$Value-match '[*?]'){throw "$Name must be project-relative and bounded."};$full=[IO.Path]::GetFullPath((Join-Path $root ($Value.Replace('/',[IO.Path]::DirectorySeparatorChar))));if(-not($full.Equals($root,[StringComparison]::OrdinalIgnoreCase)-or$full.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw "$Name escapes ProjectRoot."};$full}
if ($TaskId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$') { throw 'TaskId contains unsafe characters.' }
if ($ParserVersion -notmatch '^[A-Za-z0-9._-]{1,32}$') { throw 'ParserVersion contains unsafe characters.' }
if ([string]::IsNullOrWhiteSpace($SnapshotPath)) { $SnapshotPath = "ES/Output/TaskReadSnapshots/$TaskId.json" }
$snapshotFull = Assert-ProjectRelativePath $SnapshotPath 'SnapshotPath'
$snapshotDir = Split-Path -Parent $snapshotFull
$mutexName = 'Global\ESFrameworkTaskReadSnapshot_' + $TaskId
$mutex = [Threading.Mutex]::new($false, $mutexName)
$acquired = $false
try {
    $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds(15))
    if (-not $acquired) { throw 'Another snapshot operation for this TaskId is active.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    function Get-Sha256Stable([string]$file) {
        for ($attempt=0; $attempt -le $MaxReadRetries; $attempt++) {
            $before = Get-Item -LiteralPath $file -ErrorAction Stop
            $bytes = [IO.File]::ReadAllBytes($file)
            $after = Get-Item -LiteralPath $file -ErrorAction Stop
            if ($before.Length -eq $after.Length -and $before.LastWriteTimeUtc.Ticks -eq $after.LastWriteTimeUtc.Ticks) {
                return [pscustomobject]@{ Length=[int64]$after.Length; LastWriteUtc=$after.LastWriteTimeUtc.ToString('o'); Sha256=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() }
            }
            if ($attempt -eq $MaxReadRetries) { throw "Input changed while being read: $file" }
        }
    }
    function Resolve-ProjectFile([string]$p) {
        $parentToken = [string]([char]46) + [char]46
        $segments = ($p.Replace('\\','/')).Split('/')
        $unsafePath = ([string]::IsNullOrWhiteSpace($p) -or [IO.Path]::IsPathRooted($p) -or $segments -contains $parentToken -or $p -match '[*?]')
        if ($unsafePath) { throw "Unsafe input path: $p" }
        $full = (Resolve-Path -LiteralPath (Join-Path $root ($p.Replace('/',[IO.Path]::DirectorySeparatorChar))) -ErrorAction Stop).Path
        $relative = $full.Substring($root.Length).TrimStart([char]92,[char]47).Replace([string][char]92,'/')
        $item = Get-Item -LiteralPath $full -ErrorAction Stop
        $relativeSegments = $relative.Split('/')
        if (($relativeSegments | Where-Object { $_ -eq $parentToken }).Count -gt 0 -or $item.LinkType) { throw "Input escapes project root: $p" }
        [pscustomobject]@{ Full=$full; Relative=$relative }
    }
    function Read-Json([string]$file) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
        try { Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop } catch { throw "Snapshot manifest is invalid: $file" }
    }
    function Write-Atomic([string]$file,[string]$content) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $file) | Out-Null
        $tmp = "$file.$([Guid]::NewGuid().ToString('N')).tmp"
        try { [IO.File]::WriteAllText($tmp,$content,(New-Object Text.UTF8Encoding($false))); Move-Item -LiteralPath $tmp -Destination $file -Force }
        finally { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue } }
    }
    if ($Mode -eq 'Verify' -and -not (Test-Path -LiteralPath $snapshotFull -PathType Leaf)) { throw 'Snapshot manifest is missing.' }
    $old = Read-Json $snapshotFull
    if ($Mode -eq 'Verify') { $Path = @($old.entries | ForEach-Object path); if ($old.taskId -ne $TaskId) { throw 'Snapshot TaskId mismatch.' }; if ($old.parserVersion -ne $ParserVersion) { throw 'ParserVersion changed; snapshot is stale.' } }
    if (@($Path).Count -eq 0) { throw 'At least one input Path is required for Build.' }
    if (@($Path).Count -gt $MaxFiles) { throw "ReadSet exceeds MaxFiles=$MaxFiles." }
    $normalized = @($Path | ForEach-Object { (Resolve-ProjectFile ([string]$_)).Relative })
    $duplicates = @($normalized | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) { throw ('Duplicate input paths: ' + ($duplicates.Name -join ', ')) }
    $entries = @(); $hits=0; $misses=0; $invalidated=0; $totalBytes=[int64]0
    foreach ($relative in $normalized) {
        $resolved = Resolve-ProjectFile $relative; $observed = Get-Sha256Stable $resolved.Full
        if ($observed.Length -gt $MaxFileBytes) { throw "File exceeds MaxFileBytes=$MaxFileBytes`: $relative" }
        $totalBytes += $observed.Length
        if ($totalBytes -gt $MaxTotalBytes) { throw "ReadSet exceeds MaxTotalBytes=$MaxTotalBytes." }
        $key = "$($resolved.Relative)|$($observed.Sha256)|$ParserVersion"
        $prior = @($old.entries | Where-Object { $_.path -eq $resolved.Relative }) | Select-Object -First 1
        if ($null -ne $prior -and $prior.cacheKey -eq $key) { $hits++ } else { $misses++; if($null -ne $prior){$invalidated++} }
        $entries += [ordered]@{ path=$resolved.Relative; length=$observed.Length; lastWriteUtc=$observed.LastWriteUtc; sha256=$observed.Sha256; cacheKey=$key }
    }
    $contentHashes = @($entries | ForEach-Object { [string]$_.sha256 })
    $contentDuplicates = @($contentHashes | Group-Object | Where-Object Count -gt 1)
    if ($contentDuplicates.Count -gt 0 -and -not $AllowDuplicateContent) { throw 'ReadSet contains duplicate file content; use one authoritative path or explicitly pass -AllowDuplicateContent.' }
    $canonicalLines = @($entries | ForEach-Object { [string]$_.path + '|' + [string]$_.sha256 + '|' + [string]$_.cacheKey } | Sort-Object)
    $canonical = $canonicalLines -join "`n"
    $snapshotHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-','').ToLowerInvariant()
    $status = 'passed'; $reason = $null
    if ($Mode -eq 'Verify' -and $null -ne $old -and $old.snapshotHash -ne $snapshotHash) { $status='stale'; $reason='Input bytes or parser version changed.' }
    $result = [ordered]@{ schemaVersion=1; taskId=$TaskId; projectRoot='.'; parserVersion=$ParserVersion; entries=@($entries | Sort-Object path); snapshotHash=$snapshotHash; createdUtc=if($null -ne $old){$old.createdUtc}else{[DateTime]::UtcNow.ToString('o')}; verifiedUtc=[DateTime]::UtcNow.ToString('o'); cacheHitCount=$hits; cacheMissCount=$misses; invalidatedCount=$invalidated; duplicateContentCount=$contentDuplicates.Count; totalBytes=$totalBytes; readCount=@($entries).Count; status=$status }
    if ($reason) { $result.reason=$reason }
    Write-Atomic $snapshotFull ($result | ConvertTo-Json -Depth 8)
    $result | ConvertTo-Json -Depth 8
    if ($status -ne 'passed') { exit 2 }
}
finally {
    if ($sha) { $sha.Dispose() }
    if ($acquired) { $mutex.ReleaseMutex() | Out-Null }
    $mutex.Dispose()
}
