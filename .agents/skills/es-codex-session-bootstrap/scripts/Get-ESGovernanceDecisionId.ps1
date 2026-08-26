function Get-ESGovernanceCanonicalHash($Value) {
    $json = ConvertTo-Json -InputObject $Value -Compress -Depth 20
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}

function Get-ESGovernanceDecisionFingerprint($Decision) {
    $depthValue = if ($null -ne $Decision.routeDepth) { [int]$Decision.routeDepth } else { 0 }
    $depthReasonValue = if ($null -ne $Decision.depthReasonCode) { [string]$Decision.depthReasonCode } else { '' }
    return [ordered]@{
        object = [string]$Decision.object
        field = [string]$Decision.field
        profile = [string]$Decision.profile
        scope = [string]$Decision.scope
        scopeKind = [string]$Decision.scopeKind
        reasonCode = [string]$Decision.reasonCode
        routeState = [string]$Decision.routeState
        evidenceState = [string]$Decision.evidenceState
        effect = [string]$Decision.effect
        routeDepth = $depthValue
        depthReasonCode = $depthReasonValue
        authorization = [ordered]@{
            mode = [string]$Decision.authorization.mode
            requestedAction = [string]$Decision.authorization.requestedAction
            requestedScope = [string]$Decision.authorization.requestedScope
        }
        snapshot = [ordered]@{
            head = [string]$Decision.snapshot.head
            sourceRefsHash = [string]$Decision.snapshot.sourceRefsHash
            registryHash = [string]$Decision.snapshot.registryHash
        }
    }
}

function Get-ESGovernanceDecisionId($Decision) {
    return 'decision-' + (Get-ESGovernanceCanonicalHash (Get-ESGovernanceDecisionFingerprint $Decision)).Substring(0, 24)
}
