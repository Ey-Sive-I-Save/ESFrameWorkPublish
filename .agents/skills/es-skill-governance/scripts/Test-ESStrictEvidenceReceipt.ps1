[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SkillPath,
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [string]$ProjectRoot,
    [ValidateRange(1,8760)][int]$MaxEvidenceAgeHours=168
)
$ErrorActionPreference='Stop'
$skill=(Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$root=if($ProjectRoot){(Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path}else{Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skill))}
$receipt=(Resolve-Path -LiteralPath $EvidencePath -ErrorAction Stop).Path
$prefix=$root.TrimEnd('\','/')+'\'
function Relative([string]$path){$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes ProjectRoot'};$full.Substring($root.Length+1).Replace('\','/')}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
if(-not (Test-Path -LiteralPath $receipt -PathType Leaf)){throw "Evidence receipt not found: $EvidencePath"}
$raw=[IO.File]::ReadAllText($receipt,(New-Object Text.UTF8Encoding($false,$true)));try{$r=$raw|ConvertFrom-Json}catch{throw 'Evidence receipt is not strict UTF-8 JSON'}
foreach($field in @('skillName','case','status','evidenceLevel','receiptPath','sourceRefs','sourceRefHashes','toolId','unityVersion','capturedUtc','planHash')){if($null -eq $r.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$r.$field)){throw "Missing strict receipt field: $field"}}
$name=Split-Path -Leaf $skill;if([string]$r.skillName -ne $name){throw 'Receipt skillName mismatch'}
if([string]$r.status -notmatch '^(passed|failed|blocked|not-run)$'){throw 'Invalid receipt status'}
if([string]$r.evidenceLevel -notmatch '^S[0-6]$'){throw 'Invalid evidence level'}
if([string]$r.receiptPath -ne (Relative $receipt)){throw 'receiptPath does not identify this receipt'}
if([string]$r.planHash -notmatch '^[a-fA-F0-9]{64}$'){throw 'planHash must be SHA-256'}
try{$captured=[DateTime]::Parse([string]$r.capturedUtc).ToUniversalTime()}catch{throw 'capturedUtc must be an ISO timestamp'}
if(([DateTime]::UtcNow-$captured).TotalHours -gt $MaxEvidenceAgeHours){throw "Evidence receipt is older than $MaxEvidenceAgeHours hours"}
foreach($ref in @($r.sourceRefs)){$refText=[string]$ref;if([IO.Path]::IsPathRooted($refText)){throw "sourceRef must be project-relative: $refText"};$refPath=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$refText));if(-not $refPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $refPath -PathType Leaf)){throw "Receipt sourceRef missing: $refText"};$prop=$r.sourceRefHashes.PSObject.Properties[$refText];if($null -eq $prop){$prop=$r.sourceRefHashes.PSObject.Properties[$refText.Replace('/','_')]};if($null -eq $prop -or [string]$prop.Value -ne (Hash $refPath)){throw "Receipt sourceRef hash is stale: $refText"}}
Write-Output "PASS: strict evidence receipt contract: $name/$($r.case)"
