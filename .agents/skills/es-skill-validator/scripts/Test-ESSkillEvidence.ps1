[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SkillPath,
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [string]$ProjectRoot
)
$ErrorActionPreference='Stop'
$skill=(Resolve-Path -LiteralPath $SkillPath).Path; $name=Split-Path -Leaf $skill
$root=if($ProjectRoot){(Resolve-Path -LiteralPath $ProjectRoot).Path}else{Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skill))}
$receipt=(Resolve-Path -LiteralPath $EvidencePath -ErrorAction Stop).Path
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Relative([string]$path){$prefix=$root.TrimEnd('\','/');$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($prefix+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes ProjectRoot'};return $full.Substring($prefix.Length+1).Replace('\','/')}
if(-not (Test-Path -LiteralPath $receipt -PathType Leaf)){throw "Evidence receipt not found: $EvidencePath"}
$raw=[IO.File]::ReadAllText($receipt,(New-Object Text.UTF8Encoding($false,$true))); try{$r=$raw|ConvertFrom-Json}catch{throw 'Evidence receipt is not valid JSON'}
if([string]$r.case -eq 'portfolio-gate'){throw 'Portfolio receipt must be validated by Test-ESSkillPortfolioEvidence.ps1'}
$timestampProperty = if($null -ne $r.PSObject.Properties['timestampUtc']) { 'timestampUtc' } elseif($null -ne $r.PSObject.Properties['capturedUtc']) { 'capturedUtc' } else { $null }
foreach($p in 'skillName','case','status','evidenceLevel','receiptPath','sourceRefs','skillHash','governanceHash','validatorHash','planHash','sourceRefHashes'){if($null -eq $r.PSObject.Properties[$p]){throw "Missing receipt field: $p"}}
if($null -eq $timestampProperty){throw 'Missing receipt timestampUtc/capturedUtc field'}
if([string]$r.skillName -ne $name){throw 'Receipt skillName mismatch'}
if([string]$r.status -notmatch '^(passed|failed|blocked|not-run)$'){throw 'Invalid receipt status'}
if(@($r.sourceRefs).Count -eq 0){throw 'Receipt must include sourceRefs'}
if([string]$r.receiptPath -ne (Relative $receipt)){throw 'receiptPath does not identify this receipt'}
if([string]$r.skillHash -ne (Hash (Join-Path $skill 'SKILL.md'))){throw 'Receipt skillHash is stale'}
if([string]$r.governanceHash -ne (Hash (Join-Path $skill 'governance.json'))){throw 'Receipt governanceHash is stale'}
$validatorPath=Join-Path $root '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
if([string]$r.validatorHash -ne (Hash $validatorPath)){throw 'Receipt validatorHash is stale'}
if([string]$r.planHash -notmatch '^[a-fA-F0-9]{64}$'){throw 'Receipt planHash must be a SHA-256 binding'}
foreach($ref in @($r.sourceRefs)){
    $refPath=Join-Path $root ([string]$ref)
    if(-not (Test-Path -LiteralPath $refPath -PathType Leaf)){throw "Receipt sourceRef missing: $ref"}
    $property=$r.sourceRefHashes.PSObject.Properties[[string]$ref]
    if($null -eq $property){$property=$r.sourceRefHashes.PSObject.Properties[([string]$ref).Replace('/','_')]}
    if($null -eq $property -or [string]$property.Value -ne (Hash $refPath)){throw "Receipt sourceRef hash is stale: $ref"}
}
Write-Output "PASS: $name evidence receipt is bound to current project semantics"
