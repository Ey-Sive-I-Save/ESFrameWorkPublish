[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ProjectRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
function Resolve-ProjectFile([string]$relativePath) {
    if ([string]::IsNullOrWhiteSpace($relativePath) -or [IO.Path]::IsPathRooted($relativePath)) {
        throw "Path must be project-relative: $relativePath"
    }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $relativePath.Replace('/', '\')))
    $prefix = $root.TrimEnd([char]92, [char]47) + [char]92
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes ProjectRoot: $relativePath"
    }
    return $full
}
$policyPath = Resolve-ProjectFile 'ES/AISpace/Public/LOCAL_TEMP_POLICY.json'
$readmePath = Resolve-ProjectFile 'ES/AISpace/README.md'
if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) { throw 'LOCAL_TEMP_POLICY.json is missing.' }
if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) { throw 'ES/AISpace/README.md is missing.' }
$policy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$readme = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8
$errors = [System.Collections.Generic.List[string]]::new()
if ([int]$policy.schemaVersion -ne 1 -or [string]$policy.policyId -ne 'es-aispace-content-placement') { [void]$errors.Add('Policy identity is invalid.') }
if ([string]$policy.defaultRoot -ne 'ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>') { [void]$errors.Add('Default Local content root is invalid.') }
foreach ($category in @('Screenshots','Captures','Cache','Scratch','Exports')) {
    if (@($policy.allowedCategories) -notcontains $category) { [void]$errors.Add("Missing allowed category: $category") }
}
foreach ($forbiddenRoot in @('Assets/ES/AISpace/Local','Assets/ES/Space/Local')) {
    if (@($policy.forbiddenRoots) -notcontains $forbiddenRoot) { [void]$errors.Add("$forbiddenRoot must remain forbidden.") }
}
if ($readme -notmatch 'Local/<category>/<YYYYMMDD>' -or $readme -notmatch 'Assets/Screenshots') { [void]$errors.Add('README does not expose classification-first date routing and Assets screenshot distinction.') }
$absoluteValues = @($policy.PSObject.Properties | ForEach-Object { [string]$_.Value } | Where-Object { $_ -match '^[A-Za-z]:[\\/]' })
if ($absoluteValues.Count -gt 0) { [void]$errors.Add('Policy contains a machine-specific absolute path.') }
$status = if ($errors.Count -eq 0) { 'passed' } else { 'failed' }
[ordered]@{ schemaVersion = 1; validator = 'es-local-temp-policy'; status = $status; policyPath = 'ES/AISpace/Public/LOCAL_TEMP_POLICY.json'; findings = @($errors) } | ConvertTo-Json -Depth 4
if ($errors.Count -gt 0) { exit 1 }
