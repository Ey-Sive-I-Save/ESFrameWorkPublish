[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SkillPath,
    [switch] $RequireGovernanceMetadata,
    [ValidateSet('CurrentUserDirect','ManagedAIBrain')][string] $AuthorizationLane = 'CurrentUserDirect'
)

$ErrorActionPreference = 'Stop'
# Read-only contract validator: SkillPath is an explicit input and no project
# file is written, deleted, or executed by this script.
$resolved = (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$name = Split-Path -Leaf $resolved
$issues = New-Object 'System.Collections.Generic.List[string]'

function Add-Issue([string] $message) {
    [void]$issues.Add($message)
}

function Read-Utf8([string] $path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $encoding = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $encoding.GetString($bytes)
    if ($text.Contains([char]0xFFFD)) {
        Add-Issue "Replacement character found: $path"
    }
    return $text
}

if ($name -notmatch '^es-[a-z0-9]+(?:-[a-z0-9]+)*$') {
    Add-Issue "Invalid Skill directory name: $name"
}

$skillFile = Join-Path $resolved 'SKILL.md'
$yamlFile = Join-Path $resolved 'agents/openai.yaml'
$governanceFile = Join-Path $resolved 'governance.json'

if (-not (Test-Path -LiteralPath $skillFile -PathType Leaf)) { Add-Issue 'Missing SKILL.md' }
if (-not (Test-Path -LiteralPath $yamlFile -PathType Leaf)) { Add-Issue 'Missing agents/openai.yaml' }

if (Test-Path -LiteralPath $skillFile -PathType Leaf) {
    try { $skillText = Read-Utf8 $skillFile } catch { Add-Issue "Invalid UTF-8: $skillFile"; $skillText = $null }
    $frontmatter = '(?ms)^---\s*\r?\nname:\s*([^\r\n]+)\s*\r?\ndescription:\s*(.+?)\s*\r?\n---'
    if (-not $skillText -or $skillText -notmatch $frontmatter) {
        Add-Issue 'Invalid frontmatter: name and description required'
    } else {
        if ($Matches[1].Trim().Trim('"''') -ne $name) { Add-Issue 'Frontmatter name does not match directory' }
        if ($Matches[2].Trim().Trim('"''').Length -gt 1024) { Add-Issue 'Frontmatter description exceeds 1024 characters' }
    }
    if ($skillText -and $skillText -match '\[TODO:|\[TODO\]') { Add-Issue 'Template TODO remains in SKILL.md' }
    if ($name -in @('es-skill-governance', 'es-skill-creator')) {
        foreach ($tier in @('SmallTool','Workflow','Engineering')) {
            if ($skillText -notmatch [regex]::Escape($tier)) { Add-Issue "Missing tier taxonomy term: $tier" }
        }
    }
    if ($skillText) {
        foreach ($link in [regex]::Matches($skillText, '\]\((references/[^)#]+)\)')) {
            $referencePath = Join-Path $resolved $link.Groups[1].Value
            if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
                Add-Issue "Missing linked reference: $($link.Groups[1].Value)"
            }
        }
    }
}

if (Test-Path -LiteralPath $yamlFile -PathType Leaf) {
    try { $yamlText = Read-Utf8 $yamlFile } catch { Add-Issue "Invalid UTF-8: $yamlFile"; $yamlText = $null }
    if ($yamlText -and $yamlText -notmatch '(?m)^\s*display_name:\s*".+"\s*$') { Add-Issue 'Missing quoted display_name' }
    if ($yamlText -and $yamlText -notmatch '(?m)^\s*short_description:\s*".{25,64}"\s*$') { Add-Issue 'short_description must be 25-64 characters' }
    if ($yamlText -and $yamlText -notmatch ('(?m)^\s*default_prompt:.*' + [regex]::Escape($name))) { Add-Issue 'default_prompt must mention Skill name' }
}

if ($RequireGovernanceMetadata -and -not (Test-Path -LiteralPath $governanceFile -PathType Leaf)) {
    Add-Issue 'Missing governance.json while -RequireGovernanceMetadata is enabled'
}
if (Test-Path -LiteralPath $governanceFile -PathType Leaf) {
    try { $governanceText = Read-Utf8 $governanceFile } catch { Add-Issue "Invalid UTF-8: $governanceFile"; $governanceText = $null }
    if ($governanceText) {
        try { $governance = $governanceText | ConvertFrom-Json } catch { Add-Issue "governance.json is not valid JSON: $($_.Exception.Message)"; $governance = $null }
        if ($governance) {
            $requiredProperties = @('schemaVersion','skillName','tier','maturity','delivery','evidenceLevel','riskClass','executionMode','writePolicy','authorityClass','owner','acceptanceOwner','routeKeys','requiredCases','controlRefs')
            if ($AuthorizationLane -eq 'ManagedAIBrain') {
                $requiredProperties += @('requiresBrainPlan','allowDirectExecution')
            }
            foreach ($property in $requiredProperties) {
                if ($null -eq $governance.PSObject.Properties[$property]) { Add-Issue "governance.json missing property: $property" }
            }
            if ($governance.schemaVersion -ne 1) { Add-Issue 'governance.json schemaVersion must be 1' }
            if ([string]$governance.skillName -ne $name) { Add-Issue 'governance.json skillName does not match directory' }
            if ([string]$governance.tier -notmatch '^(SmallTool|Workflow|Engineering)$') { Add-Issue 'governance.json tier is invalid' }
            if ([string]$governance.maturity -notmatch '^(Proposed|Scaffolded|Implementing|Integrating|Verifying|Stable|Deprecated|Archived)$') { Add-Issue 'governance.json maturity is invalid' }
            if ([string]$governance.delivery -notmatch '^(Designed|Implemented-Unverified|Blocked|Failed|Accepted|Released)$') { Add-Issue 'governance.json delivery is invalid' }
            if ([string]$governance.evidenceLevel -notmatch '^S[0-6]$') { Add-Issue 'governance.json evidenceLevel must be S0-S6' }
            if ([string]$governance.riskClass -notmatch '^[a-z0-9][a-z0-9-]*$') { Add-Issue 'governance.json riskClass is invalid' }
            if ([string]$governance.executionMode -notmatch '^[a-z0-9][a-z0-9-]*$') { Add-Issue 'governance.json executionMode is invalid' }
            if ($AuthorizationLane -eq 'ManagedAIBrain' -and $governance.allowDirectExecution -eq $true) { Add-Issue 'governance.json allowDirectExecution must be false for ManagedAIBrain execution' }
            if ([string]$governance.authorityClass -notmatch '^(standard|core-governed|project-gate)$') { Add-Issue 'governance.json authorityClass is invalid' }
            if ([string]::IsNullOrWhiteSpace([string]$governance.owner)) { Add-Issue 'governance.json owner is required' }
            if ([string]::IsNullOrWhiteSpace([string]$governance.acceptanceOwner)) { Add-Issue 'governance.json acceptanceOwner is required' }
            if (@($governance.routeKeys).Count -eq 0) { Add-Issue 'governance.json routeKeys must not be empty' }
            if (@($governance.controlRefs).Count -eq 0) { Add-Issue 'governance.json controlRefs must not be empty' }
            if ($AuthorizationLane -eq 'ManagedAIBrain' -and [string]$governance.authorityClass -ne 'standard' -and $governance.requiresBrainPlan -ne $true) { Add-Issue 'governed authority classes require requiresBrainPlan=true for ManagedAIBrain execution' }
            if ([string]$governance.authorityClass -eq 'project-gate' -and [string]$governance.evidenceLevel -notmatch '^S[2-6]$') { Add-Issue 'project-gate requires evidenceLevel S2-S6' }

            $requiredCases = @('positive','invalid-input','denied-expansion','repeat-idempotency')
            if ([string]$governance.tier -in @('Workflow','Engineering')) { $requiredCases += 'interruption-recovery' }
            foreach ($case in $requiredCases) {
                if (@($governance.requiredCases) -notcontains $case) { Add-Issue "governance.json missing required case: $case" }
            }
            if ([string]$governance.tier -eq 'Engineering') {
                foreach ($control in @('identity','authority','risk','observability','recovery','performance','compatibility','supply-chain')) {
                    if (@($governance.requiredControls) -notcontains $control) { Add-Issue "Engineering governance missing control: $control" }
                }
            }
            foreach ($controlRef in @($governance.controlRefs)) {
                $relativeControlPath = ([string]$controlRef -split '#', 2)[0]
                if ([string]::IsNullOrWhiteSpace($relativeControlPath) -or -not (Test-Path -LiteralPath (Join-Path $resolved $relativeControlPath) -PathType Leaf)) {
                    Add-Issue "Missing governance control reference: $controlRef"
                }
            }
            $expectedControlHeading = switch ([string]$governance.tier) {
                'SmallTool' { '## SmallTool controls' }
                'Workflow' { '## Workflow controls' }
                'Engineering' { '## Engineering controls' }
            }
            $specializedHeadings = @()
            if ([string]$governance.executionMode -eq 'screen-spec-v3-materializer') {
                # ScreenSpec v3 is a responsibility-specific contract: its
                # capability model and scope boundary are the control surface.
                $specializedHeadings = @('## Capability model', '## Scope boundary')
            }
            $hasSpecializedHeading = ($specializedHeadings.Count -gt 0 -and @($specializedHeadings | Where-Object { $skillText -match [regex]::Escape($_) }).Count -gt 0)
            if ($skillText -and $expectedControlHeading -and $skillText -notmatch [regex]::Escape($expectedControlHeading) -and -not $hasSpecializedHeading) {
                Add-Issue "SKILL.md missing tier control heading: $expectedControlHeading"
            }
        }
    }
}

# Official Skill structure plus the governance contract and Creator license.
foreach ($entry in (Get-ChildItem -LiteralPath $resolved -Force)) {
    $entryName = ([string]$entry.Name).ToLowerInvariant()
    if ($entryName -notmatch '^(skill\.md|agents|references|scripts|assets|tests|governance\.json|license\.txt|session-product\.json|static-replay\.manifest\.json)$') {
        Add-Issue "Unexpected top-level entry: $($entry.Name)"
    }
}
foreach ($forbidden in @('README.md','INSTALLATION_GUIDE.md','CHANGELOG.md')) {
    if (Test-Path -LiteralPath (Join-Path $resolved $forbidden)) { Add-Issue "Forbidden extra file: $forbidden" }
}

if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Output "PASS: $name contract, UTF-8, frontmatter, metadata, references and layout (authorizationLane=$AuthorizationLane)"
exit 0
