[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$AuditPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($AuditPath)){throw 'AuditPath must be project-relative.'};$relative=$AuditPath.Replace('\','/').Trim()
if($relative.Contains('..')-or $relative -notmatch '^ES/Output/.+\.json$'){throw 'AuditPath must remain under ES/Output.'}
$full=Join-Path $root ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Resource audit missing: $relative"}
$outputRoot=(Resolve-Path -LiteralPath (Join-Path $root 'ES\Output')).Path.TrimEnd('\','/');$resolved=(Resolve-Path -LiteralPath $full).Path
if(-not $resolved.StartsWith("$outputRoot$([IO.Path]::DirectorySeparatorChar)",[StringComparison]::OrdinalIgnoreCase)){throw 'Resolved audit path escapes ES/Output.'}
$audit=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($resolved))|ConvertFrom-Json;foreach($p in @('schemaVersion','platform','verdict','owner','assets')){if($null -eq $audit.PSObject.Properties[$p]){throw "Missing audit field: $p"}}
if([string]$audit.schemaVersion -ne '1'-or [string]$audit.verdict -notmatch '^(blocked|audit-only|accepted)$'){throw 'Invalid resource audit schema or verdict.'};if(@($audit.assets).Count -eq 0){throw 'At least one audited asset is required.'}
$ids=@{};foreach($asset in @($audit.assets)){foreach($p in @('AssetId','Source','Plan','Manifest','Bundle','Hash','Provider','Download','Load','LeaseScope','Rollback','EvidenceRef','Owner','StaleWhen')){if([string]::IsNullOrWhiteSpace([string]$asset.$p)){throw "Asset missing $p."}};if([string]$asset.Hash -notmatch '^[0-9a-fA-F]{64}$'){throw "Asset hash must be SHA-256: $($asset.AssetId)"};if($ids.ContainsKey([string]$asset.AssetId)){throw "Duplicate AssetId: $($asset.AssetId)"};$ids[[string]$asset.AssetId]=$true;if([string]$audit.verdict -eq 'accepted' -and [string]$asset.EvidenceRef -eq 'not-run'){throw 'Accepted resource audit requires runtime evidence.'}}
Write-Output "PASS: resource audit covers identity, artifact hash, Provider, Scope, rollback and evidence: $relative"
