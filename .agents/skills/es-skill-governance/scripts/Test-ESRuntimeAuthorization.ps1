[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$AuthorizationPath,
    [switch]$Consume
)
$ErrorActionPreference='Stop'
if($Consume){throw 'Runtime authorization consumption requires a governed one-time ledger; this validator is read-only.'}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($AuthorizationPath)){throw 'AuthorizationPath must be project-relative'}
$full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$AuthorizationPath))
$prefix=$root.TrimEnd([char]92,[char]47)+[char]92
if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'AuthorizationPath escapes ProjectRoot'}
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'Authorization manifest not found'}
$schemaRelative='ES/Automation/Contracts/es-runtime-authorization.schema.json'
$schemaFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$schemaRelative))
if(-not $schemaFull.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime authorization schema escapes ProjectRoot'}
if(-not(Test-Path -LiteralPath $schemaFull -PathType Leaf)){throw 'Runtime authorization schema not found'}
try { $schema=Get-Content -LiteralPath $schemaFull -Raw -Encoding UTF8|ConvertFrom-Json } catch { throw 'Runtime authorization schema is invalid JSON' }
if([int]$schema.schemaVersion -ne 1 -and $null -ne $schema.PSObject.Properties['schemaVersion']){throw 'Runtime authorization schema metadata is inconsistent'}
foreach($schemaField in @('schemaVersion','taskId','planHash','commandId','commandHash','taskContractRef','taskContractHash','targetPaths','issuedAtUtc','expiresAtUtc','timeBudgetSeconds','timeoutSeconds','stopCondition','oneTime','developerApproval')){if(@($schema.required|ForEach-Object {[string]$_}) -notcontains $schemaField){throw "Runtime authorization schema omits required field: $schemaField"}}
$auth=Get-Content -LiteralPath $full -Raw -Encoding UTF8|ConvertFrom-Json
$required=@('schemaVersion','taskId','planHash','commandId','commandHash','taskContractRef','taskContractHash','targetPaths','issuedAtUtc','expiresAtUtc','timeBudgetSeconds','timeoutSeconds','stopCondition','oneTime','developerApproval')
foreach($p in $required){if($null -eq $auth.PSObject.Properties[$p]){throw "Missing authorization field: $p"}}
if([int]$auth.schemaVersion -ne 1){throw 'Unsupported runtime authorization schemaVersion'}
foreach($p in @('taskId','commandId','taskContractRef','stopCondition')){if([string]::IsNullOrWhiteSpace([string]$auth.$p)){throw "Authorization field must be non-empty: $p"}}
foreach($p in @('planHash','commandHash','taskContractHash')){if([string]$auth.$p -notmatch '^[0-9a-f]{64}$'){throw "Invalid $p"}}
if([bool]$auth.oneTime -ne $true){throw 'Runtime authorization must be one-time'}
if(@($auth.targetPaths).Count -eq 0){throw 'Runtime authorization requires at least one target path'}
if([int]$auth.timeBudgetSeconds -le 0 -or [int]$auth.timeoutSeconds -le 0 -or [int]$auth.timeoutSeconds -gt [int]$auth.timeBudgetSeconds){throw 'Runtime budget and timeout must be positive and timeout cannot exceed budget'}
$issued=[DateTimeOffset]::MinValue; $expires=[DateTimeOffset]::MinValue
try { $issued=[DateTimeOffset]::Parse([string]$auth.issuedAtUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind); $expires=[DateTimeOffset]::Parse([string]$auth.expiresAtUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind) } catch { throw 'Authorization timestamps must be ISO-8601' }
if($expires -le $issued){throw 'Authorization expiry must be later than issue time'}
$contractRef=[string]$auth.taskContractRef
if([IO.Path]::IsPathRooted($contractRef)){throw 'TaskContractRef must be project-relative'}
$contractFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$contractRef))
if(-not $contractFull.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'TaskContractRef escapes ProjectRoot'}
if(-not(Test-Path -LiteralPath $contractFull -PathType Leaf)){throw 'TaskContractRef does not exist'}
if((Get-FileHash -Algorithm SHA256 -LiteralPath $contractFull).Hash.ToLowerInvariant() -ne [string]$auth.taskContractHash){throw 'TaskContractRef hash does not match taskContractHash'}
if([string]::IsNullOrWhiteSpace([string]$auth.developerApproval)){throw 'developerApproval must be an explicit non-empty approval record'}
foreach($target in @($auth.targetPaths)){if([string]::IsNullOrWhiteSpace([string]$target)){throw 'Target path cannot be empty'};if([IO.Path]::IsPathRooted([string]$target)){throw 'Target path must be project-relative'};$candidate=[IO.Path]::GetFullPath([IO.Path]::Combine($root,[string]$target));if(-not $candidate.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Target path escapes ProjectRoot'}}
if($expires.ToUniversalTime() -le [DateTimeOffset]::UtcNow){throw 'Runtime authorization expired'}
Write-Output 'PASS: runtime authorization contract is structurally valid and unexpired.'
