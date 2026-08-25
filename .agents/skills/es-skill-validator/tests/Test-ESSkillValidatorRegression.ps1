[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$sourceRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$temp=Join-Path ([IO.Path]::GetTempPath()) ('es-skill-validator-fixture-'+[Guid]::NewGuid().ToString('N'))
$tempParent=Split-Path -Parent $temp
$outsideSourcePath=Join-Path $tempParent ('es-skill-validator-outside-'+[Guid]::NewGuid().ToString('N')+'.txt')
$escapedReportPath=Join-Path $tempParent ('es-skill-architecture-report-'+[Guid]::NewGuid().ToString('N')+'.json')
try {
    $skillRoot=Join-Path $temp '.agents/skills/es-fixture'
    $validatorRoot=Join-Path $temp '.agents/skills/es-skill-validator/scripts'
    $commandRoot=Join-Path $temp 'Assets/Plugins/ES/AICommands'
    New-Item -ItemType Directory -Force -Path $skillRoot,$validatorRoot,$commandRoot | Out-Null
    Copy-Item (Join-Path $sourceRoot '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1') (Join-Path $validatorRoot 'Invoke-ESSkillValidation.ps1')
    $catalog=@{commands=@(@{id='fixture.review';path='Assets/Plugins/ES/AICommands/fixture.md';title='Fixture review';summary='read-only fixture';role='review';riskLevel='L1';writeMode='read-only';keywords='fixture'})}|ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $commandRoot 'AICommandCatalog.json'),$catalog,(New-Object Text.UTF8Encoding($false)))
    $fixtureCommandBody=('CommandType: review'+[Environment]::NewLine+'WriteMode: read-only'+[Environment]::NewLine+'RiskLevel: L1'+[Environment]::NewLine)
    [IO.File]::WriteAllText((Join-Path $commandRoot 'fixture.md'),$fixtureCommandBody,(New-Object Text.UTF8Encoding($false)))
    function Write-Fixture([object]$governance,[string]$script='') {
        $path=$skillRoot; New-Item -ItemType Directory -Force -Path $path | Out-Null
        Remove-Item -LiteralPath (Join-Path $path 'fixture.ps1') -Force -ErrorAction SilentlyContinue
        $gov=$governance|ConvertTo-Json -Depth 8
        [IO.File]::WriteAllText((Join-Path $path 'governance.json'),$gov,(New-Object Text.UTF8Encoding($false)))
        [IO.File]::WriteAllText((Join-Path $path 'SKILL.md'),"---`nname: es-fixture`ndescription: fixture`n---`nDo not bypass AIWarnings.`n",(New-Object Text.UTF8Encoding($false)))
        if($script){[IO.File]::WriteAllText((Join-Path $path 'fixture.ps1'),$script,(New-Object Text.UTF8Encoding($false)))}
        return $path
    }
    $hash=(Get-FileHash (Join-Path $commandRoot 'fixture.md') -Algorithm SHA256).Hash.ToLowerInvariant()
    $valid=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='read-only';requiresBrainPlan=$false;allowDirectExecution=$false;writePolicy='read-only';authorityClass='standard';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='required';commandBindings=@([ordered]@{commandId='fixture.review';commandHash=$hash;role='review';riskLevel='L1';writeMode='read-only';bodyContains=@('CommandType')});requiredAuthorityRefs=@();taskContractRequired=$false})
    $invoke=Join-Path $validatorRoot 'Invoke-ESSkillValidation.ps1'
    $validJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') | ConvertFrom-Json
    if($validJson.results){$validJson.results|ConvertTo-Json -Depth 8|Write-Verbose}
    if([string]$validJson.results[0].status -ne 'passed'){throw 'valid explicit binding fixture did not pass'}
    $invalid=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='read-only';requiresBrainPlan=$false;allowDirectExecution=$false;writePolicy='read-only';authorityClass='standard';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='required';commandBindings=@();requiredAuthorityRefs=@();taskContractRequired=$false})
    $invalidRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null | Out-String
    $invalidExit=$LASTEXITCODE
    $invalidJson=$invalidRaw|ConvertFrom-Json
    if([string]$invalidJson.status -ne 'review' -or [string]$invalidJson.results[0].status -ne 'review' -or $invalidExit -ne 0){throw 'CurrentUserDirect invalid binding was not review-only'}
    if([string]$invalidJson.authorizationLane -ne 'CurrentUserDirect'){throw 'default authorization lane was not reported'}
    $managedInvalidRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') -AuthorizationLane ManagedAIBrain 2>$null | Out-String
    $managedInvalidExit=$LASTEXITCODE
    $managedInvalidJson=$managedInvalidRaw|ConvertFrom-Json
    if([string]$managedInvalidJson.status -ne 'blocked' -or [string]$managedInvalidJson.results[0].status -ne 'blocked' -or $managedInvalidExit -ne 1){throw 'ManagedAIBrain invalid binding did not block'}
    if([string]$managedInvalidJson.authorizationLane -ne 'ManagedAIBrain'){throw 'managed authorization lane was not reported'}
    $missingTask=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='read-only';requiresBrainPlan=$true;allowDirectExecution=$false;writePolicy='read-only';authorityClass='core-governed';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='required';commandBindings=@([ordered]@{commandId='fixture.review';commandHash=$hash;role='review';riskLevel='L1';writeMode='read-only';bodyContains=@('CommandType');taskContractRequired=$true;taskContractRef='' });requiredAuthorityRefs=@()})
    $directTaskRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null|Out-String
    $directTaskExit=$LASTEXITCODE;$directTaskJson=$directTaskRaw|ConvertFrom-Json;$directTaskFinding=@($directTaskJson.results[0].findings|Where-Object code -eq 'MissingTaskContract')
    if([string]$directTaskJson.results[0].status -ne 'review' -or [string]$directTaskFinding[0].severity -ne 'review' -or $directTaskExit -ne 0){throw 'CurrentUserDirect missing TaskContract was not review-only'}
    $managedTaskRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') -AuthorizationLane ManagedAIBrain 2>$null|Out-String
    $managedTaskExit=$LASTEXITCODE;$managedTaskJson=$managedTaskRaw|ConvertFrom-Json;$managedTaskFinding=@($managedTaskJson.results[0].findings|Where-Object code -eq 'MissingTaskContract')
    if([string]$managedTaskJson.results[0].status -ne 'blocked' -or [string]$managedTaskFinding[0].severity -ne 'blocked' -or $managedTaskExit -ne 1){throw 'ManagedAIBrain missing TaskContract did not block'}
    $dynamic=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='read-only';requiresBrainPlan=$false;allowDirectExecution=$false;writePolicy='read-only';authorityClass='standard';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='none';commandBindings=@();requiredAuthorityRefs=@();taskContractRequired=$false}) 'Set-Content -LiteralPath (Join-Path $root $target) -Value x'
    $dynamicJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null | ConvertFrom-Json
    if(([string]$dynamicJson.results[0].source) -notmatch 'dynamic-path'){throw 'dynamic path fixture was not reported'}
    $review=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='read-only';requiresBrainPlan=$false;allowDirectExecution=$false;writePolicy='read-only';authorityClass='standard';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='none';commandBindings=@();requiredAuthorityRefs=@();taskContractRequired=$false}) "param([string]`$ProjectRoot)`n`$root=(Resolve-Path -LiteralPath `$ProjectRoot).Path`nSet-Content -LiteralPath (Join-Path `$root `$target) -Value x"
    $reviewJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null | ConvertFrom-Json
    if([string]$reviewJson.results[0].status -ne 'review'){throw 'bounded read-only dynamic path was not classified as review'}
    if([string]$reviewJson.results[0].findings[0].severity -ne 'review'){throw 'review finding severity was not preserved'}
    if([string]$reviewJson.status -ne 'review'){throw 'review-pending aggregate was incorrectly reported as passed'}
    if([string]$reviewJson.overallVerdict -ne 'StaticReviewCompleteReviewPending'){throw 'review-pending aggregate verdict was lost'}
    $external=Write-Fixture ([ordered]@{schemaVersion=1;skillName='es-fixture';tier='SmallTool';maturity='Stable';delivery='Implemented';evidenceLevel='S1';riskClass='test';executionMode='plan-then-authorize';requiresBrainPlan=$true;allowDirectExecution=$false;writePolicy='explicit-authorized';authorityClass='core-governed';owner='test';acceptanceOwner='test';requiredCases=@('positive');routeKeys=@('fixture');commandRequirement='none';commandBindings=@();requiredAuthorityRefs=@();taskContractRequired=$false}) 'Start-Process -FilePath $externalPath'
    $boundaryJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null | ConvertFrom-Json
    if([string]$boundaryJson.overallVerdict -ne 'StaticBoundaryBlocked'){throw 'external boundary was not classified as StaticBoundaryBlocked'}
    if([string]$boundaryJson.staticCodeStatus -ne 'passed' -or [string]$boundaryJson.blockingLayer -ne 'static-boundary'){throw 'external boundary leaked into static code layer'}
    $evidenceRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null | Out-String
    $evidenceExit=$LASTEXITCODE
    $evidenceJson=$evidenceRaw|ConvertFrom-Json
    if([string]$evidenceJson.decisionStatus -ne 'evidence-pending' -or [string]$evidenceJson.blockingLayer -ne 'evidence' -or [string]$evidenceJson.results[0].status -ne 'review' -or $evidenceExit -ne 0){throw 'CurrentUserDirect missing evidence was not review-only evidence-pending'}
    $managedEvidenceRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') -AuthorizationLane ManagedAIBrain 2>$null | Out-String
    $managedEvidenceExit=$LASTEXITCODE
    $managedEvidenceJson=$managedEvidenceRaw|ConvertFrom-Json
    if([string]$managedEvidenceJson.status -ne 'blocked' -or [string]$managedEvidenceJson.decisionStatus -ne 'evidence-pending' -or [string]$managedEvidenceJson.results[0].status -ne 'blocked' -or $managedEvidenceExit -ne 1){throw 'ManagedAIBrain missing evidence did not block the managed channel'}
    $outputRoot=Join-Path $temp 'ES/Output';New-Item -ItemType Directory -Force -Path $outputRoot|Out-Null
    $sourceRef='.agents/skills/es-fixture/SKILL.md'
    $receipt=[ordered]@{skillName='es-fixture';case='positive';status='passed';evidenceLevel='S2';receiptPath='ES/Output/FixtureReceipt.json';toolId='fixture-validator';capturedUtc=[DateTime]::UtcNow.ToString('o');unityVersion='not-applicable';skillHash=(Get-FileHash (Join-Path $skillRoot 'SKILL.md') -Algorithm SHA256).Hash.ToLowerInvariant();governanceHash=(Get-FileHash (Join-Path $skillRoot 'governance.json') -Algorithm SHA256).Hash.ToLowerInvariant();validatorHash=(Get-FileHash $invoke -Algorithm SHA256).Hash.ToLowerInvariant();sourceRefs=@($sourceRef);sourceRefHashes=[ordered]@{$sourceRef=(Get-FileHash (Join-Path $temp $sourceRef) -Algorithm SHA256).Hash.ToLowerInvariant()}}
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $unboundReceiptRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null|Out-String
    $unboundReceiptExit=$LASTEXITCODE;$unboundReceiptJson=$unboundReceiptRaw|ConvertFrom-Json
    if([string]$unboundReceiptJson.results[0].status -ne 'review' -or $unboundReceiptExit -ne 0){throw 'CurrentUserDirect unbound receipt was not review-only'}
    $receipt.authorizationKind='current-user-direct';$receipt.userInstructionHash='b'*64;$receipt.authorizedOperations=@('modify');$receipt.authorizedPaths=@('.agents/skills/es-fixture')
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $directReceiptRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null|Out-String
    $directReceiptExit=$LASTEXITCODE;$directReceiptJson=$directReceiptRaw|ConvertFrom-Json
    if([string]$directReceiptJson.results[0].status -ne 'passed' -or $directReceiptExit -ne 0){throw 'CurrentUserDirect valid receipt without PlanHash did not pass'}
    $receipt.sourceRefs=@();$receipt.sourceRefHashes=[ordered]@{}
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $emptyRefsRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null|Out-String
    $emptyRefsExit=$LASTEXITCODE;$emptyRefsJson=$emptyRefsRaw|ConvertFrom-Json
    if([string]$emptyRefsJson.results[0].status -ne 'review' -or $emptyRefsExit -ne 0){throw 'CurrentUserDirect empty sourceRefs were not review-only'}
    [IO.File]::WriteAllText($outsideSourcePath,'outside source ref',(New-Object Text.UTF8Encoding($false)))
    $outsideSourceRef='../'+[IO.Path]::GetFileName($outsideSourcePath)
    $receipt.sourceRefs=@($outsideSourceRef);$receipt.sourceRefHashes=[ordered]@{$outsideSourceRef=(Get-FileHash $outsideSourcePath -Algorithm SHA256).Hash.ToLowerInvariant()}
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $escapeRefsRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null|Out-String
    $escapeRefsExit=$LASTEXITCODE;$escapeRefsJson=$escapeRefsRaw|ConvertFrom-Json
    if([string]$escapeRefsJson.results[0].status -ne 'review' -or $escapeRefsExit -ne 0){throw 'CurrentUserDirect existing escaping sourceRef was not review-only'}
    $receipt.sourceRefs=@($sourceRef);$receipt.sourceRefHashes=[ordered]@{$sourceRef=(Get-FileHash (Join-Path $temp $sourceRef) -Algorithm SHA256).Hash.ToLowerInvariant()}
    $receipt.authorizationKind='managed-aibrain'
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $managedReceiptRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') -AuthorizationLane ManagedAIBrain 2>$null|Out-String
    $managedReceiptExit=$LASTEXITCODE;$managedReceiptJson=$managedReceiptRaw|ConvertFrom-Json
    if([string]$managedReceiptJson.results[0].status -ne 'blocked' -or $managedReceiptExit -ne 1){throw 'ManagedAIBrain receipt without PlanHash did not block'}
    $receipt['planHash']='a'*64
    [IO.File]::WriteAllText((Join-Path $outputRoot 'FixtureReceipt.json'),($receipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    $managedValidRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') -AuthorizationLane ManagedAIBrain 2>$null|Out-String
    $managedValidExit=$LASTEXITCODE;$managedValidJson=$managedValidRaw|ConvertFrom-Json
    if([string]$managedValidJson.status -ne 'passed' -or [string]$managedValidJson.results[0].status -ne 'passed' -or $managedValidExit -ne 0){throw 'ManagedAIBrain valid PlanHash receipt did not pass'}

    $policyPath=Join-Path $temp '.agents/SKILL_DISCOVERY_POLICY.json'
    $resourcePath=Join-Path $temp '.agents/SKILL_RESOURCE_INDEX.yaml'
    $catalogPath=Join-Path $temp '.agents/SKILL_CATALOG.yaml'
    $knowledgePath=Join-Path $temp 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $brainPath=Join-Path $temp 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md'
    $bindingPath=Join-Path $temp '.agents/skills/es-skill-governance/references/command-binding-registry.json'
    foreach($directory in @((Split-Path -Parent $knowledgePath),(Split-Path -Parent $bindingPath))){New-Item -ItemType Directory -Force -Path $directory|Out-Null}
    $fixtureUtf8=New-Object Text.UTF8Encoding($false)
    $policy=[ordered]@{states=[ordered]@{Stable=[ordered]@{discoveryState='operational';planEligibility='eligible';runtimeEligibility='authorized-only'}};deliveryOverrides=[ordered]@{};genericRouteKeys=@('review')}
    [IO.File]::WriteAllText($policyPath,($policy|ConvertTo-Json -Depth 8),$fixtureUtf8)
    $resourceContent="schemaVersion: 1`nregistryManifest: .agents/SKILL_REGISTRY.manifest.json`n"
    [IO.File]::WriteAllText($resourcePath,$resourceContent,$fixtureUtf8)
    [IO.File]::WriteAllText($catalogPath,"skills:`n  es-fixture:`n    discoveryState: operational`n    planEligibility: eligible`n    runtimeEligibility: authorized-only`n",$fixtureUtf8)
    [IO.File]::WriteAllText($knowledgePath,"entries:`n  - knowledgeId: fixture`n    routeKeys: [fixture]`n    relatedSkills: [es-fixture]`n",$fixtureUtf8)
    [IO.File]::WriteAllText($brainPath,"# Fixture AIBrain`n",$fixtureUtf8)
    [IO.File]::WriteAllText($bindingPath,([ordered]@{entries=@();nonExecutionExemptions=@()}|ConvertTo-Json -Depth 6),$fixtureUtf8)
    $metadata=[ordered]@{}
    foreach($metadataRelative in @('.agents/SKILL_DISCOVERY_POLICY.json','.agents/SKILL_RESOURCE_INDEX.yaml','.agents/SKILL_CATALOG.yaml','Documentation/AIKnowledge/AIBRAIN_ENTRY.md','Assets/Plugins/ES/AICommands/AICommandCatalog.json')){$metadata[$metadataRelative]=(Get-FileHash (Join-Path $temp $metadataRelative) -Algorithm SHA256).Hash.ToLowerInvariant()}
    $manifest=[ordered]@{schemaVersion=1;manifestId='esframework-skill-registry';metadata=$metadata;skills=@([ordered]@{skillName='es-fixture';skillHash=(Get-FileHash (Join-Path $skillRoot 'SKILL.md') -Algorithm SHA256).Hash.ToLowerInvariant();governanceHash=(Get-FileHash (Join-Path $skillRoot 'governance.json') -Algorithm SHA256).Hash.ToLowerInvariant()})}
    [IO.File]::WriteAllText((Join-Path $temp '.agents/SKILL_REGISTRY.manifest.json'),($manifest|ConvertTo-Json -Depth 8),$fixtureUtf8)
    $architecture=(Resolve-Path (Join-Path $sourceRoot '.agents/skills/es-skill-governance/scripts/Test-ESSkillArchitecture.ps1')).Path
    [IO.File]::WriteAllText($resourcePath,($resourceContent+"drift: true`n"),$fixtureUtf8)
    $resourceDriftRaw=& powershell -NoProfile -File $architecture -ProjectRoot $temp 2>$null|Out-String
    $resourceDriftExit=$LASTEXITCODE;$resourceDriftJson=$resourceDriftRaw|ConvertFrom-Json
    $resourceDriftFinding=@($resourceDriftJson.findings|Where-Object { $_.code -eq 'registry-metadata-stale' -and $_.detail -like '*SKILL_RESOURCE_INDEX.yaml*' })
    if([string]$resourceDriftJson.status -ne 'blocked' -or $resourceDriftFinding.Count -ne 1 -or $resourceDriftExit -ne 1){throw 'Architecture did not block a stale Resource Index metadata hash'}
    [IO.File]::WriteAllText($resourcePath,$resourceContent,$fixtureUtf8)
    $directArchitectureRaw=& powershell -NoProfile -File $architecture -ProjectRoot $temp 2>$null|Out-String
    $directArchitectureExit=$LASTEXITCODE;$directArchitectureJson=$directArchitectureRaw|ConvertFrom-Json
    $directCommandFinding=@($directArchitectureJson.findings|Where-Object code -eq 'command-binding-unresolved')
    if([string]$directArchitectureJson.authorizationLane -ne 'CurrentUserDirect' -or [string]$directArchitectureJson.status -ne 'review' -or [string]$directCommandFinding[0].severity -ne 'review' -or $directArchitectureExit -ne 0){throw 'Architecture CurrentUserDirect command binding was not review-only'}
    $managedArchitectureRaw=& powershell -NoProfile -File $architecture -ProjectRoot $temp -AuthorizationLane ManagedAIBrain 2>$null|Out-String
    $managedArchitectureExit=$LASTEXITCODE;$managedArchitectureJson=$managedArchitectureRaw|ConvertFrom-Json
    $managedCommandFinding=@($managedArchitectureJson.findings|Where-Object code -eq 'command-binding-unresolved')
    if([string]$managedArchitectureJson.authorizationLane -ne 'ManagedAIBrain' -or [string]$managedArchitectureJson.status -ne 'blocked' -or [string]$managedCommandFinding[0].severity -ne 'blocked' -or $managedArchitectureExit -ne 1){throw 'Architecture ManagedAIBrain command binding did not block'}
    if(Test-Path -LiteralPath (Join-Path $temp 'ES/Output/SkillArchitecture/architecture.json')){throw 'Architecture wrote a report without an explicit ReportPath'}
    $escapedReportRelative='../'+[IO.Path]::GetFileName($escapedReportPath)
    $previousArchitectureErrorAction=$ErrorActionPreference
    try{
        $ErrorActionPreference='Continue'
        $escapedReportRaw=& powershell -NoProfile -File $architecture -ProjectRoot $temp -ReportPath $escapedReportRelative 2>&1|Out-String
        $escapedReportExit=$LASTEXITCODE
    }finally{$ErrorActionPreference=$previousArchitectureErrorAction}
    if($escapedReportExit -ne 2){throw 'Architecture escaping ReportPath did not return parameter error exit 2'}
    if(Test-Path -LiteralPath $escapedReportPath){throw 'Architecture escaping ReportPath wrote outside ProjectRoot'}
    $previousValidatorErrorAction=$ErrorActionPreference
    try{
        $ErrorActionPreference='Continue'
        $escapedValidatorRaw=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') -ReportPath $escapedReportRelative 2>&1|Out-String
        $escapedValidatorExit=$LASTEXITCODE
    }finally{$ErrorActionPreference=$previousValidatorErrorAction}
    if($escapedValidatorExit -ne 2){throw 'Validator escaping ReportPath did not return parameter error exit 2'}
    if(Test-Path -LiteralPath $escapedReportPath){throw 'Validator escaping ReportPath wrote outside ProjectRoot'}

    $contractRoot=Join-Path $temp '.agents/skills/es-contract-fixture'
    New-Item -ItemType Directory -Force -Path (Join-Path $contractRoot 'agents'),(Join-Path $contractRoot 'references')|Out-Null
    [IO.File]::WriteAllText((Join-Path $contractRoot 'SKILL.md'),"---`nname: es-contract-fixture`ndescription: Contract authorization lane fixture.`n---`n`n## SmallTool controls`n",$fixtureUtf8)
    [IO.File]::WriteAllText((Join-Path $contractRoot 'agents/openai.yaml'),"interface:`n  display_name: `"Contract Fixture`"`n  short_description: `"Validate contract lane semantics`"`n  default_prompt: `"Use es-contract-fixture for validation.`"`n",$fixtureUtf8)
    [IO.File]::WriteAllText((Join-Path $contractRoot 'references/control.md'),"# Fixture control`n",$fixtureUtf8)
    $contractGovernance=[ordered]@{schemaVersion=1;skillName='es-contract-fixture';tier='SmallTool';maturity='Stable';delivery='Accepted';evidenceLevel='S2';riskClass='test';executionMode='managed';writePolicy='read-only';authorityClass='core-governed';owner='test';acceptanceOwner='test';routeKeys=@('fixture');requiredCases=@('positive','invalid-input','denied-expansion','repeat-idempotency');controlRefs=@('references/control.md')}
    [IO.File]::WriteAllText((Join-Path $contractRoot 'governance.json'),($contractGovernance|ConvertTo-Json -Depth 8),$fixtureUtf8)
    $contract=(Resolve-Path (Join-Path $sourceRoot '.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1')).Path
    $directContractRaw=& powershell -NoProfile -File $contract -SkillPath $contractRoot -RequireGovernanceMetadata 2>&1|Out-String
    $directContractExit=$LASTEXITCODE
    if($directContractExit -ne 0 -or $directContractRaw -notmatch 'authorizationLane=CurrentUserDirect'){throw 'Contract Direct lane was constrained by managed-only governance fields'}
    $previousErrorAction=$ErrorActionPreference
    try{
        $ErrorActionPreference='Continue'
        $managedContractRaw=& powershell -NoProfile -File $contract -SkillPath $contractRoot -RequireGovernanceMetadata -AuthorizationLane ManagedAIBrain 2>&1|Out-String
        $managedContractExit=$LASTEXITCODE
    }finally{$ErrorActionPreference=$previousErrorAction}
    if($managedContractExit -ne 1){throw 'Contract ManagedAIBrain lane did not require managed governance fields'}
    Write-Output 'PASS: direct/managed binding, PlanHash, sourceRef containment, Architecture, Contract and evidence-layer regression checks'
} finally {
    if(Test-Path $temp){Remove-Item -LiteralPath $temp -Recurse -Force}
    foreach($cleanupPath in @($outsideSourcePath,$escapedReportPath)){if(Test-Path -LiteralPath $cleanupPath){Remove-Item -LiteralPath $cleanupPath -Force}}
}
