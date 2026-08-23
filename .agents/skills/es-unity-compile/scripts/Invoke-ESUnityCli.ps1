[CmdletBinding()]
param(
    [ValidateSet('Status', 'Compile', 'EditModeTests', 'PlayModeTests')]
    [string]$Mode = 'Status',
    [string]$ProjectRoot,
    [string]$UnityPath,
    [string]$TestFilter,
    [string]$TestCategory,
    [string[]]$AssemblyName,
    [string]$ResultsPath,
    [string]$LogPath,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

function Resolve-ProjectRoot {
    param([string]$Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        $Candidate = (& git rev-parse --show-toplevel 2>$null)
    }
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        throw 'Cannot resolve the Unity project root. Pass -ProjectRoot.'
    }

    $resolved = [IO.Path]::GetFullPath($Candidate.Trim())
    $versionFile = Join-Path $resolved 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "ProjectSettings/ProjectVersion.txt was not found under: $resolved"
    }
    return $resolved
}

function Get-ProjectUnityVersion {
    param([string]$Root)

    $versionFile = Join-Path $Root 'ProjectSettings\ProjectVersion.txt'
    $versionLine = Get-Content -LiteralPath $versionFile -Encoding UTF8 |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    if ($null -eq $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "Cannot read m_EditorVersion from: $versionFile"
    }
    return $Matches[1].Trim()
}

function Test-CommandLineTargetsProject {
    param(
        [string]$CommandLine,
        [string]$Root
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return $false }
    $normalizedCommand = $CommandLine.Replace('/', '\').TrimEnd('\')
    $normalizedRoot = $Root.Replace('/', '\').TrimEnd('\')
    $pattern = '(?i)(?:^|["\s])' + [regex]::Escape($normalizedRoot) + '(?=$|["\s])'
    return [regex]::IsMatch($normalizedCommand, $pattern)
}

function Get-OpenProjectEditors {
    param([string]$Root)

    $editors = New-Object Collections.Generic.List[object]
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue)) {
        $commandLine = [string]$process.CommandLine
        if (-not (Test-CommandLineTargetsProject -CommandLine $commandLine -Root $Root)) { continue }
        if ($commandLine -match '(?i)AssetImportWorker') { continue }

        $editors.Add([pscustomobject]@{
            processId = [int]$process.ProcessId
            executablePath = [string]$process.ExecutablePath
        })
    }
    return $editors.ToArray()
}

function Test-ExecutableMatchesVersion {
    param(
        [string]$Executable,
        [string]$ExpectedVersion
    )

    if ([string]::IsNullOrWhiteSpace($Executable)) { return $false }
    try {
        $productVersion = (Get-Item -LiteralPath $Executable).VersionInfo.ProductVersion
        $versionMatches = (-not [string]::IsNullOrWhiteSpace($productVersion)) -and $productVersion.StartsWith($ExpectedVersion, [StringComparison]::OrdinalIgnoreCase)
        if ($versionMatches) {
            return $true
        }
    }
    catch {
    }

    $normalized = [IO.Path]::GetFullPath($Executable).Replace('/', '\')
    return $normalized.IndexOf("\$ExpectedVersion\", [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Resolve-UnityExecutable {
    param(
        [string]$ExplicitPath,
        [string]$ExpectedVersion,
        [object[]]$OpenEditors
    )

    $candidates = New-Object Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolvedExplicitPath = [IO.Path]::GetFullPath($ExplicitPath.Trim())
        if (-not (Test-Path -LiteralPath $resolvedExplicitPath -PathType Leaf)) {
            throw "The explicit Unity executable does not exist: $resolvedExplicitPath"
        }
        if (-not (Test-ExecutableMatchesVersion -Executable $resolvedExplicitPath -ExpectedVersion $ExpectedVersion)) {
            throw "The explicit Unity executable does not match project version ${ExpectedVersion}: $resolvedExplicitPath"
        }
        return $resolvedExplicitPath
    }
    foreach ($editor in $OpenEditors) {
        if (-not [string]::IsNullOrWhiteSpace([string]$editor.executablePath)) {
            $candidates.Add([string]$editor.executablePath)
        }
    }

    foreach ($basePath in @(
        'C:\Program Files\Unity\Hub\Editor',
        'D:\UnityEdi',
        'D:\UnityEditorDir'
    )) {
        $candidate = Join-Path $basePath "$ExpectedVersion\Editor\Unity.exe"
        $candidates.Add($candidate)
    }

    $command = Get-Command Unity.exe -ErrorAction SilentlyContinue
    if ($null -ne $command -and -not [string]::IsNullOrWhiteSpace([string]$command.Source)) {
        $candidates.Add([string]$command.Source)
    }

    foreach ($candidate in @($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        if (Test-ExecutableMatchesVersion -Executable $candidate -ExpectedVersion $ExpectedVersion) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Unity $ExpectedVersion was not found. Pass its exact Unity.exe path with -UnityPath."
}

function Resolve-OutputPath {
    param(
        [string]$Candidate,
        [string]$DefaultPath,
        [string]$Root
    )

    $path = if ([string]::IsNullOrWhiteSpace($Candidate)) { $DefaultPath } else { $Candidate }
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
    $resolved = [IO.Path]::GetFullPath($path)
    $rootPrefix = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -and $resolved -ne $Root) {
        throw "Unity output path must remain inside the project root: $resolved"
    }
    return $resolved
}

function Write-Report {
    param(
        [object]$Report,
        [switch]$AsJson
    )

    if ($AsJson) { $Report | ConvertTo-Json -Depth 8 }
    else { $Report | Format-List }
}

function Read-TestResultSummary {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{
            valid = $false
            total = 0
            passed = 0
            failed = 0
            skipped = 0
            inconclusive = 0
            result = ''
            error = 'Unity did not create the requested Test Runner XML.'
        }
    }

    try {
        [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        $root = $document.DocumentElement
        if ($null -eq $root) { throw 'The XML has no document element.' }

        $totalText = $root.GetAttribute('testcasecount')
        if ([string]::IsNullOrWhiteSpace($totalText)) { $totalText = $root.GetAttribute('total') }
        $total = 0
        $passed = 0
        $failed = 0
        $skipped = 0
        $inconclusive = 0
        [void][int]::TryParse($totalText, [ref]$total)
        [void][int]::TryParse($root.GetAttribute('passed'), [ref]$passed)
        [void][int]::TryParse($root.GetAttribute('failed'), [ref]$failed)
        [void][int]::TryParse($root.GetAttribute('skipped'), [ref]$skipped)
        [void][int]::TryParse($root.GetAttribute('inconclusive'), [ref]$inconclusive)

        return [pscustomobject]@{
            valid = $true
            total = $total
            passed = $passed
            failed = $failed
            skipped = $skipped
            inconclusive = $inconclusive
            result = $root.GetAttribute('result')
            error = ''
        }
    }
    catch {
        return [pscustomobject]@{
            valid = $false
            total = 0
            passed = 0
            failed = 0
            skipped = 0
            inconclusive = 0
            result = ''
            error = "Cannot parse Test Runner XML: $($_.Exception.Message)"
        }
    }
}

$projectRootResolved = Resolve-ProjectRoot -Candidate $ProjectRoot
$projectVersion = Get-ProjectUnityVersion -Root $projectRootResolved
$openEditors = @(Get-OpenProjectEditors -Root $projectRootResolved)
$lockPath = Join-Path $projectRootResolved 'Temp\UnityLockfile'
$lockPresent = Test-Path -LiteralPath $lockPath -PathType Leaf
$unityExecutable = Resolve-UnityExecutable `
    -ExplicitPath $UnityPath `
    -ExpectedVersion $projectVersion `
    -OpenEditors $openEditors

$status = [pscustomobject]@{
    evidence = 'unity-cli-status'
    executor = 'Unity.exe (official Unity Editor CLI)'
    launcher = 'ES project launcher; it does not replace Unity CLI.'
    projectRoot = $projectRootResolved
    projectVersion = $projectVersion
    unityExecutable = $unityExecutable
    editorOpen = $openEditors.Count -gt 0
    editorProcesses = $openEditors
    lockPresent = $lockPresent
    safeToLaunchBatchMode = $openEditors.Count -eq 0 -and -not $lockPresent
}

if ($Mode -eq 'Status') {
    Write-Report -Report $status -AsJson:$Json
    exit 0
}

if ($openEditors.Count -gt 0 -or $lockPresent) {
    $blocked = [pscustomobject]@{
        evidence = 'unity-cli'
        mode = $Mode
        succeeded = $false
        blocked = $true
        reason = 'The project is already open in Unity or has an active Unity lock. Close the Editor cleanly before starting batchmode for the same project.'
        status = $status
    }
    Write-Report -Report $blocked -AsJson:$Json
    exit 3
}

$outputRoot = Join-Path $projectRootResolved 'Temp\ESUnityCLI'
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$defaultLog = Join-Path $outputRoot "$timestamp-$Mode.log"
$resolvedLogPath = Resolve-OutputPath -Candidate $LogPath -DefaultPath $defaultLog -Root $projectRootResolved
$logDirectory = Split-Path -Parent $resolvedLogPath
if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $projectRootResolved,
    '-logFile', $resolvedLogPath
)

$resolvedResultsPath = $null
if ($Mode -eq 'Compile') {
    $arguments += '-quit'
}
else {
    $platform = if ($Mode -eq 'EditModeTests') { 'EditMode' } else { 'PlayMode' }
    $defaultResults = Join-Path $outputRoot "$timestamp-$platform-results.xml"
    $resolvedResultsPath = Resolve-OutputPath -Candidate $ResultsPath -DefaultPath $defaultResults -Root $projectRootResolved
    $resultsDirectory = Split-Path -Parent $resolvedResultsPath
    if (-not (Test-Path -LiteralPath $resultsDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
    }

    $arguments += @('-runTests', '-testPlatform', $platform, '-testResults', $resolvedResultsPath)
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('-testFilter', $TestFilter.Trim())
    }
    if (-not [string]::IsNullOrWhiteSpace($TestCategory)) {
        $arguments += @('-testCategory', $TestCategory.Trim())
    }
    if ($AssemblyName -and $AssemblyName.Count -gt 0) {
        $assemblyValue = (@($AssemblyName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ';')
        if (-not [string]::IsNullOrWhiteSpace($assemblyValue)) {
            $arguments += @('-assemblyNames', $assemblyValue)
        }
    }
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $unityExecutable @arguments
$exitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

$testResultSummary = $null
$succeeded = $exitCode -eq 0
if ($null -ne $resolvedResultsPath) {
    $testResultSummary = Read-TestResultSummary -Path $resolvedResultsPath
    $succeeded = $succeeded -and $testResultSummary.valid -and $testResultSummary.total -gt 0 -and $testResultSummary.failed -eq 0
}

$report = [pscustomobject]@{
    evidence = 'unity-cli-batchmode'
    executor = 'Unity.exe (official Unity Editor CLI)'
    launcher = 'ES project launcher; the reported process and exit code come from Unity.exe.'
    candidateEvidence = if ($Mode -eq 'Compile') { 'unity-editor-compile' } else { 'unity-test-runner' }
    warning = if ($Mode -eq 'Compile') {
        'A zero Unity process exit code is batchmode compile/import evidence. Review the log before claiming a clean Console or successful domain reload.'
    } else {
        'The Test Runner XML and Unity log are the authority for named test results. This does not prove PlayMode observation, Profiler, Player, IL2CPP, or release acceptance.'
    }
    mode = $Mode
    projectRoot = $projectRootResolved
    projectVersion = $projectVersion
    unityExecutable = $unityExecutable
    exitCode = $exitCode
    succeeded = $succeeded
    blocked = $false
    logPath = $resolvedLogPath
    resultsPath = $resolvedResultsPath
    testResults = $testResultSummary
    arguments = $arguments
}

Write-Report -Report $report -AsJson:$Json
if (-not $report.succeeded) { exit 1 }
