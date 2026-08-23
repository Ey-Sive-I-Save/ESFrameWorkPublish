[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$PlanPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($PlanPath)){throw 'PlanPath must be project-relative.'};$relative=$PlanPath.Replace('\','/').Trim()
if($relative.Contains('..')-or $relative -notmatch '^ES/Output/.+\.json$'){throw 'PlanPath must remain under ES/Output.'}
$full=Join-Path $root ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Migration plan missing: $relative"}
$outputRoot=(Resolve-Path -LiteralPath (Join-Path $root 'ES\Output')).Path.TrimEnd('\','/');$resolved=(Resolve-Path -LiteralPath $full).Path
if(-not $resolved.StartsWith("$outputRoot$([IO.Path]::DirectorySeparatorChar)",[StringComparison]::OrdinalIgnoreCase)){throw 'Resolved plan path escapes ES/Output.'}
$raw=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($resolved));if($raw -match '(?i)"(?:delete|publish|overwrite|destructive)"\s*:\s*(?:true|"true")'){throw 'Migration plan contains destructive execution permission.'}
$plan=$raw|ConvertFrom-Json;foreach($p in @('schemaVersion','source','target','preservationLedger','scope','batchSize','maxRetries','rollback','compatibilityWindow','owner','evidenceRef','staleWhen','executionMode')){if($null -eq $plan.PSObject.Properties[$p]){throw "Missing migration field: $p"}}
if([string]$plan.schemaVersion -ne '1'-or [string]$plan.executionMode -ne 'dry-run'){throw 'Migration plan must use schemaVersion 1 and dry-run mode.'}
if([int]$plan.batchSize -lt 1 -or [int]$plan.maxRetries -lt 0){throw 'batchSize/maxRetries are invalid.'};if(@($plan.preservationLedger).Count -eq 0){throw 'Preservation ledger is required.'}
$allowed='keep|normalize-in-place|merge|split|archive-with-link|deprecate-with-redirect|defer';foreach($entry in @($plan.preservationLedger)){foreach($p in @('original','retainedPath','disposition')){if([string]::IsNullOrWhiteSpace([string]$entry.$p)){throw "Ledger entry missing $p."}};if([string]$entry.disposition -notmatch "^($allowed)$"){throw "Invalid ledger disposition: $($entry.disposition)"}}
Write-Output "PASS: migration plan is reversible, dry-run only, and preserves legacy evidence: $relative"
