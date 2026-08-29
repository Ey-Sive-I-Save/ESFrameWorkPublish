[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestText,
    [string]$ProjectPath = '',
    [string]$SessionId = '',
    [string]$SessionPath = '',
    [string]$ArchivePath = '',
    [string]$TaskKey = '',
    [string]$ResponsibilityKey = '',
    [string]$TabTitle = '',
    [string]$TaskPrompt = '',
    [ValidateSet('Auto','CurrentWindow','ProjectWindow','NewWindow','PlainCmd')][string]$TerminalMode = 'ProjectWindow',
    [string]$TerminalWindowName = 'ESFramework',
    [switch]$SkipHooks,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$launcher = Join-Path $PSScriptRoot 'Start-ESCodexSession.ps1'
$readOnly = Join-Path $PSScriptRoot 'New-ESCodexReadOnlyContext.ps1'
$recycle = Join-Path $PSScriptRoot 'Invoke-ESCodexCurrentTabRecycle.ps1'
$handoff = Join-Path $projectRoot 'ES\AI协作历程（Codex）\Tools\Complete-ESCodexHandoff.ps1'

# The caller's CMD working directory is not authoritative. If the request
# contains a Windows path, resolve and validate it before dispatching.
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $pathMatches = [regex]::Matches($RequestText, '(?i)([A-Z]:\\[^\r\n"''`]+)')
    foreach ($match in $pathMatches) {
        $candidate = $match.Groups[1].Value.TrimEnd(' ', '.', ',')
        $isUnityProject = (Test-Path -LiteralPath $candidate -PathType Container -ErrorAction SilentlyContinue) -and `
            (Test-Path -LiteralPath (Join-Path $candidate 'ProjectSettings\ProjectVersion.txt') -PathType Leaf) -and `
            (Test-Path -LiteralPath (Join-Path $candidate 'Assets') -PathType Container) -and `
            (Test-Path -LiteralPath (Join-Path $candidate 'Packages') -PathType Container)
        if ($isUnityProject) {
            $ProjectPath = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = $projectRoot }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

# Explicit historical-context phrases select Resume/Handoff. Everything else,
# including temporary task splitting, safely defaults to an independent New.
$readOnlyPhrase = -join @([char]0x53EA,[char]0x8BFB,[char]0x6062,[char]0x590D)
$readOnlyMarker = -join @([char]0x53EA,[char]0x8BFB)
$readOnlyMatch = ($RequestText.Contains($readOnlyPhrase) -or $RequestText.Contains($readOnlyMarker) -or $RequestText -match '(?i)read[- ]?only\s+restore|new window.*read[- ]?only')
$recycleChinese = @(
    (-join @([char]0x539F,[char]0x9875,[char]0x7B7E,[char]0x81EA,[char]0x4EA4,[char]0x63A5)),
    (-join @([char]0x5F53,[char]0x524D,[char]0x9875,[char]0x7B7E,[char]0x91CD,[char]0x5EFA)),
    (-join @([char]0x5F53,[char]0x524D,[char]0x9875,[char]0x7B7E,[char]0x6E05,[char]0x6389)),
    (-join @([char]0x4F1A,[char]0x8BDD,[char]0x518D,[char]0x751F))
)
$currentTabMarker = -join @([char]0x5F53,[char]0x524D,[char]0x9875,[char]0x7B7E)
$recycleActionMarker = -join @([char]0x91CD,[char]0x5EFA)
$recycleMatch = (($recycleChinese | Where-Object { $RequestText.Contains($_) }).Count -gt 0 -or ($RequestText.Contains($currentTabMarker) -and $RequestText.Contains($recycleActionMarker)) -or $RequestText -match '(?i)current[- ]?tab.*(recycle|handoff|refresh)|same[- ]?tab.*(rebuild|handoff)')
$handoffMatch = $RequestText -match '(?i)handoff|handover|\u5B8C\u6574\u4EA4\u63A5|\u5386\u53F2\u4E0A\u4E0B\u6587|\u5E26\u4E0A\u65E7\u4E0A\u4E0B\u6587|\u8BFB\u53D6\u5F52\u6863'
$resumeMatch = $RequestText -match '(?i)\u6062\u590D\u539F\u4F1A\u8BDD|\u7EE7\u7EED\u539F\u4F1A\u8BDD|\u627E\u56DE\u4E4B\u524D\u4F1A\u8BDD|\u6062\u590D\s+session'
$temporaryMatch = $RequestText -match '(?i)\u4E34\u65F6\u5206\u5DE5|\u5206\u51FA\u4E00\u4E2A\u7A97\u53E3|\u65B0\u5EFA\u7A97\u53E3|\u72EC\u7ACB\u5904\u7406|\u81EA\u884C\u8BFB\u9879\u76EE|\u4E0D\u8981\u6062\u590D'

if ($recycleMatch) { $intent = 'CurrentTabRecycle' }
elseif ($readOnlyMatch) { $intent = 'ReadOnlyRestore' }
elseif ($temporaryMatch) { $intent = 'New' }
elseif ($handoffMatch) { $intent = 'Handoff' }
elseif ($resumeMatch) { $intent = 'Resume' }
else { $intent = 'New' }

if ($intent -eq 'Handoff') {
    if ([string]::IsNullOrWhiteSpace($SessionPath) -or [string]::IsNullOrWhiteSpace($ArchivePath)) {
        [pscustomobject]@{ intent='Handoff'; status='NeedsInputs'; blockingReason='Formal Handoff requires SessionPath and ArchivePath'; selectedCommand='Complete-ESCodexHandoff.ps1'; resolvedProjectPath=$ProjectPath; request=$RequestText } | ConvertTo-Json -Depth 5
        exit 0
    }
    Push-Location -LiteralPath $ProjectPath
    try { & $handoff -SessionPath $SessionPath -ArchivePath $ArchivePath -ProjectPath $ProjectPath -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TabTitle $TabTitle -TaskPrompt $TaskPrompt -TerminalMode $TerminalMode -OpenNew -DryRun:$DryRun }
    finally { Pop-Location }
    exit $LASTEXITCODE
}

if ($intent -eq 'ReadOnlyRestore') {
    if ([string]::IsNullOrWhiteSpace($SessionPath)) {
        [pscustomobject]@{ intent='ReadOnlyRestore'; status='NeedsInputs'; blockingReason='Read-only restore requires an exact historical transcript SessionPath (or use New-ESProjectSemanticArchive output path)'; selectedCommand='New-ESCodexReadOnlyContext.ps1'; resolvedProjectPath=$ProjectPath; request=$RequestText } | ConvertTo-Json -Depth 5
        exit 0
    }
    Push-Location -LiteralPath $ProjectPath
    try { & $readOnly -SourcePath $SessionPath -ProjectPath $ProjectPath -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TabTitle $TabTitle -TaskPrompt $TaskPrompt -TerminalMode $TerminalMode -SkipHooks:$SkipHooks -DryRun:$DryRun }
    finally { Pop-Location }
    exit $LASTEXITCODE
}

if ($intent -eq 'CurrentTabRecycle') {
    Push-Location -LiteralPath $ProjectPath
    try { & $recycle -ProjectPath $ProjectPath -TaskPrompt $TaskPrompt -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TabTitle $TabTitle -TerminalMode $TerminalMode -TerminalWindowName $TerminalWindowName -SkipHooks:$SkipHooks -DryRun:$DryRun }
    finally { Pop-Location }
    exit $LASTEXITCODE
}

$common = @{ Mode=$intent; ProjectPath=$ProjectPath; SessionId=$SessionId; TaskKey=$TaskKey; ResponsibilityKey=$ResponsibilityKey; TabTitle=$TabTitle; TaskPrompt=$TaskPrompt; TerminalMode=$TerminalMode; SkipHooks=$SkipHooks; DryRun=$DryRun }
Push-Location -LiteralPath $ProjectPath
try { & $launcher @common }
finally { Pop-Location }
exit $LASTEXITCODE
