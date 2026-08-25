[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath = 'ES/Output/SkillPortfolioReceipt.json'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$skillsRoot = Join-Path $root '.agents\skills'
$catalog = Join-Path $root '.agents\SKILL_CATALOG.yaml'
$validator = Join-Path $skillsRoot 'es-skill-validator\scripts\Invoke-ESSkillValidation.ps1'
$contract = Join-Path $skillsRoot 'es-skill-governance\scripts\Test-ESSkillContract.ps1'
$pathBoundary = Join-Path $skillsRoot 'es-skill-governance\scripts\ESPathBoundary.Common.ps1'
if (-not (Test-Path -LiteralPath $skillsRoot -PathType Container)) { Write-Error 'Missing .agents/skills'; exit 2 }
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { Write-Error 'Missing Skill Validator'; exit 2 }
. $pathBoundary

function Resolve-ESPortfolioOutputPath([string]$Candidate) {
    $target = Resolve-ESContainedRelativePath -Candidate $Candidate -ContainerRoot $root -Label 'ReportPath'
    if (-not $target.RelativePath.StartsWith('ES/Output/', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ReportPath must remain below ES/Output.'
    }
    return $target
}

try {
    $reportTarget = Resolve-ESPortfolioOutputPath -Candidate $ReportPath
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 2
}
$reportRelative = $reportTarget.RelativePath

$skillDirs = @(Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name)
$required = @('SKILL.md','agents/openai.yaml','governance.json','references/evidence-receipt-contract.md','scripts/Test-ESSkillEvidence.ps1')
$resourceFailures = @()
$contractFailures = @()
foreach ($dir in $skillDirs) {
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $dir.FullName $relative) -PathType Leaf)) {
            $resourceFailures += [pscustomobject]@{ skill=$dir.Name; missing=$relative }
        }
    }
    $previousErrorAction=$ErrorActionPreference
    $ErrorActionPreference='Continue'
    & powershell -NoProfile -File $contract -SkillPath $dir.FullName -RequireGovernanceMetadata *> $null
    $ErrorActionPreference=$previousErrorAction
    if ($LASTEXITCODE -ne 0) { $contractFailures += $dir.Name }
}

$innerRelative = Join-Path 'ES/Output' 'SkillPortfolioInnerValidation.json'
$previousErrorAction=$ErrorActionPreference
$ErrorActionPreference='Continue'
& powershell -NoProfile -File $validator -ProjectRoot $root -Profile @('Full') -ReportPath $innerRelative *> $null
$innerExit = $LASTEXITCODE
$ErrorActionPreference=$previousErrorAction
$innerPath = Join-Path $root $innerRelative
$inner = $null
if (Test-Path -LiteralPath $innerPath -PathType Leaf) {
    try { $inner = Get-Content -Raw -Encoding UTF8 $innerPath | ConvertFrom-Json } catch { $inner = $null }
}

$innerResults = if ($inner) { @($inner.results) } else { @() }
$blocked = @($innerResults | Where-Object { $_.status -eq 'blocked' })
$failed = @($innerResults | Where-Object { $_.status -eq 'failed' })
$notRun = @($innerResults | Where-Object { $_.status -eq 'not-run' })
$skillNames=@($skillDirs|ForEach-Object Name)
$staticReadyCount=0
$evidencePendingCount=0
$runtimeRequiredCount=0
$runtimeNotRunCount=0
foreach($skillName in $skillNames){
    $skillResults=@($innerResults|Where-Object {[string]$_.skill -eq $skillName})
    $staticResults=@($skillResults|Where-Object {[string]$_.profile -in @('Structural','Governance','VerificationSemantics','StaticDeepReplay','Security','Semantic','Boundary')})
    if($staticResults.Count -gt 0 -and @($staticResults|Where-Object {[string]$_.status -in @('failed','blocked')}).Count -eq 0){$staticReadyCount++}
    $evidence=@($skillResults|Where-Object {[string]$_.profile -eq 'Evidence'})
    if(@($evidence|Where-Object {[string]$_.claimStatus -in @('EvidenceMissing','EvidenceStale','EvidenceUnbound') -or ([string]$_.status -in @('failed','blocked','not-run') -and [string]$_.claimStatus -notin @('NotApplicable','Passed'))}).Count -gt 0){$evidencePendingCount++}
    $govPath=Join-Path $skillsRoot ($skillName+'\governance.json')
    if(Test-Path -LiteralPath $govPath -PathType Leaf){try{$gov=Get-Content -Raw -Encoding UTF8 $govPath|ConvertFrom-Json;$runtime=@($gov.verificationProfiles.RuntimeAcceptance);if($runtime.Count -gt 0 -and [bool]$runtime[0].runtimeRequired){$runtimeRequiredCount++}}catch{}}
}
$runtimeNotRunCount=$runtimeRequiredCount
$status = if ($innerExit -eq 0 -and $resourceFailures.Count -eq 0 -and $contractFailures.Count -eq 0) { 'passed' } else { 'blocked' }
$receipt = [pscustomobject][ordered]@{
    skillName = 'es-skill-validator'
    case = 'portfolio-gate'
    status = $status
    evidenceLevel = 'S2'
    receiptPath = $reportRelative
    sourceRefs = @(
        '.agents/SKILL_CATALOG.yaml',
        '.agents/SKILL_RESOURCE_INDEX.yaml',
        '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1',
        '.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1'
    )
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    catalogHash = (Get-FileHash -LiteralPath $catalog -Algorithm SHA256).Hash.ToLowerInvariant()
    resourceIndexHash = (Get-FileHash -LiteralPath (Join-Path $root '.agents/SKILL_RESOURCE_INDEX.yaml') -Algorithm SHA256).Hash.ToLowerInvariant()
    validatorHash = (Get-FileHash -LiteralPath $validator -Algorithm SHA256).Hash.ToLowerInvariant()
    skillCount = $skillDirs.Count
    staticReadyCount = $staticReadyCount
    evidencePendingCount = $evidencePendingCount
    runtimeRequiredCount = $runtimeRequiredCount
    runtimeNotRunCount = $runtimeNotRunCount
    contractFailures = @($contractFailures)
    resourceFailures = @($resourceFailures)
    validatorFailures = @($failed | ForEach-Object { $_.skill + ':' + $_.profile })
    validatorBlocked = @($blocked | ForEach-Object { $_.skill + ':' + $_.profile })
    validatorNotRun = @($notRun | ForEach-Object { $_.skill + ':' + $_.profile })
    sourceRefHashes = [ordered]@{}
    portfolioHash = ''
    result = if ($status -eq 'passed') { 'All Skill assets passed the portfolio gate.' } else { 'Portfolio gate blocked; resolve every failure and security review before acceptance.' }
}
$sourceRefs=@($receipt.sourceRefs)
foreach($ref in $sourceRefs){$receipt.sourceRefHashes[$ref]=(Get-FileHash -LiteralPath (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
$projection=($receipt.catalogHash+'|'+$receipt.resourceIndexHash+'|'+$receipt.validatorHash+'|'+($receipt.contractFailures -join ',')+'|'+($receipt.resourceFailures.skill -join ',')+'|'+($receipt.validatorFailures -join ',')+'|'+($receipt.validatorBlocked -join ',')+'|'+($receipt.validatorNotRun -join ',')+'|'+$receipt.status)
$receipt.portfolioHash=([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($projection))|ForEach-Object ToString x2)-join ''
$report = $reportTarget.FullPath
$parent = Split-Path -Parent $report
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$report = (Resolve-ESPortfolioOutputPath -Candidate $reportRelative).FullPath
$json = $receipt | ConvertTo-Json -Depth 8
$temporary = "$report.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    [IO.File]::WriteAllText($temporary, $json, (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $report -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
$receipt | ConvertTo-Json -Depth 8
if ($status -ne 'passed') { exit 1 }
exit 0
