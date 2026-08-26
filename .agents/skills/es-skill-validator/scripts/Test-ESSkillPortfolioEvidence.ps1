[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$EvidencePath)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path; $receipt=(Resolve-Path -LiteralPath $EvidencePath).Path
$decisionModule=Join-Path $root '.agents/skills/es-skill-validator/scripts/ESPortfolioDecision.psm1'
Import-Module $decisionModule -Force
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Relative([string]$path){$prefix=$root.TrimEnd('\','/');$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($prefix+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes ProjectRoot'};return $full.Substring($prefix.Length+1).Replace('\','/')}
$raw=[IO.File]::ReadAllText($receipt,(New-Object Text.UTF8Encoding($false,$true))); try{$r=$raw|ConvertFrom-Json}catch{throw 'Portfolio receipt is not valid JSON'}
foreach($p in 'case','status','evidenceLevel','receiptPath','sourceRefs','timestampUtc','portfolioHash','catalogHash','resourceIndexHash','validatorHash','sourceRefHashes','innerReportPath','innerReportHash','innerResultAvailable','validatorReviewCount','evidencePendingCount','decisionStatus','effect','staticStatus','evidenceStatus','runtimeStatus','blockingLayer'){if($null -eq $r.PSObject.Properties[$p]){throw "Missing portfolio receipt field: $p"}}
if([string]$r.case -ne 'portfolio-gate'){throw 'Receipt is not a portfolio-gate receipt'}
if([string]$r.receiptPath -ne (Relative $receipt)){throw 'receiptPath does not identify this receipt'}
if([string]$r.status -notmatch '^(passed|review|blocked)$'){throw 'Invalid portfolio receipt status'}
$catalog=Join-Path $root '.agents/SKILL_CATALOG.yaml'; $resource=Join-Path $root '.agents/SKILL_RESOURCE_INDEX.yaml'; $validator=Join-Path $root '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
if([string]$r.catalogHash -ne (Hash $catalog)){throw 'Portfolio catalogHash is stale'}
if([string]$r.resourceIndexHash -ne (Hash $resource)){throw 'Portfolio resourceIndexHash is stale'}
if([string]$r.validatorHash -ne (Hash $validator)){throw 'Portfolio validatorHash is stale'}
foreach($ref in @($r.sourceRefs)){$path=Join-Path $root ([string]$ref);if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Portfolio sourceRef missing: $ref"};$property=$r.sourceRefHashes.PSObject.Properties[[string]$ref];if($null -eq $property){$property=$r.sourceRefHashes.PSObject.Properties[([string]$ref).Replace('/','_')]};if($null -eq $property -or [string]$property.Value -ne (Hash $path)){throw "Portfolio sourceRef hash is stale: $ref"}}
$innerPath=Join-Path $root ([string]$r.innerReportPath)
if([IO.Path]::IsPathRooted([string]$r.innerReportPath) -or -not(Test-Path -LiteralPath $innerPath -PathType Leaf) -or (Relative $innerPath) -ne [string]$r.innerReportPath){throw 'innerReportPath is invalid or missing'}
if([string]$r.innerReportHash -ne (Hash $innerPath)){throw 'innerReportHash is stale'}
$hardFailureCount=@($r.validatorBlocked).Count+@($r.validatorFailures).Count+@($r.validatorNotRun).Count+@($r.contractFailures).Count+@($r.resourceFailures).Count
$decision=Resolve-ESPortfolioDecision -InnerResultAvailable ([bool]$r.innerResultAvailable) -HardFailureCount $hardFailureCount -EvidencePendingCount ([int]$r.evidencePendingCount) -ValidatorReviewCount ([int]$r.validatorReviewCount)
foreach($field in @('status','decisionStatus','effect','blockingLayer')){if([string]$r.$field -ne [string]$decision.$field){throw "Portfolio decision mismatch: $field"}}
$expectedEvidenceStatus=if([int]$r.evidencePendingCount -gt 0){'evidence-pending'}else{'evidence-closed'}
if([string]$r.evidenceStatus -ne $expectedEvidenceStatus){throw 'Portfolio evidenceStatus does not match evidencePendingCount'}
$expectedHash=Get-ESPortfolioProjectionHash -Receipt $r
if([string]$r.portfolioHash -ne $expectedHash){throw 'portfolioHash does not match the canonical receipt projection'}
Write-Output 'PASS: portfolio receipt is bound to current catalog, resource index and validator'
