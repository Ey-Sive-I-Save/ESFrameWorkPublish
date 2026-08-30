[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$JsonPath,
    [ValidateSet('Deep','Schema')][string]$Mode = 'Deep',
    [string]$SchemaPath = (Join-Path $PSScriptRoot '..\references\es-resource-group-state.v1.schema.json')
)
$ErrorActionPreference = 'Stop'
$errors = [System.Collections.Generic.List[string]]::new()
function Add-Error([string]$m) { [void]$errors.Add($m) }
try {
    $raw = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $JsonPath), [Text.UTF8Encoding]::new($false, $true))
    $root = $raw | ConvertFrom-Json
    if ($null -eq $root -or $root -isnot [pscustomobject]) { Add-Error 'root must be a JSON object' }
    if (-not (Test-Path -LiteralPath $SchemaPath)) { Add-Error "schema missing: $SchemaPath" }
    if ($null -ne $root) {
        foreach ($p in 'schemaVersion','groupId','groupName','lifecycleState','authorityStage','source','classification','deliveryIntent','targetPath','items','dependencies','verification','rollback') { if ($null -eq $root.PSObject.Properties[$p]) { Add-Error "missing required property: $p" } }
        if ($root.schemaVersion -ne 1) { Add-Error 'schemaVersion must be 1' }
        if ($root.groupId -notmatch '^[a-z0-9][a-z0-9._-]{0,63}$') { Add-Error 'groupId is not canonical' }
        $allowedStates = 'Discovered','Verified','Classified','Staged','ReadyForAggregation','NeedsReview','Quarantined','Failed','Canceled'; if ($allowedStates -notcontains [string]$root.lifecycleState) { Add-Error 'invalid lifecycleState' }
        $allowedStages = 'Unverified','ProvenanceVerified','UserAuthorized','AssetPackageAccepted'; if ($allowedStages -notcontains [string]$root.authorityStage) { Add-Error 'invalid authorityStage' }
        foreach ($p in 'source','verification','rollback') { if ($root.$p -isnot [pscustomobject]) { Add-Error "$p must be an object" } }
        if ($root.source) { foreach ($p in 'sourceId','sourceKind','sourceReference','provenance','license','observedUtc') { if ([string]::IsNullOrWhiteSpace([string]$root.source.$p)) { Add-Error "source.$p is required" } }; if (@('local','network','unitypackage','aispace') -notcontains [string]$root.source.sourceKind) { Add-Error 'invalid source.sourceKind' }; $dt=[datetimeoffset]::MinValue; if (-not [datetimeoffset]::TryParse([string]$root.source.observedUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind,[ref]$dt)) { Add-Error 'source.observedUtc must be ISO-8601 date-time' }; if ($root.source.sha256 -and $root.source.sha256 -notmatch '^[0-9a-f]{64}$') { Add-Error 'source.sha256 must be lowercase SHA-256' }; if ([string]$root.source.sourceReference -match '(^|[\\/])\.\.([\\/]|$)') { Add-Error 'source.sourceReference contains traversal' } }
        $items = @($root.items); if ($items.Count -eq 0) { Add-Error 'items must contain at least one item' }
        $ids = @{}; $physical = @{}
        foreach ($i in $items) {
            foreach ($p in 'itemId','guid','localFileId','contentSha256','dependencySha256','assetType','relativePath') { if ($null -eq $i.PSObject.Properties[$p]) { Add-Error "item missing $p" } }
            if ($ids.ContainsKey([string]$i.itemId)) { Add-Error "duplicate itemId: $($i.itemId)" } else { $ids[[string]$i.itemId] = $true }
            $pk = "$(($i.guid).ToLowerInvariant()):$($i.localFileId)"; if ($physical.ContainsKey($pk)) { Add-Error "duplicate physical identity: $pk" } else { $physical[$pk] = $true }
            foreach ($h in 'contentSha256','dependencySha256') { if ([string]$i.$h -notmatch '^[0-9a-f]{64}$') { Add-Error "$h must be lowercase SHA-256 for $($i.itemId)" } }
            $path = [string]$i.relativePath; if ([IO.Path]::IsPathRooted($path) -or $path -match '(^|[\\/])\.\.([\\/]|$)' -or $path -match '^[A-Za-z]:') { Add-Error "non-canonical relativePath: $path" }
        }
        $sorted = @($items | Sort-Object itemId | ForEach-Object { [string]$_.itemId }); $actual = @($items | ForEach-Object { [string]$_.itemId }); if ((ConvertTo-Json $sorted -Compress) -ne (ConvertTo-Json $actual -Compress)) { Add-Error 'items must be deterministically sorted by itemId' }
        $edges = @{}; foreach ($d in @($root.dependencies)) { if (-not $ids.ContainsKey([string]$d.from) -or -not $ids.ContainsKey([string]$d.to)) { Add-Error "unresolved dependency: $($d.from) -> $($d.to)" }; if (-not $edges.ContainsKey([string]$d.from)) { $edges[[string]$d.from] = @() }; $edges[[string]$d.from] += [string]$d.to }
        $vis = @{}; $stack = @{}; function Visit([string]$n) { if ([string]::IsNullOrWhiteSpace($n)) { return }; if ($stack[$n]) { Add-Error "dependency cycle at $n"; return }; if ($vis[$n]) { return }; $stack[$n]=$true; if ($edges.ContainsKey($n)) { foreach ($x in @($edges[$n])) { Visit $x } }; $stack.Remove($n); $vis[$n]=$true }; foreach ($n in $ids.Keys) { Visit $n }
        if ([string]$root.rollback.transactionId -eq '') { Add-Error 'rollback.transactionId is required' }
    }
} catch { Add-Error $_.Exception.Message }
$result = [ordered]@{ validator='Test-ESResourceGroupJson'; mode=$Mode; path=(Resolve-Path -LiteralPath $JsonPath).Path; valid=($errors.Count -eq 0); errorCount=$errors.Count; errors=@($errors) }
$result | ConvertTo-Json -Depth 8
if ($errors.Count -gt 0) { exit 1 }; exit 0
