[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Create','Get','VerifySources','SubmitEvidence','Evaluate','Complete','SetDelivery','Transition','Integrity')]
    [string]$Action,
    [Parameter(Mandatory=$true)][string]$InputPath,
    [string]$ProjectRoot
)
$ErrorActionPreference='Stop'
$fixedProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path.TrimEnd('\','/')
if(-not [string]::IsNullOrWhiteSpace($ProjectRoot)){
    $resolvedProjectRoot=(Resolve-Path -LiteralPath ([IO.Path]::GetFullPath($ProjectRoot))).Path.TrimEnd('\','/')
    $separator=[IO.Path]::DirectorySeparatorChar
    $isExactProjectRoot=(($resolvedProjectRoot+$separator).StartsWith($fixedProjectRoot+$separator,[StringComparison]::OrdinalIgnoreCase)-and$resolvedProjectRoot.Length-eq$fixedProjectRoot.Length)
    if(-not$isExactProjectRoot){throw 'ProjectRoot escapes the Skill-bound project root.'}
}
$entry=Join-Path $fixedProjectRoot 'ES/Automation/TaskContextRuntime/Invoke-ESTaskContextRuntime.ps1'
if(-not(Test-Path -LiteralPath $entry -PathType Leaf)){throw 'TaskContextRuntime platform entry point is missing.'}
& $entry -Action $Action -InputPath $InputPath -ProjectRoot $fixedProjectRoot
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
