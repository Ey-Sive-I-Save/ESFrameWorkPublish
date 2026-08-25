$ErrorActionPreference = 'Stop'

function Get-ESSemanticArchiveRoot {
    $base = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { $env:LOCALAPPDATA } else { [IO.Path]::GetTempPath() }
    return [IO.Path]::GetFullPath((Join-Path $base 'ESFrameworkSemanticArchives')).TrimEnd('\', '/')
}

function Assert-ESSemanticArchiveKey([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9._-]{1,96}$') { throw "$Name must contain only bounded logical identifier characters." }
}

function Test-ESArchiveAbsolutePath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match '^[A-Za-z]:[\\/]' -or $Value -match '^\\\\' -or $Value -match '^/'
}

function Assert-ESArchiveNoAbsolutePaths([object]$Value, [string]$Location = '$') {
    if ($null -eq $Value) { return }
    if ($Value -is [string]) {
        if (Test-ESArchiveAbsolutePath ([string]$Value)) { throw "Absolute path content is forbidden in semantic archive: $Location" }
        return
    }
    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            if ([string]$key -match '(?i)(absolutePath|sourceAbsolutePath|projectRoot|cwd|workingDirectory)') { throw "Real-path field is forbidden in semantic archive: $Location.$key" }
            Assert-ESArchiveNoAbsolutePaths $Value[$key] "$Location.$key"
        }
        return
    }
    if ($Value -is [pscustomobject]) {
        foreach ($property in $Value.PSObject.Properties) {
            if ([string]$property.Name -match '(?i)(absolutePath|sourceAbsolutePath|projectRoot|cwd|workingDirectory)') { throw "Real-path field is forbidden in semantic archive: $Location.$($property.Name)" }
            Assert-ESArchiveNoAbsolutePaths $property.Value "$Location.$($property.Name)"
        }
        return
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $index = 0
        foreach ($item in $Value) { Assert-ESArchiveNoAbsolutePaths $item "$Location[$index]"; $index++ }
    }
}

function ConvertTo-ESArchiveRelativePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) { throw "Archive scope path must be project-relative: $Path" }
    $normalized = $Path.Replace('\', '/').TrimStart('./')
    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -match '(^|/)\.\.(?:/|$)') { throw "Archive scope path escapes project root: $Path" }
    return $normalized
}

function Get-ESSemanticArchivePath([string]$ProjectKey, [string]$ArchiveId) {
    Assert-ESSemanticArchiveKey $ProjectKey 'ProjectKey'
    Assert-ESSemanticArchiveKey $ArchiveId 'ArchiveId'
    return Join-Path (Join-Path (Get-ESSemanticArchiveRoot) $ProjectKey) ($ArchiveId + '.json')
}

function Write-ESSemanticArchiveCreateOnly([string]$Path, [object]$Archive) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    $json = $Archive | ConvertTo-Json -Depth 16
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

function Read-ESSemanticArchive([string]$ProjectKey, [string]$ArchiveId) {
    $path = Get-ESSemanticArchivePath $ProjectKey $ArchiveId
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Semantic archive was not found: $ProjectKey/$ArchiveId" }
    try { $archive = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw "Semantic archive is not valid UTF-8 JSON: $ProjectKey/$ArchiveId" }
    if ([int]$archive.schemaVersion -ne 1 -or [string]$archive.archiveKind -ne 'es-semantic-archive') { throw "Unsupported semantic archive schema: $ProjectKey/$ArchiveId" }
    Assert-ESArchiveNoAbsolutePaths $archive
    return [pscustomobject]@{ archive = $archive; path = $path; storageLocator = "$ProjectKey/$ArchiveId.json" }
}

function Get-ESArchiveSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ESRecentWorktreeScope([string]$ProjectRoot, [int]$Limit = 64) {
    $items = [Collections.Generic.List[object]]::new()
    $branch = ''
    $head = ''
    if ([string]::IsNullOrWhiteSpace($ProjectRoot) -or -not (Test-Path -LiteralPath $ProjectRoot -PathType Container)) {
        return [pscustomobject]@{ items = @(); branch = ''; headSha = ''; source = 'unobserved' }
    }
    try {
        $branch = [string]((& git -C $ProjectRoot branch --show-current 2>$null) | Select-Object -First 1)
        $head = [string]((& git -C $ProjectRoot rev-parse HEAD 2>$null) | Select-Object -First 1)
        $statusLines = @((& git -C $ProjectRoot status --short --untracked-files=all 2>$null))
        foreach ($line in $statusLines) {
            if ($items.Count -ge $Limit) { break }
            $text = [string]$line
            if ($text.Length -lt 4) { continue }
            $kind = if ($text.Substring(0, 2) -match 'D') { 'deleted' } elseif ($text.Substring(0, 2) -match 'A|\?') { 'added' } elseif ($text.Substring(0, 2) -match 'R') { 'renamed' } else { 'modified' }
            $relative = $text.Substring(3).Trim().Trim('"')
            try { $relative = ConvertTo-ESArchiveRelativePath $relative } catch { continue }
            if (@($items | Where-Object relativePath -eq $relative).Count -gt 0) { continue }
            $full = Join-Path $ProjectRoot ($relative.Replace('/', '\'))
            [void]$items.Add([ordered]@{ relativePath = $relative; changeKind = $kind; contentSha256 = if (Test-Path -LiteralPath $full -PathType Leaf) { Get-ESArchiveSha256 $full } else { '' } })
        }
        $source = 'worktree-observed'
    }
    catch { $source = 'unobserved' }
    return [pscustomobject]@{ items = $items.ToArray(); branch = $branch.Trim(); headSha = $head.Trim(); source = $source }
}
