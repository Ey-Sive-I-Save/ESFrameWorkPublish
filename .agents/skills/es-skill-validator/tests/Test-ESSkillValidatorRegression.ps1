[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$sourceRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$temp=Join-Path ([IO.Path]::GetTempPath()) ('es-skill-validator-fixture-'+[Guid]::NewGuid().ToString('N'))
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
    $invalidJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Boundary') 2>$null | ConvertFrom-Json
    if([string]$invalidJson.status -ne 'blocked'){throw 'invalid binding fixture did not block'}
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
    $evidenceJson=& powershell -NoProfile -File $invoke -ProjectRoot $temp -SkillName es-fixture -Profile @('Evidence') 2>$null | ConvertFrom-Json
    if([string]$evidenceJson.decisionStatus -ne 'evidence-pending' -or [string]$evidenceJson.blockingLayer -ne 'evidence'){throw 'missing evidence was not classified as evidence-pending'}
    Write-Output 'PASS: explicit binding, refusal wording, fixture boundary, external boundary and evidence-layer regression checks'
} finally { if(Test-Path $temp){Remove-Item -LiteralPath $temp -Recurse -Force} }
