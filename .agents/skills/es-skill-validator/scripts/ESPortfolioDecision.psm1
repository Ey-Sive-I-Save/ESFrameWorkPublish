Set-StrictMode -Version Latest

function Format-ESPortfolioField($Value) {
    $text = if ($null -eq $Value) { '' } elseif ($Value -is [bool]) { if ($Value) { 'true' } else { 'false' } } else { [string]$Value }
    return "$($text.Length):$text"
}

function New-ESPortfolioRecord([string]$Kind, [object[]]$Values) {
    $fields = [Collections.Generic.List[string]]::new()
    $fields.Add((Format-ESPortfolioField $Kind))
    foreach ($value in @($Values)) { $fields.Add((Format-ESPortfolioField $value)) }
    return ($fields -join '|')
}

function Resolve-ESPortfolioDecision {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][bool]$InnerResultAvailable,
        [ValidateRange(0, 1000000)][int]$HardFailureCount,
        [ValidateRange(0, 1000000)][int]$EvidencePendingCount,
        [ValidateRange(0, 1000000)][int]$ValidatorReviewCount
    )

    if (-not $InnerResultAvailable) {
        return [pscustomobject][ordered]@{ status = 'blocked'; decisionStatus = 'validator-error'; effect = 'hard-block'; blockingLayer = 'validator' }
    }
    if ($HardFailureCount -gt 0) {
        return [pscustomobject][ordered]@{ status = 'blocked'; decisionStatus = 'blocked'; effect = 'hard-block'; blockingLayer = 'static-contract' }
    }
    if ($EvidencePendingCount -gt 0) {
        return [pscustomobject][ordered]@{ status = 'review'; decisionStatus = 'evidence-pending'; effect = 'claim-cap'; blockingLayer = 'none' }
    }
    if ($ValidatorReviewCount -gt 0) {
        return [pscustomobject][ordered]@{ status = 'review'; decisionStatus = 'review'; effect = 'review'; blockingLayer = 'none' }
    }
    return [pscustomobject][ordered]@{ status = 'passed'; decisionStatus = 'static-passed'; effect = 'none'; blockingLayer = 'none' }
}

function Get-ESPortfolioProjectionHash {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Receipt)

    $records = [Collections.Generic.List[string]]::new()
    $records.Add((New-ESPortfolioRecord 'portfolio' @(
        $Receipt.catalogHash, $Receipt.resourceIndexHash, $Receipt.validatorHash, $Receipt.innerReportHash,
        $Receipt.skillCount, $Receipt.staticReadyCount, $Receipt.evidencePendingCount,
        $Receipt.runtimeRequiredCount, $Receipt.runtimeNotRunCount, $Receipt.validatorReviewCount,
        $Receipt.innerResultAvailable, $Receipt.status, $Receipt.decisionStatus, $Receipt.effect,
        $Receipt.staticStatus, $Receipt.evidenceStatus, $Receipt.runtimeStatus, $Receipt.blockingLayer
    )))

    foreach ($value in @($Receipt.contractFailures | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
        $records.Add((New-ESPortfolioRecord 'contract-failure' @($value)))
    }
    foreach ($item in @($Receipt.resourceFailures | Sort-Object skill, missing)) {
        $records.Add((New-ESPortfolioRecord 'resource-failure' @($item.skill, $item.missing)))
    }
    foreach ($name in @('validatorFailures', 'validatorBlocked', 'validatorNotRun')) {
        foreach ($value in @($Receipt.$name | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
            $records.Add((New-ESPortfolioRecord $name @($value)))
        }
    }

    $hashMap = $Receipt.sourceRefHashes
    $keys = if ($hashMap -is [Collections.IDictionary]) { @($hashMap.Keys) } else { @($hashMap.PSObject.Properties.Name) }
    foreach ($key in @($keys | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
        $value = if ($hashMap -is [Collections.IDictionary]) { $hashMap[$key] } else { $hashMap.PSObject.Properties[$key].Value }
        $records.Add((New-ESPortfolioRecord 'source-ref' @($key, $value)))
    }

    $ordered = [string[]]@($records)
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    $bytes = [Text.Encoding]::UTF8.GetBytes($ordered -join "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

Export-ModuleMember -Function Resolve-ESPortfolioDecision, Get-ESPortfolioProjectionHash
