[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath = 'ES/Output/SkillPortfolioReceipt.json',
    [ValidateRange(10,600)][int]$InnerValidationTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$skillsRoot = Join-Path $root '.agents\skills'
$catalog = Join-Path $root '.agents\SKILL_CATALOG.yaml'
$validator = Join-Path $skillsRoot 'es-skill-validator\scripts\Invoke-ESSkillValidation.ps1'
$contract = Join-Path $skillsRoot 'es-skill-governance\scripts\Test-ESSkillContract.ps1'
$evidenceBindings = Join-Path $skillsRoot 'es-skill-governance\scripts\Test-ESEvidenceContractBindings.ps1'
$pathBoundary = Join-Path $skillsRoot 'es-skill-governance\scripts\ESPathBoundary.Common.ps1'
$decisionModule = Join-Path $skillsRoot 'es-skill-validator\scripts\ESPortfolioDecision.psm1'
if (-not (Test-Path -LiteralPath $skillsRoot -PathType Container)) { Write-Error 'Missing .agents/skills'; exit 2 }
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { Write-Error 'Missing Skill Validator'; exit 2 }
. $pathBoundary
Import-Module $decisionModule -Force

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
$required = @('SKILL.md','agents/openai.yaml','governance.json','evidence-contract.binding.json','scripts/Test-ESSkillEvidence.ps1')
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

$previousErrorAction=$ErrorActionPreference
$ErrorActionPreference='Continue'
& powershell -NoProfile -File $evidenceBindings -ProjectRoot $root -MaxSkills 128 -Quiet *> $null
$evidenceBindingsExit=$LASTEXITCODE
$ErrorActionPreference=$previousErrorAction
if($evidenceBindingsExit -ne 0){$contractFailures += 'central-evidence-bindings'}

$innerRelative = Join-Path 'ES/Output' 'SkillPortfolioInnerValidation.json'
$innerPath = Join-Path $root $innerRelative
$innerBeforeHash = if (Test-Path -LiteralPath $innerPath -PathType Leaf) { (Get-FileHash -LiteralPath $innerPath -Algorithm SHA256).Hash } else { '' }
$previousErrorAction=$ErrorActionPreference
$ErrorActionPreference='Continue'
$innerJob = Start-Job -ScriptBlock {
    param($validatorPath,$projectRoot,$profile,$reportPath)
    $output = (& powershell -NoProfile -File $validatorPath -ProjectRoot $projectRoot -Profile $profile -ReportPath $reportPath 2>&1 | Out-String).Trim()
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
} -ArgumentList $validator,$root,@('Full'),$innerRelative
$innerError = ''
$innerCompleted = Wait-Job -Job $innerJob -Timeout $InnerValidationTimeoutSeconds
$innerTimedOut = ($null -eq $innerCompleted)
if ($innerTimedOut) {
    Stop-Job -Job $innerJob -ErrorAction SilentlyContinue
    $innerExit = 124
} else {
    $innerResult = Receive-Job -Job $innerJob -ErrorAction SilentlyContinue | Select-Object -Last 1
    $innerExit = if ($innerResult -and $innerResult.PSObject.Properties['ExitCode']) { [int]$innerResult.ExitCode } else { 1 }
    $innerError = if ($innerResult -and $innerResult.PSObject.Properties['Output']) { [string]$innerResult.Output } else { '' }
}
if ($innerTimedOut) { $innerError = "Inner validation exceeded $InnerValidationTimeoutSeconds seconds." }
Remove-Job -Job $innerJob -Force -ErrorAction SilentlyContinue
$ErrorActionPreference=$previousErrorAction
$inner = $null
 $innerAfterHash = if (Test-Path -LiteralPath $innerPath -PathType Leaf) { (Get-FileHash -LiteralPath $innerPath -Algorithm SHA256).Hash } else { '' }
if (-not $innerTimedOut -and (Test-Path -LiteralPath $innerPath -PathType Leaf) -and $innerAfterHash -ne $innerBeforeHash) {
    try { $inner = Get-Content -Raw -Encoding UTF8 $innerPath | ConvertFrom-Json } catch { $inner = $null }
}
$contractFailures = @($contractFailures)
if ($innerTimedOut) { $contractFailures += 'inner-validator-timeout' }

$innerResults = if ($inner) { @($inner.results) } else { @() }
$blocked = @($innerResults | Where-Object { $_.status -eq 'blocked' })
$failed = @($innerResults | Where-Object { $_.status -eq 'failed' })
$notRun = @($innerResults | Where-Object { $_.status -eq 'not-run' })
$review = @($innerResults | Where-Object { $_.status -eq 'review' })
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
$hardFailureCount = $blocked.Count + $failed.Count + $notRun.Count + $resourceFailures.Count + $contractFailures.Count
$decision = Resolve-ESPortfolioDecision -InnerResultAvailable ($null -ne $inner) -HardFailureCount $hardFailureCount -EvidencePendingCount $evidencePendingCount -ValidatorReviewCount $review.Count
$status = [string]$decision.status
$innerReportHash = if (Test-Path -LiteralPath $innerPath -PathType Leaf) { (Get-FileHash -LiteralPath $innerPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { '' }
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
                       '.agents/skills/es-skill-validator/scripts/Test-ESSkillPortfolio.ps1',
                       '.agents/skills/es-skill-validator/scripts/ESPortfolioDecision.psm1',
                       '.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1',
                       '.agents/skills/es-skill-governance/scripts/Test-ESEvidenceContractBindings.ps1'
                   )
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    catalogHash = (Get-FileHash -LiteralPath $catalog -Algorithm SHA256).Hash.ToLowerInvariant()
    resourceIndexHash = (Get-FileHash -LiteralPath (Join-Path $root '.agents/SKILL_RESOURCE_INDEX.yaml') -Algorithm SHA256).Hash.ToLowerInvariant()
    validatorHash = (Get-FileHash -LiteralPath $validator -Algorithm SHA256).Hash.ToLowerInvariant()
    innerReportPath = $innerRelative.Replace('\','/')
    innerReportHash = $innerReportHash
    innerResultAvailable = ($null -ne $inner)
    innerExitCode = $innerExit
    innerTimedOut = $innerTimedOut
    innerError = $innerError
    skillCount = $skillDirs.Count
    staticReadyCount = $staticReadyCount
    evidencePendingCount = $evidencePendingCount
    runtimeRequiredCount = $runtimeRequiredCount
    runtimeNotRunCount = $runtimeNotRunCount
    validatorReviewCount = $review.Count
    decisionStatus = [string]$decision.decisionStatus
    effect = [string]$decision.effect
    staticStatus = if ($status -eq 'blocked') { 'static-blocked' } elseif ($staticReadyCount -eq $skillDirs.Count) { 'static-passed' } else { 'static-partial' }
    evidenceStatus = if ($evidencePendingCount -gt 0) { 'evidence-pending' } else { 'evidence-closed' }
    runtimeStatus = if ($runtimeRequiredCount -gt 0) { 'runtime-not-run' } else { 'not-applicable' }
    blockingLayer = [string]$decision.blockingLayer
    contractFailures = @($contractFailures)
    resourceFailures = @($resourceFailures)
    validatorFailures = @($failed | ForEach-Object { $_.skill + ':' + $_.profile })
    validatorBlocked = @($blocked | ForEach-Object { $_.skill + ':' + $_.profile })
    validatorNotRun = @($notRun | ForEach-Object { $_.skill + ':' + $_.profile })
    sourceRefHashes = [ordered]@{}
    portfolioHash = ''
    result = if ($status -eq 'passed') { 'All Skill static assets passed the portfolio gate.' } elseif ($status -eq 'review') { 'Static assets have no hard failure; evidence review remains claim-limiting.' } else { 'Portfolio gate blocked by a scoped contract, resource, or validator failure.' }
}
$sourceRefs=@($receipt.sourceRefs)
foreach($ref in $sourceRefs){$receipt.sourceRefHashes[$ref]=(Get-FileHash -LiteralPath (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
$receipt.portfolioHash = Get-ESPortfolioProjectionHash -Receipt $receipt
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
