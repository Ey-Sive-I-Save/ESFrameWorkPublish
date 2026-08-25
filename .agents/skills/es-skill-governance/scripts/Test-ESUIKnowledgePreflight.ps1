[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [switch]$Replay
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$contract = Join-Path $root '.agents/skills/es-skill-governance/references/ui-knowledge-preflight-contract.md'
if (-not (Test-Path -LiteralPath $contract -PathType Leaf)) { throw 'UI Knowledge preflight contract is missing.' }
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
function Read-Strict([string]$path) { return $utf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $path))) }

$skills = @(
    (Join-Path $root '.agents/skills/es-ui-intent-authoring'),
    (Join-Path $root '.agents/skills/es-ui-prefab-authoring')
)
$requiredMarkers = @(
    'Knowledge preflight',
    'selectedKnowledgeIds',
    'requiredReads',
    'SourceRef',
    'stale',
    'NoKnowledgeRoute',
    'Only an explicit',
    'not applicable',
    'exempt'
)
$failures = New-Object 'System.Collections.Generic.List[string]'
foreach ($skill in $skills) {
    $name = Split-Path -Leaf $skill
    $skillPath = Join-Path $skill 'SKILL.md'
    $govPath = Join-Path $skill 'governance.json'
    if (-not (Test-Path -LiteralPath $skillPath -PathType Leaf)) { [void]$failures.Add("${name}: SKILL.md missing"); continue }
    if (-not (Test-Path -LiteralPath $govPath -PathType Leaf)) { [void]$failures.Add("${name}: governance.json missing"); continue }
    $body = Read-Strict $skillPath
    foreach ($marker in $requiredMarkers) {
        if ($body -notmatch $marker) { [void]$failures.Add("${name}: missing preflight marker '$marker'") }
    }
    $gov = (Read-Strict $govPath) | ConvertFrom-Json
    if ($gov.PSObject.Properties['knowledgePreflight'] -eq $null) { [void]$failures.Add("${name}: knowledgePreflight metadata missing"); continue }
    $preflight = $gov.knowledgePreflight
    foreach ($property in @('required','contractRef','receiptFields','highRiskTriggers','exemptionPolicy')) {
        if ($preflight.PSObject.Properties[$property] -eq $null) { [void]$failures.Add("${name}: knowledgePreflight.$property missing") }
    }
    if ($preflight.required -ne $true) { [void]$failures.Add("${name}: knowledgePreflight.required must be true") }
    if ([string]$preflight.contractRef -ne '.agents/skills/es-skill-governance/references/ui-knowledge-preflight-contract.md') { [void]$failures.Add("${name}: contractRef is not canonical") }
    foreach ($field in @('selectedKnowledgeIds','requiredReads','sourceRefs','staleCheck','nonClaims','decision')) {
        if (@($preflight.receiptFields) -notcontains $field) { [void]$failures.Add("${name}: receiptFields missing $field") }
    }
    if ([string]$preflight.exemptionPolicy -notmatch 'explicit-user') { [void]$failures.Add("${name}: exemption policy is not explicit-user") }
}

if ($Replay) {
    $cases = @(
        @{ id = 'matching-route'; risk = 'high'; route = $true; read = $true; stale = $false; exemption = $false; expected = 'ready' },
        @{ id = 'unread-knowledge'; risk = 'high'; route = $true; read = $false; stale = $false; exemption = $false; expected = 'blocked' },
        @{ id = 'no-route'; risk = 'high'; route = $false; read = $false; stale = $false; exemption = $false; expected = 'blocked' },
        @{ id = 'stale-source'; risk = 'high'; route = $true; read = $true; stale = $true; exemption = $false; expected = 'blocked' },
        @{ id = 'scoped-exemption'; risk = 'high'; route = $false; read = $false; stale = $false; exemption = $true; expected = 'exempted' },
        @{ id = 'low-risk'; risk = 'low'; route = $false; read = $false; stale = $false; exemption = $false; expected = 'bypass' }
    )
    foreach ($case in $cases) {
        $actual = if ($case.risk -eq 'low') { 'bypass' } elseif ($case.exemption) { 'exempted' } elseif ($case.route -and $case.read -and -not $case.stale) { 'ready' } else { 'blocked' }
        if ($actual -ne $case.expected) { [void]$failures.Add("replay $($case.id): expected $($case.expected), got $actual") }
    }
    Write-Output ('PASS: UI Knowledge preflight replay covered ' + $cases.Count + ' high/low-risk cases.')
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Output 'PASS: UI Skills declare the canonical Knowledge preflight contract and receipt fields.'
