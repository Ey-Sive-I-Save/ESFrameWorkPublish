Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ESTestImportedModuleInstance {
    param(
        [Parameter(Mandatory=$true)][object[]]$ImportedModules,
        [Parameter(Mandatory=$true)][string]$ExpectedPath,
        [Parameter(Mandatory=$true)][string]$ModuleName
    )

    $expectedFullPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ExpectedPath -ErrorAction Stop).Path)
    $matches = @($ImportedModules | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.Path) -and
        [string]::Equals([IO.Path]::GetFullPath([string]$_.Path), $expectedFullPath, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one imported $ModuleName module from '$expectedFullPath'; found $($matches.Count)."
    }
    return $matches[0]
}

$script:ESTestRouteRegistrySource = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-route-stage.registry.json'))
$script:ESTestRouteRegistryProjectPath = 'ES/Automation/Contracts/es-route-stage.registry.json'
$script:ESTestRoutePlanModuleSource = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\RoutePlan\ESRoutePlanContract.psm1'))
$routePlanModules = @(Import-Module $script:ESTestRoutePlanModuleSource -ErrorAction Stop -PassThru)
$script:ESTestRoutePlanModule = Resolve-ESTestImportedModuleInstance -ImportedModules $routePlanModules -ExpectedPath $script:ESTestRoutePlanModuleSource -ModuleName 'ESRoutePlanContract'

function Initialize-ESTestRoutePlanRepository {
    param([Parameter(Mandatory=$true)][string]$Root)
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { New-Item -ItemType Directory -Path $Root -Force | Out-Null }
    if (-not (Test-Path -LiteralPath (Join-Path $Root '.git') -PathType Container)) {
        & git -C $Root init -q 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to initialize the RoutePlan fixture Git repository.' }
        & git -C $Root config user.name 'ES RoutePlan Fixture'
        & git -C $Root config user.email 'route-plan-fixture@local.invalid'
        & git -C $Root config core.autocrlf false
        & git -C $Root commit -q --allow-empty --no-gpg-sign -m 'fixture root' 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to create the RoutePlan fixture root commit.' }
    }
}

function Initialize-ESTestGitSnapshot {
    param([Parameter(Mandatory=$true)][string]$Root)
    $gitRoot = @(& git -C $Root rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or $gitRoot.Count -ne 1) {
        Initialize-ESTestRoutePlanRepository $Root
    }
    & git -C $Root add --all 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to stage the RoutePlan fixture snapshot.' }
    & git -C $Root diff --cached --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        & git -C $Root commit -q --no-gpg-sign -m 'fixture snapshot' 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to commit the RoutePlan fixture snapshot.' }
    }
    $head = @(& git -C $Root rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $head.Count -ne 1 -or ([string]$head[0]).Trim() -cnotmatch '^[a-f0-9]{40}$') { throw 'RoutePlan fixture HEAD is invalid.' }
    return ([string]$head[0]).Trim().ToLowerInvariant()
}

function New-ESTestRoutePlan {
    param(
        [Parameter(Mandatory=$true)][string]$Root,
        [Parameter(Mandatory=$true)]$Goal,
        [string]$Profile = 'governance',
        [string]$FileName = 'route-plan.json'
    )
    $registryTarget = Join-Path $Root ($script:ESTestRouteRegistryProjectPath.Replace('/',[IO.Path]::DirectorySeparatorChar))
    $registryParent = Split-Path -Parent $registryTarget
    if (-not (Test-Path -LiteralPath $registryParent -PathType Container)) { New-Item -ItemType Directory -Path $registryParent -Force | Out-Null }
    [IO.File]::Copy($script:ESTestRouteRegistrySource, $registryTarget, $true)

    $head = Initialize-ESTestGitSnapshot $Root
    $goalFull = Join-Path $Root ([string]$Goal.path).Replace('/',[IO.Path]::DirectorySeparatorChar)
    $goalArtifactHash = (Get-FileHash -LiteralPath $goalFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $registryHash = (Get-FileHash -LiteralPath $registryTarget -Algorithm SHA256).Hash.ToLowerInvariant()
    $sourceRefs = @(
        [ordered]@{projectPath=[string]$Goal.path;sha256=$goalArtifactHash},
        [ordered]@{projectPath=$script:ESTestRouteRegistryProjectPath;sha256=$registryHash}
    ) | Sort-Object { [string]$_.projectPath } -CaseSensitive
    $snapshot = [ordered]@{
        head=$head
        sourceRefs=@($sourceRefs)
        sourceRefsHash=Get-ESRoutePlanCanonicalHash @($sourceRefs)
        registryHash=$registryHash
        coverage=[ordered]@{normalizationVersion='route-plan-canonical-v1';includes=@('goal-revision-artifact','route-stage-registry')}
    }
    $core = [ordered]@{
        schemaVersion=1
        contractId='es://automation/contracts/route-plan/v1'
        status='Ready'
        routeState='core'
        evidenceState='closed'
        effect='review'
        profile=$Profile
        scope='task-object'
        routeKeys=@('creator','skill')
        goalRevision=[ordered]@{goalId=[string]$Goal.goalId;goalRevision=[string]$Goal.goalRevision;revisionHash=[string]$Goal.goalRevisionHash;projectPath=[string]$Goal.path;artifactHash=$goalArtifactHash}
        stages=@([ordered]@{stageId='stage-01-es-skill-creator';stageContractId='es.route-stage.skill-creator.v1';skillName='es-skill-creator';depth=0;requires=@('goal-revision');produces=@('skill-candidate');failureConditions=@('candidate-write-denied','skill-contract-invalid');depthReasonCode='';executionStatus='not-executed'})
        maxDepth=0
        budget=[ordered]@{maxReads=8}
        stopConditions=@([ordered]@{code='ROUTE.DEPTH_LIMIT';predicate='next depth exceeds budget';trigger='before next stage';outcome='stop-next-read';evidence=@($script:ESTestRouteRegistryProjectPath);recovery='reduce route depth'})
        issues=@()
        snapshot=$snapshot
        compatibility=[ordered]@{legacyPlanStatus='Ready';projectionOnly=$true;productionRouteIntegrated=$false;globalP0Integrated=$false;executionAuthority='none'}
        executionEnabled=$false
    }
    $core = Add-ESRoutePlanShadowIntegration -Core $core -LegacyPlanStatus 'Ready'
    $payload = New-ESRoutePlanDocument -Core $core
    $routePlanFull = Join-Path $Root $FileName
    [IO.File]::WriteAllText($routePlanFull, ($payload | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{path=$FileName;routePlanId=$payload.routePlanId;routePlanHash=$payload.routePlanHash;fullPath=$routePlanFull}
}
