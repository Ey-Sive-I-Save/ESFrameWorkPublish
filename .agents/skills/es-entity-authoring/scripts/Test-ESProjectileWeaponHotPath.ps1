[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [switch]$Json
)

$ErrorActionPreference = 'Stop'

$targets = @(
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemBasicModules.cs'
        Methods = @(
            'Internal_TickCentralized',
            'Tick',
            'SetPendingShotResult',
            'ApplyMotionInfluences',
            'TryReadDynamicVelocity',
            'ApplyExternalMotion',
            'TickScan',
            'SetScanWaitingResult',
            'ExecuteScan',
            'TryBuildHitCandidate',
            'TryBuildMustHitCandidate',
            'ResolveHit',
            'TryApplyPreparedBounce',
            'PublishPreparedImpactHits',
            'PublishAreaHits',
            'PublishChainHits',
            'TrySelectNearestImpactTarget',
            'BuildImpactHit',
            'StopAtHitBoundary',
            'ContainsResolvedCollider',
            'TryAddResolvedCollider',
            'PublishLifecycle',
            'RequestPoolReturn',
            'RefreshTargetPosition',
            'ApplySpread',
            'RangeFromSeed'
        )
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemShotHitAndTick.cs'
        Methods = @(
            'Internal_TryPrepareSpawn',
            'Internal_TrySpawnPrepared',
            'TryResolveEntity',
            'Resolve',
            'Query',
            'QuerySaturatedNearest',
            'TryAllows'
        )
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs'
        Methods = @(
            'TryExecutePrimaryAttack',
            'TryFireWeapon',
            'TickWeaponFirePolicy',
            'HandleShotLifecycle',
            'TryRegisterShotPattern',
            'TryCompleteShotPatternMember',
            'PublishPrimaryAttackEvent',
            'Internal_ConsumePrimaryAttackHit',
            'ApplyWeaponPatternSpread',
            'TryResolveWeaponRaycast',
            'TryResolveWeaponRaycastOverflow',
            'GetWeaponUseFailureReason'
        )
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionSolver.cs'
        Methods = @('Step', 'StepRotation')
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityCombatDamage.cs'
        Methods = @('TryApplyDamage')
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ESShotSimulationScheduler.cs'
        Methods = @('Internal_Tick')
    },
    @{
        Path = 'Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs'
        Methods = @('Update')
    }
)

$forbidden = [ordered]@{
    'runtime-preparation' = '\b(?:Ensure|Internal_Prepare)\w*\s*\('
    'array-resize' = '\bArray\.Resize\s*\('
    'temporary-array' = '\bnew\s+[A-Za-z_][\w.<>]*\s*\['
    'temporary-collection' = '\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack)\s*[<(]'
    'component-lookup' = '\bGetComponents?(?:InChildren|InParent)?\s*<'
    'runtime-module-lookup' = '\b(?:GetMoudle|FindMyModule)\s*<'
    'runtime-validation' = '\b(?:Validate|TryValidate)\w*\s*\('
    'reflection' = '\b(?:System\.)?Reflection\b|\bGetMethod\s*\('
    'linq' = '(?<!EntityPrimaryAttackSelector)\.(?:Select|Where|OrderBy|ThenBy|ToList|ToArray|Any|First|Single)\s*\('
    'invocation-snapshot' = '\bGetInvocationList\s*\('
    'uncached-shot-lifecycle-delegate' = '\bHandleShotLifecycle\s*[,)]'
    'enum-to-string' = '\b(?:selection\.route|useFailure|failure|kind|decision)\s*(?:\.ToString\s*\(|\+)|\+\s*(?:selection\.route|useFailure|failure|kind|decision)\b'
    'failure-string-routing' = '\blastPrimaryAttackFailureReason\s*\.\s*(?:IndexOf|Contains|StartsWith)\s*\('
}

function Get-MethodBlock {
    param(
        [string[]]$Lines,
        [string]$MethodName
    )

    for ($attributeIndex = 0; $attributeIndex -lt $Lines.Count; $attributeIndex++) {
        if ($Lines[$attributeIndex].Trim() -ne '[ESHotPath]') {
            continue
        }

        $signatureIndex = $attributeIndex + 1
        while ($signatureIndex -lt $Lines.Count -and [string]::IsNullOrWhiteSpace($Lines[$signatureIndex])) {
            $signatureIndex++
        }
        $signatureFound = $false
        for ($probe = $signatureIndex; $probe -lt [Math]::Min($Lines.Count, $signatureIndex + 12); $probe++) {
            if ($Lines[$probe] -match ('\b' + [regex]::Escape($MethodName) + '\s*\(')) {
                $signatureIndex = $probe
                $signatureFound = $true
                break
            }
            $reachedBody = $Lines[$probe].IndexOf('{') -ge 0
            $reachedNextHotMethod = $probe -gt $signatureIndex -and $Lines[$probe].Trim() -eq '[ESHotPath]'
            if ($reachedBody -or $reachedNextHotMethod) {
                break
            }
        }
        if (-not $signatureFound) {
            continue
        }

        $openIndex = $signatureIndex
        while ($openIndex -lt $Lines.Count -and $Lines[$openIndex].IndexOf('{') -lt 0) {
            $openIndex++
        }
        if ($openIndex -ge $Lines.Count) {
            return $null
        }

        $depth = 0
        $body = [System.Collections.Generic.List[object]]::new()
        for ($lineIndex = $openIndex; $lineIndex -lt $Lines.Count; $lineIndex++) {
            $line = $Lines[$lineIndex]
            $body.Add([pscustomobject]@{ Line = $lineIndex + 1; Text = $line })

            # Strip strings and comments before counting braces so interpolated diagnostics
            # do not confuse the lightweight method boundary parser.
            $code = [regex]::Replace($line, '"(?:\\.|[^"\\])*"', '""')
            $code = [regex]::Replace($code, "'(?:\\.|[^'\\])*'", "''")
            $code = $code -replace '//.*$', ''
            $depth += ([regex]::Matches($code, '\{')).Count
            $depth -= ([regex]::Matches($code, '\}')).Count
            if ($depth -eq 0) {
                break
            }
        }

        return [pscustomobject]@{
            Method = $MethodName
            AttributeLine = $attributeIndex + 1
            Body = $body
        }
    }

    return $null
}

$violations = [System.Collections.Generic.List[object]]::new()
$checkedMethods = [System.Collections.Generic.List[object]]::new()

foreach ($target in $targets) {
    $path = Join-Path $ProjectRoot $target.Path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $violations.Add([pscustomobject]@{
            Path = $target.Path
            Method = '<file>'
            Line = 0
            Rule = 'target-missing'
            Text = 'Target file does not exist.'
        })
        continue
    }

    $lines = Get-Content -LiteralPath $path -Encoding UTF8
    foreach ($method in $target.Methods) {
        $block = Get-MethodBlock -Lines $lines -MethodName $method
        if ($null -eq $block) {
            $violations.Add([pscustomobject]@{
                Path = $target.Path
                Method = $method
                Line = 0
                Rule = 'hot-path-marker-missing'
                Text = 'Expected [ESHotPath] method was not found.'
            })
            continue
        }

        $checkedMethods.Add([pscustomobject]@{
            Path = $target.Path
            Method = $method
            AttributeLine = $block.AttributeLine
        })

        foreach ($entry in $forbidden.GetEnumerator()) {
            foreach ($bodyLine in $block.Body) {
                if ($bodyLine.Text -match $entry.Value) {
                    $violations.Add([pscustomobject]@{
                        Path = $target.Path
                        Method = $method
                        Line = $bodyLine.Line
                        Rule = $entry.Key
                        Text = $bodyLine.Text.Trim()
                    })
                }
            }
        }
    }
}

$shotSpawnerPath = 'Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemShotHitAndTick.cs'
$shotSpawnerFile = Join-Path $ProjectRoot $shotSpawnerPath
if (Test-Path -LiteralPath $shotSpawnerFile -PathType Leaf) {
    $shotSpawnerLines = Get-Content -LiteralPath $shotSpawnerFile -Encoding UTF8
    for ($lineIndex = 0; $lineIndex -lt $shotSpawnerLines.Count; $lineIndex++) {
        if ($shotSpawnerLines[$lineIndex] -match '\bTryAcquireReady\s*\(') {
            $violations.Add([pscustomobject]@{
                Path = $shotSpawnerPath
                Method = 'TrySpawn'
                Line = $lineIndex + 1
                Rule = 'per-shot-resource-lease'
                Text = $shotSpawnerLines[$lineIndex].Trim()
            })
        }
    }
}

$combatPath = 'Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs'
$combatFile = Join-Path $ProjectRoot $combatPath
if (Test-Path -LiteralPath $combatFile -PathType Leaf) {
    $combatLines = Get-Content -LiteralPath $combatFile -Encoding UTF8
    $fireBlock = Get-MethodBlock -Lines $combatLines -MethodName 'TryFireWeapon'
    if ($null -ne $fireBlock) {
        $prepareCalls = @($fireBlock.Body | Where-Object { $_.Text -match '\bInternal_TryPrepareSpawn\s*\(' })
        $preparedSpawnCalls = @($fireBlock.Body | Where-Object { $_.Text -match '\bInternal_TrySpawnPrepared\s*\(' })
        $publicSpawnCalls = @($fireBlock.Body | Where-Object { $_.Text -match '\bTrySpawn(?:WithVariable)?\s*\(' })
        if ($prepareCalls.Count -ne 1) {
            $violations.Add([pscustomobject]@{
                Path = $combatPath
                Method = 'TryFireWeapon'
                Line = $fireBlock.AttributeLine
                Rule = 'weapon-prepare-once'
                Text = 'TryFireWeapon must resolve one prepared Shot context per fire request.'
            })
        }
        if ($preparedSpawnCalls.Count -ne 1) {
            $violations.Add([pscustomobject]@{
                Path = $combatPath
                Method = 'TryFireWeapon'
                Line = $fireBlock.AttributeLine
                Rule = 'weapon-prepared-spawn-loop'
                Text = 'TryFireWeapon must use exactly one prepared-spawn call site for all pattern members.'
            })
        }
        foreach ($publicCall in $publicSpawnCalls) {
            $violations.Add([pscustomobject]@{
                Path = $combatPath
                Method = 'TryFireWeapon'
                Line = $publicCall.Line
                Rule = 'weapon-public-spawn-revalidation'
                Text = $publicCall.Text.Trim()
            })
        }
    }
}

$report = [ordered]@{
    projectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    valid = ($violations.Count -eq 0)
    checkedMethods = $checkedMethods
    violations = $violations
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    Write-Output ("ES Projectile/Weapon hot-path check: " + ($(if ($report.valid) { 'PASS' } else { 'FAIL' })))
    Write-Output ("Checked methods: " + $checkedMethods.Count)
    foreach ($violation in $violations) {
        Write-Output ("[{0}] {1}:{2} {3} - {4}" -f $violation.Rule, $violation.Path, $violation.Line, $violation.Method, $violation.Text)
    }
}

if (-not $report.valid) {
    exit 1
}
