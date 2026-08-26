$script:ESInteractionProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$sharedPathBoundary = (Resolve-Path (Join-Path $script:ESInteractionProjectRoot '.agents/skills/es-skill-governance/scripts/ESPathBoundary.Common.ps1')).Path
. $sharedPathBoundary

function Get-ESInteractionProjectRoot {
    return $script:ESInteractionProjectRoot
}

function Resolve-ESInteractionManagedPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Candidate,
        [switch] $AllowSystemTemp,
        [switch] $ReportOnly,
        [switch] $RequireExistingFile,
        [string] $Label = 'Path'
    )

    if ([string]::IsNullOrWhiteSpace($Candidate) -or $Candidate -ne $Candidate.Trim()) {
        throw "$Label must be a non-empty path without surrounding whitespace."
    }

    $projectRoot = $script:ESInteractionProjectRoot.TrimEnd('\', '/')
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $full = if ([IO.Path]::IsPathRooted($Candidate)) {
        [IO.Path]::GetFullPath($Candidate)
    } else {
        [IO.Path]::GetFullPath([IO.Path]::Combine($projectRoot, $Candidate.Replace('/', '\')))
    }

    $containerRoot = $null
    $containerPrefix = $projectRoot + [IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($containerPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $containerRoot = $projectRoot
    } elseif ($AllowSystemTemp -and $full.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        $containerRoot = $tempRoot
    } else {
        throw "$Label is outside the project and approved system Temp roots."
    }

    $relative = $full.Substring($containerRoot.Length + 1).Replace('\', '/')
    $bounded = Resolve-ESContainedRelativePath -Candidate $relative -ContainerRoot $containerRoot -Label $Label
    if ($ReportOnly) {
        if ($containerRoot -cne $projectRoot -or $bounded.RelativePath -notmatch '(?i)^ES/Output/Interaction/.+') {
            throw "$Label must be under ES/Output/Interaction."
        }
    }
    if ($RequireExistingFile -and -not (Test-Path -LiteralPath $bounded.FullPath -PathType Leaf)) {
        throw "$Label does not identify an existing file."
    }
    return $bounded.FullPath
}

function Resolve-ESInteractionReportPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Candidate,
        [switch] $AllowSystemTemp,
        [string] $Label = 'ReportPath'
    )

    if ($AllowSystemTemp -and [IO.Path]::IsPathRooted($Candidate)) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        $full = [IO.Path]::GetFullPath($Candidate)
        if ($full.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            return Resolve-ESInteractionManagedPath -Candidate $Candidate -AllowSystemTemp -Label $Label
        }
    }
    return Resolve-ESInteractionManagedPath -Candidate $Candidate -ReportOnly -Label $Label
}

function Resolve-ESInteractionInputPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Candidate,
        [switch] $AllowSystemTemp,
        [string] $Label = 'InputPath'
    )

    return Resolve-ESInteractionManagedPath -Candidate $Candidate -AllowSystemTemp:$AllowSystemTemp -RequireExistingFile -Label $Label
}
