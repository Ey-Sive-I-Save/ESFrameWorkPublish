Set-StrictMode -Version Latest

$script:ESBuildIdentityContractRef = 'ES/Automation/Contracts/es-unity-build-identity-receipt-v1.schema.json'
$script:ESBuildIdentityReceiptRoot = 'ES/Output/BuildIdentity'
$script:ESBuildIdentityMaxFiles = 20000
$script:ESBuildIdentityMaxBytes = 20GB
$script:ESBuildIdentityUtf8 = New-Object Text.UTF8Encoding($false, $true)

function Get-ESBuildSha256Bytes {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-ESBuildSha256Text {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)
    return Get-ESBuildSha256Bytes -Bytes ([Text.UTF8Encoding]::new($false).GetBytes($Text))
}

function Get-ESBuildFileHash {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-ESBuildCanonicalJson {
    param([Parameter(Mandatory = $true)][object]$Value)
    return (ConvertTo-Json -InputObject $Value -Depth 32 -Compress)
}

function Get-ESBuildObjectHash {
    param([Parameter(Mandatory = $true)][object]$Value)
    return Get-ESBuildSha256Text -Text (ConvertTo-ESBuildCanonicalJson -Value $Value)
}

function Resolve-ESBuildProjectRoot {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $root = [IO.Path]::GetFullPath($ProjectRoot.Trim()).TrimEnd('\', '/')
    foreach ($relative in @('ProjectSettings/ProjectVersion.txt', 'ProjectSettings/ProjectSettings.asset', 'Packages/manifest.json', 'Packages/packages-lock.json')) {
        $full = Join-Path $root $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw "Unity project identity file is missing: $relative"
        }
    }
    return $root
}

function ConvertTo-ESBuildRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$FullPath
    )
    $rootPrefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($FullPath)
    if (-not $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes ProjectRoot: $FullPath"
    }
    return $resolved.Substring($rootPrefix.Length).Replace('\', '/')
}

function Assert-ESBuildNoReparsePoint {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$FullPath
    )
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $current = [IO.Path]::GetFullPath($FullPath)
    while ($current.Length -ge $rootFull.Length) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are not allowed in build identity paths: $current"
            }
        }
        if ($current.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
}

function Resolve-ESBuildRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [string]$RequiredPrefix,
        [ValidateSet('Any', 'File', 'Directory')][string]$PathType = 'Any',
        [switch]$MustExist
    )
    $relative = $RelativePath.Replace('\', '/').Trim().TrimEnd('/')
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(/|$)' -or $relative -match '[:*?\x00-\x1f]') {
        throw "Path must be a bounded project-relative path: $RelativePath"
    }
    foreach ($segment in @($relative.Split('/'))) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -in @('.', '..') -or $segment -match '[ .]$' -or
            $segment -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?$') {
            throw "Path contains a Windows-ambiguous segment: $RelativePath"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($RequiredPrefix)) {
        $prefix = $RequiredPrefix.Replace('\', '/').Trim().TrimEnd('/')
        if (-not ($relative.Equals($prefix, [StringComparison]::OrdinalIgnoreCase) -or $relative.StartsWith($prefix + '/', [StringComparison]::OrdinalIgnoreCase))) {
            throw "Path must remain under ${prefix}: $relative"
        }
    }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    [void](ConvertTo-ESBuildRelativePath -Root $Root -FullPath $full)
    Assert-ESBuildNoReparsePoint -Root $Root -FullPath $full
    if ($MustExist) {
        $testType = if ($PathType -eq 'File') { 'Leaf' } elseif ($PathType -eq 'Directory') { 'Container' } else { 'Any' }
        if ($testType -eq 'Any') {
            if (-not (Test-Path -LiteralPath $full)) { throw "Required path is missing: $relative" }
        }
        elseif (-not (Test-Path -LiteralPath $full -PathType $testType)) {
            throw "Required $($PathType.ToLowerInvariant()) is missing: $relative"
        }
    }
    return [pscustomobject]@{ relative = $relative; full = $full }
}

function Invoke-ESBuildGit {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][ValidateSet('status', 'head', 'branch')][string]$Operation
    )
    $arguments = switch ($Operation) {
        'status' { '-c core.quotepath=false status --porcelain=v1 -z --untracked-files=all' }
        'head' { 'rev-parse HEAD' }
        'branch' { 'branch --show-current' }
    }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = 'git'
    $start.Arguments = $Arguments
    $start.WorkingDirectory = $Root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false, $true)
    $start.StandardErrorEncoding = [Text.UTF8Encoding]::new($false, $true)
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Failed to start read-only Git inspection.' }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Git inspection failed: $stderr" }
    return $stdout
}

function Test-ESBuildPathExcluded {
    param([string]$Path, [string[]]$Exclusions)
    foreach ($item in $Exclusions) {
        if ($Path.Equals($item, [StringComparison]::OrdinalIgnoreCase) -or $Path.StartsWith($item.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Get-ESBuildWorktreeManifest {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Exclusions
    )
    $raw = Invoke-ESBuildGit -Root $Root -Operation status
    $tokens = @($raw.Split([char]0) | Where-Object { $_.Length -gt 0 })
    $entries = New-Object Collections.Generic.List[object]
    $totalBytes = [long]0
    for ($index = 0; $index -lt $tokens.Count; $index++) {
        $token = [string]$tokens[$index]
        if ($token.Length -lt 4) { throw 'Git returned a malformed porcelain status entry.' }
        $status = $token.Substring(0, 2)
        $path = $token.Substring(3).Replace('\', '/')
        $originalPath = 'not-applicable'
        if ($status -match '[RC]') {
            $index++
            if ($index -ge $tokens.Count) { throw 'Git rename/copy status is missing its original path.' }
            $originalPath = ([string]$tokens[$index]).Replace('\', '/')
        }
        if (Test-ESBuildPathExcluded -Path $path -Exclusions $Exclusions) { continue }
        $resolved = Resolve-ESBuildRelativePath -Root $Root -RelativePath $path
        $length = [long]0
        $hash = 'deleted'
        if (Test-Path -LiteralPath $resolved.full -PathType Leaf) {
            $item = Get-Item -LiteralPath $resolved.full
            $length = [long]$item.Length
            $totalBytes += $length
            if ($entries.Count -ge $script:ESBuildIdentityMaxFiles -or $totalBytes -gt $script:ESBuildIdentityMaxBytes) {
                throw 'Worktree identity exceeds the declared file or byte budget.'
            }
            $hash = Get-ESBuildFileHash -Path $resolved.full
        }
        [void]$entries.Add([ordered]@{
            status = $status
            path = $resolved.relative
            originalPath = $originalPath
            byteLength = $length
            sha256 = $hash
        })
    }
    return @($entries.ToArray() | Sort-Object path, status, originalPath)
}

function Get-ESBuildDirectoryManifest {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativeRoot
    )
    $directory = Resolve-ESBuildRelativePath -Root $Root -RelativePath $RelativeRoot -PathType Directory -MustExist
    $files = @(Get-ChildItem -LiteralPath $directory.full -File -Recurse -Force | Sort-Object FullName)
    if ($files.Count -gt $script:ESBuildIdentityMaxFiles) { throw "Directory manifest exceeds file budget: $RelativeRoot" }
    $entries = New-Object Collections.Generic.List[object]
    $totalBytes = [long]0
    foreach ($file in $files) {
        Assert-ESBuildNoReparsePoint -Root $Root -FullPath $file.FullName
        $totalBytes += [long]$file.Length
        if ($totalBytes -gt $script:ESBuildIdentityMaxBytes) { throw "Directory manifest exceeds byte budget: $RelativeRoot" }
        [void]$entries.Add([ordered]@{
            path = ConvertTo-ESBuildRelativePath -Root $Root -FullPath $file.FullName
            byteLength = [long]$file.Length
            sha256 = Get-ESBuildFileHash -Path $file.FullName
        })
    }
    return @($entries.ToArray())
}

function Get-ESBuildProjectVersion {
    param([Parameter(Mandatory = $true)][string]$Root)
    $path = Join-Path $Root 'ProjectSettings\ProjectVersion.txt'
    $text = [IO.File]::ReadAllText($path, $script:ESBuildIdentityUtf8)
    $version = [regex]::Match($text, '(?m)^m_EditorVersion:\s*(?<value>[^\r\n]+)$')
    $revision = [regex]::Match($text, '(?m)^m_EditorVersionWithRevision:\s*[^\(]+\((?<value>[^\)]+)\)')
    if (-not $version.Success -or -not $revision.Success) { throw 'ProjectVersion.txt does not contain Unity version and revision.' }
    return [pscustomobject]@{ version = $version.Groups['value'].Value.Trim(); revision = $revision.Groups['value'].Value.Trim() }
}

function Get-ESBuildOptionalDirectoryIdentity {
    param([Parameter(Mandatory = $true)][string]$Root, [Parameter(Mandatory = $true)][string]$RelativePath)
    $resolved = Resolve-ESBuildRelativePath -Root $Root -RelativePath $RelativePath
    if (-not (Test-Path -LiteralPath $resolved.full -PathType Container)) {
        return [pscustomobject]@{ manifest = @(); hash = 'unknown' }
    }
    $manifest = @(Get-ESBuildDirectoryManifest -Root $Root -RelativeRoot $resolved.relative)
    return [pscustomobject]@{ manifest = $manifest; hash = Get-ESBuildObjectHash -Value $manifest }
}

function Get-ESBuildOptionalFileHash {
    param([Parameter(Mandatory = $true)][string]$Root, [Parameter(Mandatory = $true)][string]$RelativePath)
    $resolved = Resolve-ESBuildRelativePath -Root $Root -RelativePath $RelativePath
    if (-not (Test-Path -LiteralPath $resolved.full -PathType Leaf)) { return 'unknown' }
    return Get-ESBuildFileHash -Path $resolved.full
}

function Get-ESBuildHybridClrIdentity {
    param([Parameter(Mandatory = $true)][string]$Root, [Parameter(Mandatory = $true)][string]$BuildTarget)
    $settingsRelative = 'ProjectSettings/HybridCLRSettings.asset'
    $settings = Resolve-ESBuildRelativePath -Root $Root -RelativePath $settingsRelative
    if (-not (Test-Path -LiteralPath $settings.full -PathType Leaf)) {
        return [ordered]@{ mode = 'disabled'; settingsHash = 'not-applicable'; packageVersion = 'not-applicable'; packageManifestHash = 'not-applicable'; hotUpdateDllManifest = @(); hotUpdateDllManifestHash = 'not-applicable'; strippedAotDllManifest = @(); strippedAotDllManifestHash = 'not-applicable'; linkXmlHash = 'not-applicable'; aotGenericReferencesHash = 'not-applicable' }
    }
    $text = [IO.File]::ReadAllText($settings.full, $script:ESBuildIdentityUtf8)
    $enabled = [regex]::IsMatch($text, '(?m)^\s*enable:\s*1\s*$')
    if (-not $enabled) {
        return [ordered]@{ mode = 'disabled'; settingsHash = Get-ESBuildFileHash $settings.full; packageVersion = 'not-applicable'; packageManifestHash = 'not-applicable'; hotUpdateDllManifest = @(); hotUpdateDllManifestHash = 'not-applicable'; strippedAotDllManifest = @(); strippedAotDllManifestHash = 'not-applicable'; linkXmlHash = 'not-applicable'; aotGenericReferencesHash = 'not-applicable' }
    }
    $packageRoot = 'Packages/com.code-philosophy.hybridclr'
    $packageJson = Resolve-ESBuildRelativePath -Root $Root -RelativePath "$packageRoot/package.json" -PathType File -MustExist
    $package = [IO.File]::ReadAllText($packageJson.full, $script:ESBuildIdentityUtf8) | ConvertFrom-Json
    $manifest = @(Get-ESBuildDirectoryManifest -Root $Root -RelativeRoot $packageRoot)
    $hotUpdateRootMatch = [regex]::Match($text, '(?m)^\s*hotUpdateDllCompileOutputRootDir:\s*(?<value>[^\r\n]+)$')
    $strippedAotRootMatch = [regex]::Match($text, '(?m)^\s*strippedAOTDllOutputRootDir:\s*(?<value>[^\r\n]+)$')
    $linkMatch = [regex]::Match($text, '(?m)^\s*outputLinkFile:\s*(?<value>[^\r\n]+)$')
    $genericMatch = [regex]::Match($text, '(?m)^\s*outputAOTGenericReferenceFile:\s*(?<value>[^\r\n]+)$')
    if (-not $hotUpdateRootMatch.Success -or -not $strippedAotRootMatch.Success -or -not $linkMatch.Success -or -not $genericMatch.Success) {
        throw 'HybridCLRSettings.asset is missing required generated-input paths.'
    }
    $hotUpdate = Get-ESBuildOptionalDirectoryIdentity -Root $Root -RelativePath ($hotUpdateRootMatch.Groups['value'].Value.Trim().TrimEnd('/') + '/' + $BuildTarget)
    $strippedAot = Get-ESBuildOptionalDirectoryIdentity -Root $Root -RelativePath ($strippedAotRootMatch.Groups['value'].Value.Trim().TrimEnd('/') + '/' + $BuildTarget)
    $linkRelative = 'Assets/' + $linkMatch.Groups['value'].Value.Trim().TrimStart('/')
    $genericRelative = 'Assets/' + $genericMatch.Groups['value'].Value.Trim().TrimStart('/')
    return [ordered]@{
        mode = 'enabled'
        settingsHash = Get-ESBuildFileHash $settings.full
        packageVersion = [string]$package.version
        packageManifestHash = Get-ESBuildObjectHash -Value $manifest
        hotUpdateDllManifest = $hotUpdate.manifest
        hotUpdateDllManifestHash = $hotUpdate.hash
        strippedAotDllManifest = $strippedAot.manifest
        strippedAotDllManifestHash = $strippedAot.hash
        linkXmlHash = Get-ESBuildOptionalFileHash -Root $Root -RelativePath $linkRelative
        aotGenericReferencesHash = Get-ESBuildOptionalFileHash -Root $Root -RelativePath $genericRelative
    }
}

function Test-ESBuildIdentityIncomplete {
    param([Parameter(Mandatory = $true)][object]$InputIdentity)
    if ([string]$InputIdentity.managedStrippingLevel -eq 'unknown') { return $true }
    $hybrid = $InputIdentity.hybridClr
    if ([string]$hybrid.mode -ne 'enabled') { return $false }
    if (@($hybrid.hotUpdateDllManifest).Count -eq 0 -or @($hybrid.strippedAotDllManifest).Count -eq 0) { return $true }
    foreach ($value in @($hybrid.hotUpdateDllManifestHash, $hybrid.strippedAotDllManifestHash, $hybrid.linkXmlHash, $hybrid.aotGenericReferencesHash)) {
        if ([string]$value -eq 'unknown') { return $true }
    }
    return $false
}

function New-ESBuildInputState {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$ProjectId,
        [Parameter(Mandatory = $true)][string]$BuildTarget,
        [Parameter(Mandatory = $true)][string]$BuildTargetGroup,
        [Parameter(Mandatory = $true)][string]$Architecture,
        [Parameter(Mandatory = $true)][ValidateSet('Mono', 'IL2CPP')][string]$ScriptingBackend,
        [Parameter(Mandatory = $true)][bool]$Development,
        [string[]]$BuildOption = @(),
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [string[]]$ScenePath = @(),
        [string[]]$DefineSymbol = @(),
        [Parameter(Mandatory = $true)][string]$ManagedStrippingLevel,
        [Parameter(Mandatory = $true)][bool]$StripEngineCode
    )
    $output = Resolve-ESBuildRelativePath -Root $Root -RelativePath $OutputPath -RequiredPrefix 'ES/Output/Builds'
    $options = @($BuildOption | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() } | Sort-Object -Unique)
    $defines = @($DefineSymbol | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() } | Sort-Object -Unique)
    $exclusions = @($script:ESBuildIdentityReceiptRoot; $output.relative) | Sort-Object -Unique
    $worktree = @(Get-ESBuildWorktreeManifest -Root $Root -Exclusions $exclusions)
    $projectSettingsManifest = @(Get-ESBuildDirectoryManifest -Root $Root -RelativeRoot 'ProjectSettings')
    $sceneEntries = New-Object Collections.Generic.List[object]
    for ($sceneIndex = 0; $sceneIndex -lt @($ScenePath).Count; $sceneIndex++) {
        $scene = Resolve-ESBuildRelativePath -Root $Root -RelativePath ([string]$ScenePath[$sceneIndex]) -RequiredPrefix 'Assets' -PathType File -MustExist
        if (-not $scene.relative.EndsWith('.unity', [StringComparison]::OrdinalIgnoreCase)) { throw "ScenePath must identify a .unity file: $($scene.relative)" }
        [void]$sceneEntries.Add([ordered]@{ order = $sceneIndex; path = $scene.relative; sha256 = Get-ESBuildFileHash $scene.full })
    }
    $version = Get-ESBuildProjectVersion -Root $Root
    $head = (Invoke-ESBuildGit -Root $Root -Operation head).Trim()
    $branch = (Invoke-ESBuildGit -Root $Root -Operation branch).Trim()
    if ($head -notmatch '^[0-9a-f]{40}$' -or [string]::IsNullOrWhiteSpace($branch)) { throw 'Git HEAD or branch identity is invalid.' }
    $project = [ordered]@{
        projectId = $ProjectId
        projectRoot = $Root.Replace('\', '/')
        branch = $branch
        gitHead = $head
        unityVersion = $version.version
        unityRevision = $version.revision
    }
    $intent = [ordered]@{
        buildTarget = $BuildTarget
        buildTargetGroup = $BuildTargetGroup
        architecture = $Architecture
        scriptingBackend = $ScriptingBackend
        development = $Development
        buildOptions = $options
        outputPath = $output.relative
    }
    $input = [ordered]@{
        worktreeState = if ($worktree.Count -eq 0) { 'clean' } else { 'dirty' }
        worktreeExclusions = $exclusions
        worktreeManifest = $worktree
        scopedChangeManifestHash = Get-ESBuildObjectHash -Value $worktree
        projectSettingsHash = Get-ESBuildFileHash (Join-Path $Root 'ProjectSettings\ProjectSettings.asset')
        projectConfigurationManifest = $projectSettingsManifest
        projectConfigurationManifestHash = Get-ESBuildObjectHash -Value $projectSettingsManifest
        packageManifestHash = Get-ESBuildFileHash (Join-Path $Root 'Packages\manifest.json')
        packageLockHash = Get-ESBuildFileHash (Join-Path $Root 'Packages\packages-lock.json')
        scenes = @($sceneEntries.ToArray())
        sceneListHash = Get-ESBuildObjectHash -Value @($sceneEntries.ToArray())
        defineSymbols = $defines
        managedStrippingLevel = $ManagedStrippingLevel
        stripEngineCode = $StripEngineCode
        hybridClr = Get-ESBuildHybridClrIdentity -Root $Root -BuildTarget $BuildTarget
    }
    return [pscustomobject]@{ project = $project; intent = $intent; inputIdentity = $input }
}

function Get-ESBuildInputFingerprint {
    param(
        [Parameter(Mandatory = $true)][object]$Project,
        [Parameter(Mandatory = $true)][object]$Intent,
        [Parameter(Mandatory = $true)][object]$InputIdentity
    )
    $payload = [ordered]@{
        fingerprintSchemaVersion = 1
        project = [ordered]@{
            projectId = [string]$Project.projectId
            gitHead = [string]$Project.gitHead
            unityVersion = [string]$Project.unityVersion
            unityRevision = [string]$Project.unityRevision
        }
        intent = [ordered]@{
            buildTarget = [string]$Intent.buildTarget
            buildTargetGroup = [string]$Intent.buildTargetGroup
            architecture = [string]$Intent.architecture
            scriptingBackend = [string]$Intent.scriptingBackend
            development = [bool]$Intent.development
            buildOptions = @($Intent.buildOptions)
            outputPath = [string]$Intent.outputPath
        }
        input = [ordered]@{
            scopedChangeManifestHash = [string]$InputIdentity.scopedChangeManifestHash
            projectSettingsHash = [string]$InputIdentity.projectSettingsHash
            projectConfigurationManifestHash = [string]$InputIdentity.projectConfigurationManifestHash
            packageManifestHash = [string]$InputIdentity.packageManifestHash
            packageLockHash = [string]$InputIdentity.packageLockHash
            sceneListHash = [string]$InputIdentity.sceneListHash
            defineSymbols = @($InputIdentity.defineSymbols)
            managedStrippingLevel = [string]$InputIdentity.managedStrippingLevel
            stripEngineCode = [bool]$InputIdentity.stripEngineCode
            hybridClr = [ordered]@{
                mode = [string]$InputIdentity.hybridClr.mode
                settingsHash = [string]$InputIdentity.hybridClr.settingsHash
                packageVersion = [string]$InputIdentity.hybridClr.packageVersion
                packageManifestHash = [string]$InputIdentity.hybridClr.packageManifestHash
                hotUpdateDllManifestHash = [string]$InputIdentity.hybridClr.hotUpdateDllManifestHash
                strippedAotDllManifestHash = [string]$InputIdentity.hybridClr.strippedAotDllManifestHash
                linkXmlHash = [string]$InputIdentity.hybridClr.linkXmlHash
                aotGenericReferencesHash = [string]$InputIdentity.hybridClr.aotGenericReferencesHash
            }
        }
    }
    return Get-ESBuildObjectHash -Value $payload
}

function Get-ESBuildArtifactIdentity {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$OutputRoot,
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Path
    )
    if ($Role -notmatch '^[a-z0-9][a-z0-9._-]{1,63}$') { throw "Artifact role is invalid: $Role" }
    $resolved = Resolve-ESBuildRelativePath -Root $Root -RelativePath $Path -RequiredPrefix $OutputRoot -MustExist
    if (Test-Path -LiteralPath $resolved.full -PathType Leaf) {
        $file = Get-Item -LiteralPath $resolved.full
        return [ordered]@{ role = $Role; path = $resolved.relative; kind = 'file'; byteLength = [long]$file.Length; sha256 = Get-ESBuildFileHash $file.FullName; files = @() }
    }
    if (-not (Test-Path -LiteralPath $resolved.full -PathType Container)) { throw "Artifact path is neither a file nor directory: $($resolved.relative)" }
    $files = @(Get-ESBuildDirectoryManifest -Root $Root -RelativeRoot $resolved.relative)
    $bytes = [long](($files | Measure-Object -Property byteLength -Sum).Sum)
    return [ordered]@{ role = $Role; path = $resolved.relative; kind = 'directory'; byteLength = $bytes; sha256 = Get-ESBuildObjectHash -Value $files; files = $files }
}

function Read-ESBuildIdentityJson {
    param([Parameter(Mandatory = $true)][string]$Path)
    $raw = [IO.File]::ReadAllText($Path, $script:ESBuildIdentityUtf8)
    try { return $raw | ConvertFrom-Json } catch { throw "Build identity receipt is not valid JSON: $($_.Exception.Message)" }
}

function Write-ESBuildIdentityJson {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [Parameter(Mandatory = $true)][object]$Receipt
    )
    $resolved = Resolve-ESBuildRelativePath -Root $Root -RelativePath $ReceiptPath -RequiredPrefix $script:ESBuildIdentityReceiptRoot
    if (-not $resolved.relative.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) { throw 'ReceiptPath must end in .json.' }
    if (Test-Path -LiteralPath $resolved.full) { throw "ReceiptPath already exists and receipts are immutable: $($resolved.relative)" }
    $parent = Split-Path -Parent $resolved.full
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { [void](New-Item -ItemType Directory -Path $parent -Force) }
    $temporary = "$($resolved.full).tmp-$([Guid]::NewGuid().ToString('N'))"
    [IO.File]::WriteAllText($temporary, ($Receipt | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $resolved.full
    return $resolved.relative
}

function Get-ESBuildContractIdentity {
    param([Parameter(Mandatory = $true)][string]$Root)
    $contract = Resolve-ESBuildRelativePath -Root $Root -RelativePath $script:ESBuildIdentityContractRef -PathType File -MustExist
    return [pscustomobject]@{ reference = $script:ESBuildIdentityContractRef; hash = Get-ESBuildFileHash $contract.full }
}
